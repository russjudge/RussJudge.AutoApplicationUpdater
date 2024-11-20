using RussJudge.AutoApplicationUpdater;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Example
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        //Hard-code the URL that contains the data for verison information and self-update.
        const string remoteUpdateMainfestURL = "https://raw.githubusercontent.com/russjudge/RussJudge.AutoApplicationUpdater/refs/heads/master/SampleDeployment/Example.json";
        public MainWindow()
        {
            InitializeComponent();

            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (ver != null)
            {
                AssemblyVersion = ver.ToString();
            }
            else
            {
                AssemblyVersion = "0.0.0.0";
            }
            DataContext = this;

        }
        public string AssemblyVersion { get; set; }
        private string _remoteVersion = string.Empty;
        public string RemoteVersion
        {
            get { return _remoteVersion; }
            set
            {
                if (_remoteVersion != value)
                {
                    _remoteVersion = value;
                    DoPropertyChanged();
                }
            }
        }


        private string _RemotePackageURL = string.Empty;
        public string RemotePackageURL
        {
            get { return _RemotePackageURL; }
            set
            {
                if (_RemotePackageURL != value)
                {
                    _RemotePackageURL = value;
                    DoPropertyChanged();
                }
            }
        }



        private string _packageName = string.Empty;
        public string PackageName
        {
            get { return _packageName; }
            set
            {
                if (_packageName != value)
                {
                    _packageName = value;
                    DoPropertyChanged();
                }
            }
        }

        private string _PackageCheckSum = string.Empty;
        public string PackageCheckSum
        {
            get { return _PackageCheckSum; }
            set
            {
                if (_PackageCheckSum != value)
                {
                    _PackageCheckSum = value;
                    DoPropertyChanged();
                }
            }
        }

        private long _PackageSize;
        public long PackageSize
        {
            get { return _PackageSize; }
            set
            {
                if (_PackageSize != value)
                {
                    _PackageSize = value;
                    DoPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void DoPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        private async Task CheckForUpdate()
        {
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
                    DoUpdate = MessageBox.Show("An update is available.  Do you wish to update?\r\n(Please don't--this is just an example for how to code this, but it will work)", "Example", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
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
        //Non-http method for checking for update
        public static bool NonHTTPCheckForUpdate(string localPathToRemoteUpdateManifestFile)
        {
            //This code assumes that the remote UpdateManifest file has already been downloaded
            //and can be found in localPathToRemoteUpdateManifestFile.

            var remoteManifest = UpdateManifest.GetManifestFile(localPathToRemoteUpdateManifestFile);
            return remoteManifest.NeedsUpdated(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //The remote location will be stored in remoteManifest.RemoteURLSourcePackage--you will need to write your own code for getting
            //  this package based on the mechanism used.  If using http or https, see the CheckForUpdate method.

        }

        public static bool NonHTTPValidateInstallerPackage(string localPathToRemoteUpdateManifestFile, string pathToDownloadedInstallerPackage)
        {
            var remoteManifest = UpdateManifest.GetManifestFile(localPathToRemoteUpdateManifestFile);
            return remoteManifest.PackageIsValid(pathToDownloadedInstallerPackage);
        }

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
                this.Close();
                Environment.Exit(0);
            }
        }
        private async void GetRemoteManifestInfo()
        {
            var response = await UpdateManifest.GetRemoteManifestFile(remoteUpdateMainfestURL);
            if (response != null && response.IsSuccess)
            {
                if (response.ManifestFile != null)
                {
                    RemoteVersion = response.ManifestFile.Version;
                    PackageCheckSum = response.ManifestFile.FilePackageChecksum;
                    PackageSize = response.ManifestFile.FilePackageSize;
                    PackageName = response.ManifestFile.FilePackageName;
                    RemotePackageURL = response.ManifestFile.RemoteURLSourcePackage;

                }
                else
                {
                    MessageBox.Show("UpdateManifest file was not returned");
                }
            }
            else if (response != null)
            {
                MessageBox.Show(response.ResponseMessage);
            }
            else
            {
                MessageBox.Show("There was some kind of severe error--nothing was returned from the check.");
            }
        }
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await CheckForUpdate();
        }

        private async void OnUpdateCheck(object sender, RoutedEventArgs e)
        {
            await CheckForUpdate();
        }

        private void OnGetRemoteManifest(object sender, RoutedEventArgs e)
        {
            GetRemoteManifestInfo();
        }
        const string LocalSampleDeploymentPath = @"..\..\..\..\SampleDeployment";
        private void OnNonHTTPUpdateCheck(object sender, RoutedEventArgs e)
        {
            if (NonHTTPCheckForUpdate(System.IO.Path.Combine(LocalSampleDeploymentPath, "Example.json")))
            {
                MessageBox.Show("A newer version was detected");
            }
            else
            {
                MessageBox.Show("No update detected");
            }
        }

        private void OnNonHTTPValidatePackageChecksum(object sender, RoutedEventArgs e)
        {
            if (NonHTTPValidateInstallerPackage(
                System.IO.Path.Combine(LocalSampleDeploymentPath, "Example.json"),
                System.IO.Path.Combine(LocalSampleDeploymentPath, "ExampleSetup.zip")))
            {
                MessageBox.Show("The Checksum of the installer package is valid");
            }
            else
            {
                var realChecksum = UpdateManifest.GetFileChecksum(System.IO.Path.Combine(LocalSampleDeploymentPath, "ExampleSetup.zip"));
                MessageBox.Show($"The Checksum of the installer package was NOT valid.\r\nThe Checksum is: {realChecksum}");
            }
        }

        private void OnInstallLocalPackage(object sender, RoutedEventArgs e)
        {
            RunInstallProcess(System.IO.Path.Combine(LocalSampleDeploymentPath, "ExampleSetup.zip"));
        }

        //By design, this will ALWAYS detect that an update is available, because the remote update manifest file is intentionally
        // set with the wrong version number, so that this update process can be fully demonstrated.
    }
}