using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace RussJudge.AutoApplicationUpdater
{

    /// <summary>
    /// Information for determining whether an update is needed, the location of the installer package.
    /// </summary>
    public class UpdateManifest
    {
        /// <summary>
        /// Extension used for the Application version information file for automatic self-update.
        /// </summary>
        public const string DefaultManifestExtension = ".json";

        /// <summary>
        /// List of supported compression types, in addition to zip.
        /// </summary>
        public static Dictionary<string, Type> CompressionTypes { get; private set; } =
                new([new("gzip", typeof(System.IO.Compression.GZipStream)),
                    new("br", typeof(System.IO.Compression.BrotliStream)),
                    new("zlib", typeof(System.IO.Compression.ZLibStream)) ]);

        internal static string? LastError { get; set; }
        internal static string? Decompress(string compressedFile, Type compressorType)
        {
            string? retVal = null;
            int pos = compressedFile.LastIndexOf('.');
            if (pos > 0)
            {
                try
                {
                    retVal = compressedFile[..pos];
                    using FileStream compressedFileStream = File.Open(compressedFile, FileMode.Open);
                    using FileStream outputFileStream = File.Create(retVal);

                    ConstructorInfo? constructor = compressorType.GetConstructor([typeof(Stream), typeof(CompressionMode)]);
                    if (constructor != null)
                    {
                        using var decompressor = (Stream)constructor.Invoke([compressedFileStream, CompressionMode.Decompress]);
                        decompressor.CopyTo(outputFileStream);
                    }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                }
            }
            return retVal;
        }
        internal static string? UnzipFile(string packagedZip)
        {
            string? retVal = null;
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(packagedZip))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        retVal = Path.Combine(Path.GetTempPath(), entry.Name);

                        entry.ExtractToFile(retVal, true);
                    }
                }
                if (retVal == null)
                {
                    LastError = "Compressed package file was empty.  Cannot proceed.  Please try again or contact the developer if the problem continues.";
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            return retVal;
        }


        private const string DLLExtension = ".dll";
        private const string EXEExtension = ".exe";
        /// <summary>
        /// The file extension for zip files.
        /// </summary>
        public const string ZIPExtension = ".zip";

        static readonly HttpClient httpClient = new();

        private void LoadManifestData(UpdateManifest? manifest)
        {
            if (manifest != null)
            {
                _versionInfo = new(manifest.Version);

                this.Executable = manifest.Executable;
                this.FilePackageChecksum = manifest.FilePackageChecksum;
                this.FilePackageSize = manifest.FilePackageSize;
                this.IsRequired = manifest.IsRequired;
                this.RemoteURLSourcePackage = manifest.RemoteURLSourcePackage;
            }
        }
        private UpdateManifest() { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jsonOrUpdateManifestFileOrAssemblyPathExecutable">The source of data for creating the Manifest file.
        /// This can be JSON data, or the path to a file with the JSON data, or it can be the path to the referenced AssemblyPath executable.</param>
        /// <param name="remoteURLSourcePackageInstaller">Required if the AssemblyPath executable is passed.
        /// Sets the URL to the application package installer.</param>
        public UpdateManifest(string jsonOrUpdateManifestFileOrAssemblyPathExecutable, string? remoteURLSourcePackageInstaller = null)
        {

            if (jsonOrUpdateManifestFileOrAssemblyPathExecutable.Substring(1, 2).Equals(@":\")
                && !jsonOrUpdateManifestFileOrAssemblyPathExecutable.Contains('\n'))
            {
                FileInfo f = new(jsonOrUpdateManifestFileOrAssemblyPathExecutable);
                if (f.Name.EndsWith(DLLExtension, StringComparison.OrdinalIgnoreCase)
                    || f.Name.EndsWith(EXEExtension, StringComparison.OrdinalIgnoreCase))
                {
                    Executable = f.Name;

                    _versionInfo = GetVersionData(f.FullName);


                    if (!string.IsNullOrEmpty(remoteURLSourcePackageInstaller))
                    {

                        RemoteURLSourcePackage = remoteURLSourcePackageInstaller;
                    }
                }
                else
                {
                    using StreamReader sr = new(f.FullName);
                    LoadManifestData(Deserialize(sr.ReadToEnd()));
                    if (!string.IsNullOrEmpty(remoteURLSourcePackageInstaller))
                    {
                        RemoteURLSourcePackage = remoteURLSourcePackageInstaller;
                    }
                }

            }
            else
            {
                LoadManifestData(Deserialize(jsonOrUpdateManifestFileOrAssemblyPathExecutable));
            }
        }
        /// <summary>
        /// The assembly location for version checking.
        /// </summary>
        public string Executable { get; set; } = string.Empty;


        private VersionInfo? _versionInfo;
        /// <summary>
        /// The Version for determining an update.
        /// </summary>
        public string Version
        {
            get
            {
                if (_versionInfo == null)
                {
                    return "0.0.0.0";
                }
                else
                {
                    return _versionInfo.Version;
                }
            }
            set
            {
                _versionInfo = new(value);
            }
        }

        /// <summary>
        /// The major version of an update.
        /// </summary>
        [JsonIgnore]
        public int Major
        {
            get
            {
                if (_versionInfo == null)
                {
                    return 0;
                }
                else
                {
                    return _versionInfo.Major;
                }
            }
        }
        /// <summary>
        /// The minor version of an update.
        /// </summary>
        [JsonIgnore]
        public int Minor
        {
            get
            {
                if (_versionInfo == null)
                {
                    return 0;
                }
                else
                {
                    return _versionInfo.Minor;
                }
            }
        }

        /// <summary>
        /// The revision of an update.
        /// </summary>
        [JsonIgnore]
        public int Revision
        {
            get
            {
                if (_versionInfo == null)
                {
                    return 0;
                }
                else
                {
                    return _versionInfo.Revision;
                }
            }
        }

        /// <summary>
        /// The build number of an update.
        /// </summary>
        [JsonIgnore]
        public int Build
        {
            get
            {
                if (_versionInfo == null)
                {
                    return 0;
                }
                else
                {
                    return _versionInfo.Build;
                }
            }
        }


        /// <summary>
        /// The package filename, without the path.
        /// </summary>
        public string FilePackageName { get; set; } = string.Empty;

        /// <summary>
        /// The size in bytes of the package file.
        /// </summary>
        public long FilePackageSize { get; set; } = 0;
        /// <summary>
        /// The MD5 checksum of the package file.
        /// </summary>
        public string FilePackageChecksum { get; set; } = string.Empty;



        /// <summary>
        /// Whether or not the update is required.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// The URL to the installer package.
        /// </summary>
        public string RemoteURLSourcePackage { get; set; } = string.Empty;

        /// <summary>
        /// Downloads the Remote Update Manifest file.
        /// </summary>
        /// <param name="RemoteSourceManifestFileURL"></param>
        /// <returns>The response from the remote, including the manifest if successful, or any error messages if failed.</returns>
        public static async Task<RemoteResponse?> GetRemoteManifestFile(string RemoteSourceManifestFileURL)
        {
            RemoteResponse retVal;
            try
            {
                string responseBody = await httpClient.GetStringAsync(RemoteSourceManifestFileURL);
                retVal = new(HttpStatusCode.OK, null, null, true, responseBody);
            }
            catch (HttpRequestException ex)
            {
                HttpStatusCode code = HttpStatusCode.BadRequest;
                if (ex.StatusCode != null)
                {
                    code = ex.StatusCode.Value;
                }
                retVal = new(code, ex.Message, null, false);
            }
            return retVal;
        }

        /// <summary>
        /// Downloads the update manifest from the remote URL.
        /// </summary>
        /// <param name="UpdateManifestFilePath">The local path to the UpdateManifest File</param>
        /// <returns>The Update Manifest file.</returns>

        public static UpdateManifest GetManifestFile(string UpdateManifestFilePath)
        {
            using StreamReader sr = new(UpdateManifestFilePath);
            return new(sr.ReadToEnd());
        }
        /// <summary>
        /// Downloads the installer package from the remote URL.
        /// </summary>
        /// <param name="remotePackageURL">The URL to the remote installer package.</param>
        /// <returns>The response from the remote, including the installer package if successful, or any error messages if failed.</returns>
        public async Task<RemoteResponse> GetRemotePackage(string remotePackageURL)
        {
            RemoteResponse retVal;
            try
            {
                using var result = await httpClient.GetAsync(remotePackageURL);
                if (result.IsSuccessStatusCode)
                {
                    var content = await result.Content.ReadAsByteArrayAsync();
                    string remoteChecksum = GetFileChecksum(content);
                    if (string.IsNullOrEmpty(FilePackageChecksum) || remoteChecksum.Equals(FilePackageChecksum))
                    {
                        string file = remotePackageURL;
                        int i = remotePackageURL.LastIndexOf('/');
                        if (i > 0)
                        {
                            file = remotePackageURL[(i + 1)..];
                        }

                        retVal = new(result.StatusCode, result.ReasonPhrase, result.RequestMessage, result.IsSuccessStatusCode, content);

                    }
                    else
                    {
                        retVal = new(result.StatusCode, $"Remote Package checksum ({remoteChecksum}) does not match expected checksum from Update Manifest ({FilePackageChecksum}).", result.RequestMessage, false);
                    }
                }
                else
                {

                    retVal = new(result.StatusCode, result.ReasonPhrase, result.RequestMessage, result.IsSuccessStatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                HttpStatusCode code = HttpStatusCode.BadRequest;
                if (ex.StatusCode != null)
                {
                    code = ex.StatusCode.Value;
                }
                retVal = new(code, ex.Message, null, false);
            }
            return retVal;
        }
        /// <summary>
        /// Downloads the remote installer package.
        /// </summary>
        /// <returns>The response from the remote, including the installer packkage if successful, or any error messages if failed.</returns>
        public async Task<RemoteResponse?> GetRemotePackage()
        {
            return await GetRemotePackage(RemoteURLSourcePackage);
        }


        /// <summary>
        /// Tests the assembly against the latest version and determines if an update is needed.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly needing tested.</param>
        /// <returns>True if the this version is higher than the passed local assembly version, otherwise false.</returns>
        public bool NeedsUpdated(string assemblyPath)
        {

            bool retVal = false;
            var localVersion = GetVersionData(assemblyPath);
            if (_versionInfo != null)
            {
                retVal = _versionInfo.IsNewer(localVersion);
            }
            return retVal;
        }

        /// <summary>
        /// Validates that the checksum of the local file matches the expected checksum as provided by the remote Update Manifest file.
        /// </summary>
        /// <param name="path">The local path to the downloaded installer package.</param>
        /// <returns>True if the checksum is valid.</returns>
        public bool PackageIsValid(string path)
        {
            return GetFileChecksum(path).Equals(FilePackageChecksum);
        }


        /// <summary>
        /// Validates that the checksum of the local file matches the expected checksum as provided by the remote Update Manifest file.
        /// </summary>
        /// <param name="package">The installer package.</param>
        /// <returns>True if the checksum is valid.</returns>
        public bool PackageIsValid(byte[] package)
        {
            return GetFileChecksum(package).Equals(FilePackageChecksum);
        }


        private static VersionInfo GetVersionData(string FullAssemblyFile)
        {
            if (!string.IsNullOrEmpty(FullAssemblyFile)
               && File.Exists(FullAssemblyFile))
            {
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(FullAssemblyFile);
                return new(fileVersionInfo);
            }
            else
            {
                return new("0.0.0.0");
            }
        }
        /// <summary>
        /// Gets the MD5 hash checksum for the provided file.
        /// </summary>
        /// <param name="filePath">The full local path to the file to get the checksum for.</param>
        /// <returns>The string representation of the MD5 checksum of the file.</returns>
        public static string GetFileChecksum(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(MD5.HashData(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
        /// <summary>
        /// Gets the MD5 hash checksum for the provided file.
        /// </summary>
        /// <param name="bytes">The full binary data of the file.</param>
        /// <returns>The string representation of the MD5 checksum of the file.</returns>
        public static string GetFileChecksum(byte[] bytes)
        {
            return BitConverter.ToString(MD5.HashData(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
        /// <summary>
        /// Serializes the UpdateManifest into JSON data.
        /// </summary>
        /// <returns>The JSON for the UpdateManifest</returns>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }
        /// <summary>
        /// Converts valid JSON into an UpdateManifest object.
        /// </summary>
        /// <param name="json">The valid JSON of UpdateManifest object.</param>
        /// <returns>The UpdateManifest of the JSON data.</returns>
        public static UpdateManifest? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<UpdateManifest>(json);
        }
    }
}
