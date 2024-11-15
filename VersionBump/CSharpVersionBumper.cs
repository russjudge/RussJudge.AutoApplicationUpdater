namespace VersionBump
{
    public class CSharpVersionBumper(string projectFile)
    {
        public string ProjectFile { get; private set; } = projectFile;
        public string ProjectData { get; private set; } = string.Empty;

        private const string VersionMatch1 = "<FileVersion>";
        private const string VersionMatch2 = "<AssemblyVersion>";
        private const string Match3 = "</PropertyGroup>";
        private string? newFileVersion;
        private string? newAssemblyVersion;


        public void Process()
        {
            using (StreamReader sr = new(ProjectFile))
            {
                ProjectData = sr.ReadToEnd();
            }

            UpdateMatch(VersionMatch1, out newFileVersion);
            UpdateMatch(VersionMatch2, out newAssemblyVersion);

            using (StreamWriter sw = new(ProjectFile))
            {
                sw.Write(ProjectData);
            }
            FileInfo f = new(ProjectFile);
            Processor.WriteStarLine();
            Processor.WriteLine($"Project: {f.Name}");
            Processor.WriteLine($"Project FileVersion update to    {newFileVersion}");
            Processor.WriteLine($"Project AsemblyVersion update to {newAssemblyVersion}");
            Processor.WriteStarLine();
        }

        private void UpdateMatch(string match, out string theNewVersion)
        {

            theNewVersion = string.Empty;
            int i = ProjectData.IndexOf(match) + match.Length;
            if (i >= match.Length)
            {
                int j = ProjectData.IndexOf('<', i);
                string version = ProjectData[i..j];
                string[] parts = version.Split('.');
                if (parts.Length > 3)
                {
                    int k = int.Parse(parts[3]);
                    k++;
                    string newVersion = $"{parts[0]}.{parts[1]}.{parts[2]}.{k}";
                    theNewVersion = newVersion;
                    ProjectData = string.Concat(ProjectData.AsSpan(0, i), newVersion, ProjectData.AsSpan(j));
                }
                else if (parts.Length <= 3)
                {
                    string newVersion = $"{parts[0]}.{parts[1]}.{parts[2]}.1";
                    theNewVersion = newVersion;
                    ProjectData = string.Concat(ProjectData.AsSpan(0, i), newVersion, ProjectData.AsSpan(j));
                }
                else
                {
                    InsertMissingVersionMatch(match);
                }
            }
            else
            {
                InsertMissingVersionMatch(match);
            }
        }


        private void InsertMissingVersionMatch(string match)
        {
            int i = ProjectData.IndexOf(Match3, StringComparison.InvariantCultureIgnoreCase);
            ProjectData = $"{ProjectData[..i]}  {match}1.0.0.0</{match[1..]}\r\n  {ProjectData[i..]}";
        }
    }
}
