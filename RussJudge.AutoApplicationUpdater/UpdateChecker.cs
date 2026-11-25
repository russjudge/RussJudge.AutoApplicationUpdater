using System.Diagnostics;

namespace RussJudge.AutoApplicationUpdater
{
    /// <summary>
    /// Class to check for and download and install an update to your application.
    /// </summary>
    public class UpdateChecker
    {
        /// <summary>
        /// Remote UpdateManifest file
        /// </summary>
        public UpdateManifest? RemoteManifestFile { get; private set; }
        /// <summary>
        /// Indicates whether or not the update is required.
        /// </summary>
        public bool IsRequired { get; private set; }

        /// <summary>
        /// Last response from connection to the remote server for checking for update or downloading the installer package.
        /// </summary>
        public RemoteResponse? LastRemoteResponse { get; private set; }

        private const string DecompressFailedMessage = "Unable to decompress the installer package.";

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
                        retVal = UpdateManifest.LastError ?? DecompressFailedMessage;
                        canProceed = false;
                    }
                    else
                    {
                        localInstallerPackagePath = decompressedFile;
                    }
                }
                else if (UpdateManifest.CompressionTypes.ContainsKey(package.Extension))
                {
                    var decompressedFile = UpdateManifest.Decompress(localInstallerPackagePath);
                    if (string.IsNullOrEmpty(decompressedFile))
                    {
                        retVal = UpdateManifest.LastError ?? DecompressFailedMessage;
                        canProceed = false;
                    }
                    else
                    {
                        localInstallerPackagePath = decompressedFile;
                    }
                }
                else
                {

                }
                if (canProceed)
                {
                    ProcessStartInfo startInfo = new(localInstallerPackagePath)
                    {
                        UseShellExecute = true
                    };
                    //Disposing only releases this application's handle to the installer--the installer keeps running.
                    using Process? installer = Process.Start(startInfo);
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
        /// <param name="fallbackURL">Fallback URL to use if the first one fails.</param>
        /// <returns>The error message if the process fails, or null if successful.</returns>
        public async Task<string?> UpdateFromRemote(string remoteURLForPackage, string fallbackURL = "")
        {
            string? retVal = "Update Failed";
            if (RemoteManifestFile != null)
            {
                //ConfigureAwait(false) keeps the work that follows the download (verifying, saving, and decompressing
                //the package) off of the caller's UI thread.
                string packageURL = remoteURLForPackage;
                LastRemoteResponse = await RemoteManifestFile.GetRemotePackage(packageURL).ConfigureAwait(false);
                if ((LastRemoteResponse == null || !LastRemoteResponse.IsSuccess) && !string.IsNullOrEmpty(fallbackURL))
                {
                    packageURL = fallbackURL;
                    LastRemoteResponse = await RemoteManifestFile.GetRemotePackage(packageURL).ConfigureAwait(false);
                }
                if (LastRemoteResponse != null && LastRemoteResponse.IsSuccess)
                {
                    var setupPackage = LastRemoteResponse.InstallerPackageContent;
                    if (setupPackage != null && setupPackage.Length > 0)
                    {
                        //The download already compared the package against the manifest's checksum when one was provided.
                        if (LastRemoteResponse.PackageChecksumVerified || RemoteManifestFile.PackageIsValid(setupPackage))
                        {
                            try
                            {
                                string packageFile = Path.Combine(Path.GetTempPath(), GetPackageFileName(RemoteManifestFile, packageURL));
                                await File.WriteAllBytesAsync(packageFile, setupPackage).ConfigureAwait(false);
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
        /// Determines the name to save the downloaded installer package as.
        /// </summary>
        private static string GetPackageFileName(UpdateManifest manifest, string packageURL)
        {
            //Only the name is taken: the manifest comes from the remote, and must not be able to steer the package to a location outside of the temp folder.
            string retVal = Path.GetFileName(manifest.FilePackageName);
            if (string.IsNullOrEmpty(retVal))
            {
                //The manifest did not name the package, so use the name it was downloaded as.
                string urlPath = Uri.TryCreate(packageURL, UriKind.Absolute, out Uri? uri) ? Uri.UnescapeDataString(uri.AbsolutePath) : packageURL;
                retVal = Path.GetFileName(urlPath);
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
                //This must be a completed task: a task created with "new Task" is never started, so awaiting it would never return.
                return Task.FromResult<string?>("Unable to update--Remote Update Manifest is not loaded.");
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
        /// <param name="fallbackURL">An optional URL to use for failover.</param>
        /// <returns>True if there is an update available.</returns>
        public Task<bool> CheckRemote(System.Reflection.Assembly AssemblyForVersionCheck, string URL, string fallbackURL = "")
        {
            string location = AssemblyForVersionCheck.Location;
            if (string.IsNullOrEmpty(location))
            {
                //An assembly bundled into a single-file application has no location.  Its version must be read from the
                //assembly itself, otherwise it is seen as version 0.0.0.0 and an update is reported on every check.
                VersionInfo localVersion = new(AssemblyForVersionCheck);
                return CheckRemote(manifest => manifest.NeedsUpdated(localVersion), URL, fallbackURL);
            }
            return CheckRemote(location, URL, fallbackURL);
        }
        /// <summary>
        /// Checks if an update is available.
        /// </summary>
        /// <param name="AssemblyForVersionCheckLocation">Generally the entry assembly, or whatever assembly is used for version-checking.</param>
        /// <param name="URL">The remote URL to the UpdateManifest file.</param>
        /// <param name="fallbackURL">An optional URL to use for failover.</param>
        /// <returns>True if there is an update available.</returns>
        public Task<bool> CheckRemote(string AssemblyForVersionCheckLocation, string URL, string fallbackURL = "")
        {
            return CheckRemote(manifest => manifest.NeedsUpdated(AssemblyForVersionCheckLocation), URL, fallbackURL);
        }

        private async Task<bool> CheckRemote(Func<UpdateManifest, bool> needsUpdated, string URL, string fallbackURL)
        {
            LastRemoteResponse = await UpdateManifest.GetRemoteManifestFile(URL).ConfigureAwait(false);
            if ((LastRemoteResponse == null || !LastRemoteResponse.IsSuccess) && !string.IsNullOrEmpty(fallbackURL))
            {
                LastRemoteResponse = await UpdateManifest.GetRemoteManifestFile(fallbackURL).ConfigureAwait(false);
            }
            if (LastRemoteResponse != null && LastRemoteResponse.IsSuccess)
            {
                RemoteManifestFile = LastRemoteResponse.ManifestFile;
            }

            if (RemoteManifestFile != null)
            {
                IsRequired = RemoteManifestFile.IsRequired;
                return needsUpdated(RemoteManifestFile);
            }
            else
            {
                return false;
            }
        }
    }
}

