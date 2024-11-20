using System.Windows;

namespace Example
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Hard-code the URL that contains the data for verison information and self-update.
        const string remoteUpdateMainfestURL = "https://github.com/russjudge/RussJudge.AutoApplicationUpdater/blob/master/SampleDeployment/Example.json";
        public MainWindow()
        {
            InitializeComponent();
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
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await CheckForUpdate();
        }
        //By design, this will ALWAYS detect that an update is available, because the remote update manifest file is intentionally
        // set with the wrong version number, so that this update process can be fully demonstrated.
    }
}