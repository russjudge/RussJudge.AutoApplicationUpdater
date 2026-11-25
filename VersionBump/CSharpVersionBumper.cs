namespace VersionBump
{
    public class CSharpVersionBumper(string projectFile)
    {
        public string ProjectFile { get; private set; } = projectFile;
        public string ProjectData { get; private set; } = string.Empty;

        private const string VersionMatch1 = "<FileVersion>";
        private const string VersionMatch2 = "<AssemblyVersion>";
        private const string Match3 = "</PropertyGroup>";
        private const string InitialVersion = "1.0.0.0";


        public void Process()
        {
            ProjectData = Processor.ReadProjectFile(ProjectFile, out System.Text.Encoding encoding);

            UpdateMatch(VersionMatch1, out string? newFileVersion);
            UpdateMatch(VersionMatch2, out string? newAssemblyVersion);

            Processor.WriteProjectFile(ProjectFile, ProjectData, encoding);

            FileInfo f = new(ProjectFile);
            Processor.WriteStarLine();
            Processor.WriteLine("VersionBump--updating File and Project versions");
            Processor.WriteLine($"Project: {f.Name}");
            Processor.WriteLine($"Project FileVersion update to    {newFileVersion}");
            Processor.WriteLine($"Project AsemblyVersion update to {newAssemblyVersion}");
            Processor.WriteStarLine();
        }

        private void UpdateMatch(string match, out string theNewVersion)
        {
            int i = ProjectData.IndexOf(match, StringComparison.Ordinal);
            int j = i < 0 ? -1 : ProjectData.IndexOf('<', i + match.Length);
            if (j >= 0)
            {
                i += match.Length;
                string version = ProjectData[i..j];
                string[] parts = version.Split('.');
                string newVersion;
                if (parts.Length > 3)
                {
                    int k = int.Parse(parts[3]);
                    k++;
                    newVersion = $"{parts[0]}.{parts[1]}.{parts[2]}.{k}";
                }
                else if (string.IsNullOrWhiteSpace(version))
                {
                    newVersion = InitialVersion;
                }
                else
                {
                    //The version has fewer than four nodes ("1.0"), so the missing nodes are filled in.
                    string[] nodes = [.. parts, "0", "0"];
                    newVersion = $"{nodes[0]}.{nodes[1]}.{nodes[2]}.1";
                }
                theNewVersion = newVersion;
                ProjectData = string.Concat(ProjectData.AsSpan(0, i), newVersion, ProjectData.AsSpan(j));
            }
            else
            {
                InsertMissingVersionMatch(match, out theNewVersion);
            }
        }


        private void InsertMissingVersionMatch(string match, out string theNewVersion)
        {
            int i = ProjectData.IndexOf(Match3, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
            {
                throw new InvalidDataException($"Unable to add {match} to the project file, because it has no {Match3} to add it to.");
            }
            theNewVersion = InitialVersion;
            ProjectData = $"{ProjectData[..i]}  {match}{theNewVersion}</{match[1..]}\r\n  {ProjectData[i..]}";
        }
    }
}
