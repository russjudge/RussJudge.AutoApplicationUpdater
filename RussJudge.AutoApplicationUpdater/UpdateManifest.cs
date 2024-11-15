using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RussJudge.AutoApplicationUpdater
{
    public class UpdateManifest
    {
        public const string ManifestExtension = ".UpdateManifest";
        private UpdateManifest(string json)
        {
            var manifest = Deserialize(json);
            LoadManifestData(Deserialize(json));
        }
        private void LoadManifestData(UpdateManifest? manifest)
        {
            if (manifest != null)
            {

            }
        }
        public UpdateManifest() { }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="jsonOrUpdateManifestFileOrAssemblyPathExecutable"></param>
        /// <param name="remoteURLSourcePackageInstaller">Required if the AssemblyPath executable is passed.
        /// Sets the URL to the application package installer.</param>
        public UpdateManifest(string jsonOrUpdateManifestFileOrAssemblyPathExecutable, string? remoteURLSourcePackageInstaller = null)
        {
            if (jsonOrUpdateManifestFileOrAssemblyPathExecutable.Substring(1, 2).Equals(@":\")
                && !jsonOrUpdateManifestFileOrAssemblyPathExecutable.Contains('\n'))
            {
                FileInfo f = new(jsonOrUpdateManifestFileOrAssemblyPathExecutable);
                if (f.Name.EndsWith(ManifestExtension, StringComparison.OrdinalIgnoreCase))
                {
                    using StreamReader sr = new(f.FullName);
                    LoadManifestData(Deserialize(sr.ReadToEnd()));

                    Executable = m.Executable;
                    Version = m.Version;
                    FileSize = m.FileSize;
                    FileChecksum = m.FileChecksum;
                    RemoteURLSourcePackage = m.RemoteURLSourcePackage;
                }
                else
                {
                    Executable = f.Name;
                    if (f.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        || f.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        Version = GetVersionData(f.FullName);
                    }
                    FileSize = f.Length;
                    FileChecksum = GetFileChecksum(f.FullName);
                    RemoteURLSourcePackage = remoteURLSourcePackageInstaller;
                    if (remoteURLSourcePackageInstaller.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        || remoteURLSourcePackageInstaller.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
                        || remoteURLSourcePackageInstaller.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        || remoteURLSourcePackageInstaller.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase))
                    {
                        RemoteURLSourcePackageIsRelativeOnLocalWindowsServer = false;
                    }
                    else
                    {
                        RemoteURLSourcePackageIsRelativeOnLocalWindowsServer = true;
                    }
                }
            }
            else
            {
                ParseDatafile(jsonOrUpdateManifestFileOrAssemblyPathExecutable);
            }
        }

        public string Executable { get; set; } = string.Empty;


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

        public int Major { get; set; }
        public int Minor { get; set; }
        public int Revision { get; set; }
        public int Build { get; set; }

        public long FilePackageSize { get; set; } = 0;
        public string FilePackageChecksum { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = false;
        public string RemoteURLSourcePackage { get; set; } = string.Empty;
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
        public static UpdateManifest? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<UpdateManifest>(json);
        }
    }
}
