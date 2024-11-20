using System.Diagnostics;

namespace RussJudge.AutoApplicationUpdater
{
    /// <summary>
    /// Class to check for and download and install an update to your application.
    /// </summary>
    public class UpdateChecker
    {
        private UpdateManifest? RemoteManifestFile;
        /// <summary>
        /// Indicates whether or not the update is required.
        /// </summary>
        public bool IsRequired { get; private set; }

        /// <summary>
        /// Last response from connection to the remote server for checking for update or downloading the installer package.
        /// </summary>
        public RemoteResponse? LastRemoteResponse { get; private set; }

        /// <summary>
        /// Installs the Package installer, decompressing the file first if necessary.
        /// </summary>
        /// <param name="localInstallerPackagePath">The path to the installer package.</param>
        /// <returns>Null if successful, or the error message if not.</returns>
        public static string? Update(string localInstallerPackagePath)
        {

            bool canProceed = true;

            string? retVal = null;
            try
            {
                FileInfo package = new(localInstallerPackagePath);
                if (package.Extension.Equals(UpdateManifest.ZIPExtension, StringComparison.OrdinalIgnoreCase))
                {
                    string? decompressedFile = UpdateManifest.UnzipFile(localInstallerPackagePath);
                    if (string.IsNullOrEmpty(decompressedFile))
                    {
                        retVal = UpdateManifest.LastError;
                    }
                    else
                    {
                        localInstallerPackagePath = decompressedFile;
                    }
                }
                else
                {
                    var decompressedFile = UpdateManifest.Decompress(localInstallerPackagePath);
                    if (string.IsNullOrEmpty(decompressedFile))
                    {
                        retVal = UpdateManifest.LastError;
                    }
                    else
                    {
                        localInstallerPackagePath = decompressedFile;
                    }
                }
                if (canProceed)
                {
                    ProcessStartInfo startInfo = new(localInstallerPackagePath)
                    {
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    retVal = null;
                }
            }
            catch (Exception ex)
            {
                retVal = ex.Message;
            }
            return retVal;

        }
        /// <summary>
        /// Downloads the remote installer package and executes it.
        /// </summary>
        /// <param name="remoteURLForPackage">The direct http url link to the installer package.</param>
        /// <returns>The error message if the process fails, or null if successful.</returns>
        public async Task<string?> UpdateFromRemote(string remoteURLForPackage)
        {
            string? retVal = "Update Failed";
            if (RemoteManifestFile != null)
            {
                LastRemoteResponse = await RemoteManifestFile.GetRemotePackage(remoteURLForPackage);
                if (LastRemoteResponse != null && LastRemoteResponse.IsSuccess)
                {
                    var setupPackage = LastRemoteResponse.InstallerPackageContent;
                    if (setupPackage != null && setupPackage.Length > 0)
                    {
                        if (RemoteManifestFile.PackageIsValid(setupPackage))
                        {
                            try
                            {
                                string packageFile = Path.Combine(Path.GetTempPath(), RemoteManifestFile.FilePackageName);
                                using (FileStream fs = new(packageFile, FileMode.Create))
                                {
                                    fs.Write(setupPackage, 0, setupPackage.Length);
                                }
                                retVal = Update(packageFile);
                            }
                            catch (Exception ex)
                            {
                                retVal = LastRemoteResponse == null || string.IsNullOrEmpty(LastRemoteResponse.ResponseMessage) ?
                                    ex.Message : LastRemoteResponse.ResponseMessage + "-" + ex.Message;
                            }
                        }
                        else
                        {
                            retVal = LastRemoteResponse == null || string.IsNullOrEmpty(LastRemoteResponse.ResponseMessage) ?
                                "Setup package Checksum is invalid." : LastRemoteResponse.ResponseMessage + "- Setup package Checksum is invalid.";
                        }
                    }
                    else
                    {
                        retVal = LastRemoteResponse == null || string.IsNullOrEmpty(LastRemoteResponse.ResponseMessage) ?
                            "Setup package is null or empty." : LastRemoteResponse.ResponseMessage + "- Setup package is null or empty.";
                    }
                }
                else
                {
                    retVal = LastRemoteResponse == null || string.IsNullOrEmpty(LastRemoteResponse.ResponseMessage) ?
                        "Failed to update" : LastRemoteResponse.ResponseMessage;
                }
            }
            return retVal;
        }
        /// <summary>
        /// Downloads the remote installer package and executes it.
        /// </summary>
        /// <returns>The error message if the process fails, or null if successful.</returns>
        public Task<string?> UpdateFromRemote()
        {
            if (RemoteManifestFile == null)
            {
                return new Task<string?>(() => { return "Unable to update--Remote Update Manifest is not loaded."; });
            }
            else
            {
                return UpdateFromRemote(RemoteManifestFile.RemoteURLSourcePackage);
            }
        }
        /// <summary>
        /// Checks if an update is available.
        /// </summary>
        /// <param name="AssemblyForVersionCheck">Generally the entry assembly, or whatever assembly is used for version-checking.</param>
        /// <param name="URL">The remote URL to the UpdateManifest file.</param>
        /// <returns>True if there is an update available.</returns>
        public Task<bool> CheckRemote(System.Reflection.Assembly AssemblyForVersionCheck, string URL)
        {
            return CheckRemote(AssemblyForVersionCheck.Location, URL);
        }
        /// <summary>
        /// Checks if an update is available.
        /// </summary>
        /// <param name="AssemblyForVersionCheckLocation">Generally the entry assembly, or whatever assembly is used for version-checking.</param>
        /// <param name="URL">The remote URL to the UpdateManifest file.</param>
        /// <returns>True if there is an update available.</returns>
        public async Task<bool> CheckRemote(string AssemblyForVersionCheckLocation, string URL)
        {
            LastRemoteResponse = await UpdateManifest.GetRemoteManifestFile(URL);
            if (LastRemoteResponse != null && LastRemoteResponse.IsSuccess)
            {
                RemoteManifestFile = LastRemoteResponse.ManifestFile;
            }
            if (RemoteManifestFile != null)
            {
                IsRequired = RemoteManifestFile.IsRequired;
                return RemoteManifestFile.NeedsUpdated(AssemblyForVersionCheckLocation);
            }
            else
            {
                return false;
            }
        }
    }
}

