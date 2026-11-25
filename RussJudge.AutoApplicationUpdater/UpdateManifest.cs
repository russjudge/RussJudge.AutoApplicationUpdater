using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RussJudge.AutoApplicationUpdater
{

    /// <summary>
    /// Information for determining whether an update is needed, the location of the installer package.
    /// </summary>
    public class UpdateManifest
    {
        private const string DLLExtension = ".dll";
        private const string EXEExtension = ".exe";
        private const string zeroVersion = "0.0.0.0";
        /// <summary>
        /// Extension used for the Application version information file for automatic self-update.
        /// </summary>
        public const string DefaultManifestExtension = ".json";
        /// <summary>
        /// The file extension for zip files.
        /// </summary>
        public const string ZIPExtension = ".zip";
        internal UpdateManifest() { }

        /// <summary>
        /// Instantiated UpdateManifest object.
        /// </summary>
        /// <param name="Executable"></param>
        /// <param name="Version"></param>
        /// <param name="RemoteURLSourcePackage"></param>
        /// <param name="FilePackageName"></param>
        /// <param name="FilePackageSize"></param>
        /// <param name="FilePackageChecksum"></param>
        /// <param name="IsRequired"></param>
        [JsonConstructor]
        public UpdateManifest(
            string Executable,
            string Version,
            string RemoteURLSourcePackage,
            string FilePackageName,
            long FilePackageSize,
            string FilePackageChecksum,
            bool IsRequired)
        {
            this.Executable = Executable;
            this.Version = Version;
            this.FilePackageName = FilePackageName;

            this.FilePackageSize = FilePackageSize;

            this.FilePackageChecksum = FilePackageChecksum;

            this.IsRequired = IsRequired;
            this.RemoteURLSourcePackage = RemoteURLSourcePackage;
        }

        /// <summary>
        /// List of supported compression types, in addition to zip.
        /// </summary>
        /// <remarks>Keyed by file extension, including the leading period (for example ".gzip").</remarks>
        public static Dictionary<string, Type> CompressionTypes { get; private set; } =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    [".gzip"] = typeof(System.IO.Compression.GZipStream),
                    [".br"] = typeof(System.IO.Compression.BrotliStream),
                    [".zlib"] = typeof(System.IO.Compression.ZLibStream)
                };

        /// <summary>
        /// Provides the last error message that occurred, if any.
        /// </summary>
        public static string? LastError { get; set; }

        /// <summary>
        /// Decompress a file.
        /// </summary>
        /// <param name="compressedFile">The path to the compressed file.  Naming convension must include the compressed file extension 
        /// appended to the uncompressed file name</param>
        /// <returns>Path to the decompressed file.</returns>
        public static string? Decompress(string compressedFile)
        {
            string? retVal = compressedFile;
            LastError = null;
            int pos = compressedFile.LastIndexOf('.');


            if (pos > 0)
            {
                try
                {
                    retVal = compressedFile[..pos];
                    string compressedExt = compressedFile[retVal.Length..];


                    if (CompressionTypes.TryGetValue(compressedExt, out Type? compressorType) && compressorType != null)
                    {
                        ConstructorInfo? constructor = compressorType.GetConstructor([typeof(Stream), typeof(CompressionMode)]);
                        if (constructor != null)
                        {
                            using FileStream compressedFileStream = File.OpenRead(compressedFile);
                            using FileStream outputFileStream = File.Create(retVal);
                            using var decompressor = (Stream)constructor.Invoke([compressedFileStream, CompressionMode.Decompress]);
                            decompressor.CopyTo(outputFileStream);
                        }
                        else
                        {
                            retVal = null;
                            LastError = $"Constructor for Type {compressorType} not found. Expecting constructor for ({nameof(Stream)}, {nameof(CompressionMode)})";
                        }
                    }
                    else
                    {
                        retVal = null;
                        LastError = "Compressed file extension not recognized.  Only .zip, .gzip, .zlib, and .br are supported.";
                    }
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    retVal = null;
                }
            }
            return retVal;
        }
        /// <summary>
        /// Unzips a zip file.
        /// </summary>
        /// <param name="packagedZip">Path to the zip file.</param>
        /// <returns>Path to the last file extracted from the zip.</returns>
        public static string? UnzipFile(string packagedZip)
        {
            string? retVal = null;
            LastError = null;
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(packagedZip))
                {
                    string tempPath = Path.GetTempPath();
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            //Folder entry--there is nothing to extract.
                            continue;
                        }
                        string extractedFile = Path.Combine(tempPath, entry.Name);

                        entry.ExtractToFile(extractedFile, true);
                        retVal = extractedFile;
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
                retVal = null;
            }
            return retVal;
        }


        private static readonly HttpClient httpClient = new();

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            IgnoreReadOnlyProperties = true,
            IgnoreReadOnlyFields = true,
            WriteIndented = true,
            IncludeFields = false
        };

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
                this.FilePackageName = manifest.FilePackageName;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="jsonOrUpdateManifestFileOrAssemblyPathExecutable">The source of data for creating the Manifest file.
        /// This can be JSON data, or the path to a file with the JSON data, or it can be the path to the referenced AssemblyPath executable.</param>
        /// <param name="remoteURLSourcePackageInstaller">Required if the AssemblyPath executable is passed.
        /// Sets the URL to the application package installer.</param>
        public UpdateManifest(string jsonOrUpdateManifestFileOrAssemblyPathExecutable, string? remoteURLSourcePackageInstaller = null)
        {

            if (IsFilePath(jsonOrUpdateManifestFileOrAssemblyPathExecutable))
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
        /// Determines whether the value passed to the constructor is the path to a file rather than JSON data.
        /// </summary>
        private static bool IsFilePath(string value)
        {
            ReadOnlySpan<char> trimmed = value.AsSpan().TrimStart();
            if (trimmed.IsEmpty || trimmed[0] == '{' || value.AsSpan().IndexOfAny('\r', '\n') >= 0)
            {
                //The JSON of an UpdateManifest always starts with a brace, and a path never spans lines.
                return false;
            }
            //Fully qualified covers drive paths (C:\) and network shares (\\server\share); anything else must exist to count as a path.
            return Path.IsPathFullyQualified(value) || File.Exists(value);
        }

        /// <summary>
        /// Builds an UpdateManifest from JSON data.  Unlike the public constructor, the data is never treated as a file path,
        /// which matters when the data came from a remote server.
        /// </summary>
        /// <param name="json">The JSON of an UpdateManifest object.</param>
        /// <param name="error">The reason the JSON could not be read, or null if it was read successfully.</param>
        /// <returns>The UpdateManifest, which is left empty if the JSON could not be read.</returns>
        internal static UpdateManifest FromJson(string json, out string? error)
        {
            UpdateManifest retVal = new();
            retVal.LoadManifestData(Deserialize(json, out error));
            return retVal;
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
                    return zeroVersion;
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
                string responseBody = await httpClient.GetStringAsync(RemoteSourceManifestFileURL).ConfigureAwait(false);
                UpdateManifest manifest = FromJson(responseBody, out string? error);
                retVal = new(HttpStatusCode.OK, error, null, error == null, manifest);
            }
            catch (Exception ex)
            {
                retVal = CreateFailedResponse(ex);
            }
            return retVal;
        }

        /// <summary>
        /// Converts a failure to reach the remote into a response, so that a timeout or a dropped connection
        /// is reported to the caller (and can fail over to a fallback URL) instead of being thrown.
        /// </summary>
        private static RemoteResponse CreateFailedResponse(Exception ex)
        {
            HttpStatusCode code = ex switch
            {
                HttpRequestException { StatusCode: HttpStatusCode statusCode } => statusCode,
                OperationCanceledException or TimeoutException => HttpStatusCode.RequestTimeout,
                _ => HttpStatusCode.BadRequest
            };
            return new(code, ex.Message, null, false);
        }

        /// <summary>
        /// Downloads the update manifest from the remote URL.
        /// </summary>
        /// <param name="UpdateManifestFilePath">The local path to the UpdateManifest File</param>
        /// <returns>The Update Manifest file.</returns>

        public static UpdateManifest GetManifestFile(string UpdateManifestFilePath)
        {
            return FromJson(File.ReadAllText(UpdateManifestFilePath), out _);
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
                //Only the headers are awaited here.  Otherwise HttpClient.Timeout covers the entire download, and any
                //package that takes longer than that to arrive can never be downloaded.
                using var result = await httpClient.GetAsync(remotePackageURL, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (result.IsSuccessStatusCode)
                {
                    var content = await ReadPackageContent(result.Content).ConfigureAwait(false);
                    bool checksumExpected = !string.IsNullOrEmpty(FilePackageChecksum);
                    string remoteChecksum = checksumExpected ? GetFileChecksum(content) : string.Empty;
                    if (!checksumExpected || ChecksumMatches(remoteChecksum))
                    {
                        retVal = new(result.StatusCode, result.ReasonPhrase, result.RequestMessage, result.IsSuccessStatusCode, content)
                        {
                            PackageChecksumVerified = checksumExpected
                        };
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
            catch (Exception ex)
            {
                retVal = CreateFailedResponse(ex);
            }
            return retVal;
        }

        /// <summary>
        /// Reads the body of a package download, giving up only if the remote stops sending data.
        /// </summary>
        private static async Task<byte[]> ReadPackageContent(HttpContent content)
        {
            //When the remote reports the size, the buffer is created at that size so that it can be returned as-is, without a second copy.
            long? contentLength = content.Headers.ContentLength;
            int capacity = contentLength is > 0 && contentLength <= Array.MaxLength ? (int)contentLength.Value : 0;

            //The time allowed for the remote to respond is also the longest the download may go without receiving any
            //data.  The download can take as long as it needs, so long as it keeps making progress.
            TimeSpan maximumIdleTime = httpClient.Timeout;

            using MemoryStream package = new(capacity);
            using Stream source = await content.ReadAsStreamAsync().ConfigureAwait(false);
            using CancellationTokenSource idleTimeout = new();
            byte[] buffer = new byte[81920];
            try
            {
                while (true)
                {
                    idleTimeout.CancelAfter(maximumIdleTime);
                    int bytesRead = await source.ReadAsync(buffer.AsMemory(), idleTimeout.Token).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    package.Write(buffer, 0, bytesRead);
                }
            }
            catch (OperationCanceledException) when (idleTimeout.IsCancellationRequested)
            {
                throw new TimeoutException($"The download stalled--no data was received for {maximumIdleTime.TotalSeconds:0} seconds.");
            }

            if (package.TryGetBuffer(out ArraySegment<byte> data) && data.Array != null && data.Offset == 0 && data.Count == data.Array.Length)
            {
                return data.Array;
            }
            return package.ToArray();
        }

        private bool ChecksumMatches(string checksum)
        {
            //A checksum is hexadecimal, so its casing does not matter (many tools report it in upper case).
            return checksum.Equals(FilePackageChecksum, StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Downloads the remote installer package.
        /// </summary>
        /// <returns>The response from the remote, including the installer packkage if successful, or any error messages if failed.</returns>
        public async Task<RemoteResponse?> GetRemotePackage()
        {
            return await GetRemotePackage(RemoteURLSourcePackage).ConfigureAwait(false);
        }


        /// <summary>
        /// Tests the assembly against the latest version and determines if an update is needed.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly needing tested.</param>
        /// <returns>True if the this version is higher than the passed local assembly version, otherwise false.</returns>
        public bool NeedsUpdated(string assemblyPath)
        {

            return NeedsUpdated(GetVersionData(assemblyPath));
        }

        internal bool NeedsUpdated(VersionInfo localVersion)
        {
            return _versionInfo != null && _versionInfo.IsNewer(localVersion);
        }

        /// <summary>
        /// Validates that the checksum of the local file matches the expected checksum as provided by the remote Update Manifest file.
        /// </summary>
        /// <param name="path">The local path to the downloaded installer package.</param>
        /// <returns>True if the checksum is valid.</returns>
        public bool PackageIsValid(string path)
        {
            return ChecksumMatches(GetFileChecksum(path));
        }


        /// <summary>
        /// Validates that the checksum of the local file matches the expected checksum as provided by the remote Update Manifest file.
        /// </summary>
        /// <param name="package">The installer package.</param>
        /// <returns>True if the checksum is valid.</returns>
        public bool PackageIsValid(byte[] package)
        {
            return ChecksumMatches(GetFileChecksum(package));
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
                return new(zeroVersion);
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
            return System.Convert.ToHexStringLower(MD5.HashData(stream)); //).Replace("-", string.Empty).ToLowerInvariant();
        }
        /// <summary>
        /// Gets the MD5 hash checksum for the provided file.
        /// </summary>
        /// <param name="bytes">The full binary data of the file.</param>
        /// <returns>The string representation of the MD5 checksum of the file.</returns>
        public static string GetFileChecksum(byte[] bytes)
        {
            //return BitConverter.ToString(MD5.HashData(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            return System.Convert.ToHexStringLower(MD5.HashData(bytes));
        }
        /// <summary>
        /// Serializes the UpdateManifest into JSON data.
        /// </summary>
        /// <returns>The JSON for the UpdateManifest</returns>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this, GetJsonOptions());
        }
        private static JsonSerializerOptions GetJsonOptions()
        {
            //The options instance holds the serializer's metadata cache, so it must be reused rather than rebuilt for every call.
            return jsonOptions;
        }
        /// <summary>
        /// Converts valid JSON into an UpdateManifest object.
        /// </summary>
        /// <param name="json">The valid JSON of UpdateManifest object.</param>
        /// <returns>The UpdateManifest of the JSON data.</returns>
        public static UpdateManifest? Deserialize(string json)
        {
            return Deserialize(json, out _);
        }

        private static UpdateManifest? Deserialize(string json, out string? error)
        {
            try
            {
                LastError = error = null;
                return JsonSerializer.Deserialize<UpdateManifest>(json, GetJsonOptions());
            }
            catch (Exception ex)
            {
                LastError = error = ex.Message;
                return null;
            }
        }
    }
}
