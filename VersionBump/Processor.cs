namespace VersionBump
{
    internal class Processor
    {

        public static bool IsMSISetupPackage { get; set; } = false;
        public static bool IsCSharpProject { get; set; } = false;


        public const int ConsoleLineLength = 80;
        public const string PrefixSuffix = "***";
        public const int PrefixLength = 3;

        public const int ConsoleLineUsableLength = ConsoleLineLength - (PrefixLength * 2);

        public static void WriteStarLine()
        {
            Console.WriteLine("*".PadLeft(ConsoleLineLength, '*'));
        }
        public static void WriteLine(string message)
        {
            int pad1Length = (ConsoleLineUsableLength - message.Length) / 2;
            if (pad1Length <= 0)
            {
                pad1Length = 1;
            }

            int pad2Length = ConsoleLineUsableLength - pad1Length - message.Length;
            if (pad2Length <= 0)
            {
                pad2Length = 1;
            }
            string l1Final = "".PadLeft(pad1Length, ' ') + message + "".PadRight(pad2Length, ' ');
            if (l1Final.Length < ConsoleLineUsableLength)
            {
                l1Final += ' ';
            }
            Console.WriteLine($"{PrefixSuffix}{l1Final}{PrefixSuffix}");

        }
        /// <summary>
        /// Reads a project file so that it can be written back by <see cref="WriteProjectFile"/> without altering
        /// anything other than what was deliberately changed.
        /// </summary>
        /// <param name="projectFile">The path to the project file.</param>
        /// <param name="encoding">The encoding to write the project file back with.</param>
        /// <returns>The content of the project file.</returns>
        public static string ReadProjectFile(string projectFile, out System.Text.Encoding encoding)
        {
            //A file with a byte order mark is read (and written back) in the encoding it declares.  A file without one
            //is read as Latin1, which maps every byte to exactly one character.  Only ASCII text is searched for and
            //replaced, so whatever the file's real encoding is (UTF-8, or the ANSI code page that Setup projects are
            //saved in), every other byte is written back untouched.
            using StreamReader sr = new(projectFile, System.Text.Encoding.Latin1, true);
            string retVal = sr.ReadToEnd();
            encoding = sr.CurrentEncoding;
            return retVal;
        }

        public static void WriteProjectFile(string projectFile, string projectData, System.Text.Encoding encoding)
        {
            File.WriteAllText(projectFile, projectData, encoding);
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
