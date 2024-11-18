namespace VersionBump
{
    internal class Processor
    {

        public static bool IsMSISetupPackage { get; set; } = false;
        public static bool IsCSharpProject { get; set; } = false;
        public const int ConsoleLineLength = 60;
        public static void WriteStarLine()
        {
            Console.WriteLine("*".PadLeft(ConsoleLineLength, '*'));
        }
        public static void WriteLine(string message)
        {

            const string prefixStar = "***";
            int prefixLength = prefixStar.Length * 2;

            int pad1Length = (ConsoleLineLength - prefixLength - message.Length) / 2;
            int pad2Length = ConsoleLineLength - prefixLength - pad1Length - message.Length;

            string l1Final = "".PadLeft(pad1Length, ' ') + message + "".PadRight(pad2Length, ' ');


            if (l1Final.Length < ConsoleLineLength - prefixLength)
            {
                l1Final += ' ';
            }
            Console.WriteLine($"{prefixStar}{l1Final}{prefixStar}");

        }
        public static void Process(string[] args)
        {
            string projectFile = string.Empty;
            if (args.Length > 0)
            {
                projectFile = args[^1];
                IsMSISetupPackage = projectFile.EndsWith(".vdproj", StringComparison.InvariantCultureIgnoreCase);
                IsCSharpProject = projectFile.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase);
            }

            if (IsMSISetupPackage)
            {
                MSIVersionBumper processor = new(projectFile);
                processor.Process();
            }
            else if (IsCSharpProject)
            {
                CSharpVersionBumper processor = new(projectFile);
                processor.Process();
            }
            else
            {
                WriteStarLine();
                WriteLine("Project file to update must be included on the command line.");
                WriteLine("Syntax:");
                WriteLine("VersionBump projectfile");
                WriteLine("Supports both .csproj (c# project) and .vdproj (Microsoft Setup Installer .msi project) files");
                WriteLine("Please note that if this is run as part of a pre-compile event, the compile will only use the previous version.");
                WriteStarLine();

            }
        }
    }
}
