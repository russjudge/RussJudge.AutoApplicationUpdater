# RussJudge.AutoApplicationUpdater

Over the years I have written applications that needed a mechanism for having the application automatically check if an update is needed
and apply that update.  Of the mechanisms employed, there was always a significant manual effort in setting up the data so that the
self-udpate would work properly.

As a result, I have created this project that automatically ticks up version information on a development project and builds the data
files needed for self-update checking.  This project may not be for everyone--particularly on large team projects.

For an example how to use this, see the example project at https://github.com/russjudge/RussJudge.AutoApplicationUpdater.  Detailed discussion is included
in the main project.  The example project also demonstrates an example of how to even automatically deploy updates using Github, though this method should
not be used in large team projects.

## Adding the update check to your application.
At whatever the point in your application you want to check for an update (usually at startup), use the UpdateChecker class.  Simply
instantiate the class, then call the CheckRemote method, passing the Assembly that has the FileVersion set for version tracking to determine
if an update is available, along with the URL to the file containing the update information (a JSON serialized UpdateManifest object).
If CheckRemote returns True, then an update is available.  At this point, the property IsRequired on the UpdateChecker object will indicate
whether or not the update is required (though this can mean anything the developer desires).  To start the update, call UpdateFromRemote method
on the UpdateChecker object, exiting the application when the method returns with an empty string (indicating no error) so that the installer 
package can execute.

Below is sample code to add to an application for self-update:


```
private async Task CheckForUpdate()
{
    //Hard-code the URL that contains the data for verison information and self-update.
    string remoteUpdateMainfestURL = "https://github.com/russjudge/RussJudge.AutoApplicationUpdater/blob/master/SampleDeployment/Example.json";
    RussJudge.AutoApplicationUpdater.UpdateChecker checker = new();
    var assm = System.Reflection.Assembly.GetExecutingAssembly();
    if (await checker.CheckRemote(assm, remoteUpdateMainfestURL))
    {
        bool DoUpdate;
        if (checker.IsRequired)
        {
            DoUpdate = true;
            MessageBox.Show("A required update was detected.  Downloading and installing...");
        }
        else
        {
            DoUpdate = MessageBox.Show("An update is available.  Do you wish to update?", "Example", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        }
        if (DoUpdate)
        {
            string? errorMessage = await checker.UpdateFromRemote();
            if (string.IsNullOrEmpty(errorMessage))
            {
                this.Close();
                Environment.Exit(0);
            }
            else
            {
                MessageBox.Show(errorMessage);
            }
         }
     }
}
```

It should be noted that the remote UpdateManifest must have a higher version to be indicative of an update.  If the version is the same or lower,
then it will not be considered an update.

If an update is detected, the file checksum is verified before installing the update.

This automatic update check only works when the remote is over http or https.  Using a different mechanism is only possibly by working with the
UpdateManifest class, and not the UpdateChecker class.

## Performing an update check over something other than http or https

The UpdateChecker class only works over http or https, therefore to update over ftp or some other source, you will need to create the code to download
the UpdateManifest file and the installer package.  You can then use the UpdateManifest class to compare the version of your assembly versus the
remote version and determine if an update is available.

Below is sample code for performing this:
```
//Non-http method for checking for update
public bool CheckForUpdate(string localPathToRemoteUpdateManifestFile)
{
    //This code assumes that the remote UpdateManifest file has already been downloaded and can be found in localPathToRemoteUpdateManifestFile.

    var remoteManifest = UpdateManifest.GetManifestFile(localPathToRemoteUpdateManifestFile);
    return remoteManifest.NeedsUpdated(System.Reflection.Assembly.GetExecutingAssembly().Location);
    //The remote location will be stored in remoteManifest.RemoteURLSourcePackage.
}
```

You can also validate the Checksum of the downloaded package by comparing the checksum in the UpdateManifest file against the checksum
the UpdateManifest class generates on the local file.  Below is sample code for validating that the installer package was downloaded
correctly or has not been maliciously replaced:

