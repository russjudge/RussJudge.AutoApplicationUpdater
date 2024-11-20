using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RussJudge.AutoApplicationUpdater
{
    /// <summary>
    /// Holds data for the response for connecting to the remote server for update.
    /// </summary>
    public class RemoteResponse
    {
        internal RemoteResponse(System.Net.HttpStatusCode statusCode, string? reasonPhrase, HttpRequestMessage? requestMessage, bool isSuccess, byte[]? content = null)
        {
            InstallerPackageContent = content;
            ResponseMessage = reasonPhrase;
            StatusCode = statusCode;
            RequestMessage = requestMessage;
            IsSuccess = isSuccess;
        }
        internal RemoteResponse(System.Net.HttpStatusCode statusCode, string? reasonPhrase, HttpRequestMessage? requestMessage, bool isSuccess, string updateManifestContent)
        {
            ResponseMessage = reasonPhrase;
            StatusCode = statusCode;
            RequestMessage = requestMessage;
            IsSuccess = isSuccess;
            ManifestFile = new(updateManifestContent);

        }

        /// <summary>
        /// The Install Package.  Save to a file and run to install the package.
        /// </summary>
        public byte[]? InstallerPackageContent { get; private set; }
        /// <summary>
        /// The Update manifest file.
        /// </summary>
        public UpdateManifest? ManifestFile { get; private set; }

        /// <summary>
        /// The remote response from attempting to download either the UpdateManifest or installer package.
        /// Checking this will be useful for troubleshooting problems.
        /// </summary>
        public string? ResponseMessage { get; private set; }
        /// <summary>
        /// The status code returned from either checking for an update or downloading the installer package.
        /// </summary>
        public System.Net.HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// The original http request for downloading the package installer or checking for update.
        /// </summary>
        public HttpRequestMessage? RequestMessage { get; private set; }
        /// <summary>
        /// Flag that indicates that the check for update or the download of the installer package was successful.
        /// </summary>
        public bool IsSuccess { get; internal set; }
    }
}
