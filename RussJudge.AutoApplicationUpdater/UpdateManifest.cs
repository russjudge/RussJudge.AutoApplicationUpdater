using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RussJudge.AutoApplicationUpdater
{

    /// <summary>
    /// Information for determining whether an update is needed, the location of the installer package.
    /// </summary>
    [JsonSerializable(typeof(UpdateManifest))]
    public class UpdateManifest
    {
        /// <summary>
        /// Extension used for the Application version information file for automatic self-update.
        /// </summary>
        public const string DefaultManifestExtension = ".json";

        private const string DLLExtension = ".dll";
        private const string EXEExtension = ".exe";
        static readonly HttpClient httpClient = new();

        private void LoadManifestData(UpdateManifest? manifest)
        {
            if (manifest != null)
            {
                this.Version = manifest.Version;
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
                    Version = GetVersionData(f.FullName);

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
                        RemoteURLSourcePackage = RemoteURLSourcePackage;
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

        /// <summary>
        /// The Version for determining an update.
        /// </summary>
        public string Version
        {
            get
            {
                return $"{Major}.{Minor}.{Revision}.{Build}";
            }
            set
            {
                var parts = value.Split('.');
                if (parts.Length > 0)
                {
                    if (int.TryParse(parts[0], out int v1))
                    {
                        Major = v1;
                    }
                }
                if (parts.Length > 1)
                {
                    if (int.TryParse(parts[1], out int v1))
                    {
                        Minor = v1;
                    }
                }
                if (parts.Length > 2)
                {
                    if (int.TryParse(parts[2], out int v1))
                    {
                        Revision = v1;
                    }
                }
                if (parts.Length > 3)
                {
                    if (int.TryParse(parts[3], out int v1))
                    {
                        Build = v1;
                    }
                }
            }
        }
        /// <summary>
        /// The major version of an update.
        /// </summary>
        public int Major { get; set; }
        /// <summary>
        /// The minor version of an update.
        /// </summary>
        public int Minor { get; set; }
        /// <summary>
        /// The revision of an update.
        /// </summary>
        public int Revision { get; set; }
        /// <summary>
        /// The build number of an update.
        /// </summary>
        public int Build { get; set; }

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
        /// 
        /// </summary>
        /// <param name="RemoteSourceManifestFileURL"></param>
        /// <exception cref="HttpRequestException">Thrown on an error trying to download the remote UpdateManifest</exception>
        /// <returns></returns>
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
        /// <param name="remoteUpdateManifestUrl"></param>
        /// <returns></returns>

        public static UpdateManifest GetManifestFile(string remoteUpdateManifestUrl)
        {
            using StreamReader sr = new(remoteUpdateManifestUrl);
            return new(sr.ReadToEnd());
        }
        /// <summary>
        /// Downloads the installer package from the remote URL.
        /// </summary>
        /// <param name="remotePackageURL">The URL to the remote installer package.</param>
        /// <returns></returns>
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
        public async Task<RemoteResponse?> GetRemotePackage()
        {
            return await GetRemotePackage(RemoteURLSourcePackage);
        }



        public bool FileNeedsUpdated(string assemblyPath)
        {
            return !GetVersionData(assemblyPath).Equals(Version);
        }
        public bool PackageIsValid(string path)
        {
            return GetFileChecksum(path).Equals(FilePackageChecksum);
        }
        public bool PackageIsValid(byte[] package)
        {
            return GetFileChecksum(package).Equals(FilePackageChecksum);
        }
        private static string GetVersionData(string FullAssemblyFile)
        {
            string retVal;

            if (!string.IsNullOrEmpty(FullAssemblyFile)
               && File.Exists(FullAssemblyFile))
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(FullAssemblyFile);
                if (versionInfo != null && !string.IsNullOrEmpty(versionInfo.FileVersion))
                {
                    retVal = versionInfo.FileVersion;
                }
                else
                {
                    if (versionInfo != null && !string.IsNullOrEmpty(versionInfo.ProductVersion))
                    {
                        retVal = versionInfo.ProductVersion;
                    }
                    else
                    {
                        retVal = string.Empty;
                    }
                }
            }
            else
            {
                retVal = string.Empty;
            }
            return retVal;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static string GetFileChecksum(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(MD5.HashData(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
        public static string GetFileChecksum(byte[] bytes)
        {
            return BitConverter.ToString(MD5.HashData(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
        public string Serialize()
        {
            return ToString();
        }
        public static UpdateManifest? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<UpdateManifest>(json);
        }
    }
}
