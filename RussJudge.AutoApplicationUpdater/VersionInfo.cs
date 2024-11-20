using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RussJudge.AutoApplicationUpdater
{
    internal class VersionInfo
    {
        public VersionInfo(string version)
        {
            Version = version;
            var parts = version.Split('.');
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
        public VersionInfo(FileVersionInfo? fileVersionInfo)
        {
            if (fileVersionInfo != null && !string.IsNullOrEmpty(fileVersionInfo.FileVersion))
            {
                Version = fileVersionInfo.FileVersion;
                Major = fileVersionInfo.FileMajorPart;
                Minor = fileVersionInfo.FileMinorPart;
                Revision = fileVersionInfo.FileBuildPart;
                Build = fileVersionInfo.FilePrivatePart;
            }
            else
            {
                if (fileVersionInfo != null && !string.IsNullOrEmpty(fileVersionInfo.ProductVersion))
                {
                    Version = fileVersionInfo.ProductVersion;
                    Major = fileVersionInfo.ProductMajorPart;
                    Minor = fileVersionInfo.ProductMinorPart;
                    Revision = fileVersionInfo.ProductBuildPart;
                    Build = fileVersionInfo.ProductPrivatePart;
                }
                else
                {
                    Version = string.Empty;
                }
            }
        }
        public string Version { get; private set; }
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Revision { get; private set; }
        public int Build { get; private set; }

        /// <summary>
        /// Tests if the passed VersionInfo is a newer version than this VersionInfo.
        /// </summary>
        /// <param name="versionInfo">The VersionInfo to compare</param>
        /// <returns>True if the passed VersionInfo is a newer version than this VersionInfo</returns>
        public bool IsNewer(VersionInfo versionInfo)
        {
            return Major > versionInfo.Major
                || (Major == versionInfo.Major && Minor > versionInfo.Minor)
                || (Major == versionInfo.Major && Minor == versionInfo.Minor && Revision > versionInfo.Revision)
                || (Major == versionInfo.Major && Minor == versionInfo.Minor && Revision == versionInfo.Revision && Build > versionInfo.Build);
        }
    }
}