```
public bool ValidateInstallerPackage(string localPathToRemoteUpdateManifestFile, string pathToDownloadedInstallerPackage)
{
    var remoteManifest = UpdateManifest.GetManifestFile(localPathToRemoteUpdateManifestFile);
    return remoteManifest.PackageIsValid(pathToDownloadedInstallerPackage);
}

```

Finally, you can process the update by running the Update method of the UpdateChecker class:

```
public void RunInstallProcess(string installPath)
{
    var result = UpdateChecker.Update(installPath);
    if (result != null)
    {
        MessageBox.Show($"The update failed:\r\n{result}");
    }
    else
    {
        MessageBox.Show("The installer successfully started.  Press \"OK\" to exit.");
        Environment.Exit(0);
    }
}
```

## Creating the UpdateManifest file.

The UpdateManifest file needed to check if an update is available is a simple JSON file.
Included in the lib folder upon adding a Nuget reference to RussJudge.AutoApplicationUpdater
is the application "ManifestBuilder", which is a tool for automatically generating this UpdateManifest file.

The syntax for using ManifestBuilder is:

`ManifestBuilder -assembly <EXE or DLL to get version data from> -out <Output Path> -remoteurl <remoteURL> -installer <installer Package Path> -required <True/false>`

or, alternatively, a configuration can be passed:

`ManifestBuilder -config <path to parameter file>`

The parameters are as follows:

### -assembly
The DLL or EXE that has Version data set on.

### -out
The output file to generate. If only the path is specified, then the output name generated into the path will be the executable name,
with a .json extension. Be sure to end the output path with backslash (\\) to indicate the path and use the default filename.

### -remoteurl
The URL where the package installer can be downloaded from.


### -installer
The local path to the installer package. this is required for generating the checksums to validate the the download of the installer package.

### -required
Optional parameter that if set indicates that the update is required (and must be applied). Default is False (not required).

### -config 
Full path to a file that contains all the above settings.  This will make all the previous settings optional, but any
settings entered on the command line will override the parameter file. The <parameter file> format is to have each parameter
set on a single line in any order:

`-parameter=value`

So, an example parameter file is:

```
-assembly=C:\MyProject\bin\Debug\net8.0\example.exe
-out=C:\MyProjectSetup\Debug\example.json
-installer=C:\MyProjectSetup\Debug\example.msi
-remoteurl=https://mywebsite.com/software/example.msi
-required=false
```


ManifestBuilder is capable of compressing the .msi package automatically. Compression files that are supported include .zip, .gzip, .zlib, and .br,
all of which are natively supported by .NET 9.0.  To have your installer package compressed appropriately, simply set the -remoteurl parameter
to end with one of those file extentions (.zip, .gzip, .zlib, .br).  The compressed file will be created in the same folder as the .msi package
on the PC.

ManifestBuilder does not automatically deploy the updated package.


## Updating the version of projects
This update process first checks the FileVersion on the tracked assembly, but if this is missing, the ProductVersion will be used.
You can manually set the version number in the project file of your project, or on the Build Event, run VersionBump that is located
in the lib folder upon referencing RussJudge.AutoApplicationUpdater from Nuget.

To use VersionBump, the only parameter is the path to the project file.  VersionBump currently supports only C# project (.csproj)
and Microsoft Setup Projects (.vdproj).  VersionBump will update a Setup Project in such a way as to ensure it works correctly as an upgrade
to a previous install, such that when installed, it will uninstall the older version.  This is done by updating the version and applying new
Guids to the Product Code and the Package Code.  The Upgrade Code does not get affected.

Syntax:

`VersionBump myproject.csproj`


## Adding ManifestBuilder and VersionBump to the Build events
Adding VersionBump to the Build Post Event of your project will increment the fourth node of the version by one.  Since the project is loaded into
memory at the time of compile, the version increase will only reflect on the next build.  Using VersionBump is useful for when your project is
deterministic and the version cannot be set with the wildcard (*).  VersionBump can be added to either the PreBuild or PostBuild event with the
result being the same either way.

Add ManifestBuilder to the Post Build event of the Setup project (.vdproj) with appropriate parameters.  This will result in the update manifest file
being added after the setup package is built, along with creating a compressed file (if desired) of the setup package in that same folder.