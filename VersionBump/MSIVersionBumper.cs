namespace VersionBump
{
    public class MSIVersionBumper(string projectFile)
    {
        const string productVersionMatch = "\"ProductVersion\"";
        const string productCodeMatch = "\"ProductCode\"";
        const string packageCodeMatch = "\"PackageCode\"";
        const string productMatch = "\"Product\"";
        string ProjectData = string.Empty;
        public string ProjectFile { get; private set; } = projectFile;
        private int NewVersion;
        private void IncrementProductVersion()
        {
            int position = ProjectData.IndexOf(productVersionMatch) + productVersionMatch.Length;
            if (position < ProjectData.Length)
            {
                position = ProjectData.IndexOf(':', position) + 1;
                if (position < ProjectData.Length)
                {
                    position = ProjectData.IndexOf('"', position);
                    int endPosition = position;
                    while (ProjectData[position] != '.')
                    {
                        position--;
                    }
                    position++;
                    string oldVer = ProjectData[position..endPosition];
                    if (int.TryParse(oldVer, out int ver))
                    {
                        ver++;
                        NewVersion = ver;
                        ProjectData = string.Concat(ProjectData.AsSpan(0, position), ver.ToString(), ProjectData.AsSpan(endPosition));

                    }
                }
            }
        }
        private void ChangeGuidCode(string key)
        {
            string newGuid = Guid.NewGuid().ToString().ToUpperInvariant();

            int position = ProjectData.IndexOf(productMatch);
            position = ProjectData.IndexOf(key, position);
            position = ProjectData.IndexOf('{', position) + 1;
            int endPosition = ProjectData.IndexOf('}', position);
            ProjectData = string.Concat(ProjectData.AsSpan(0, position), newGuid, ProjectData.AsSpan(endPosition));
        }
        public void Process()
        {
            using (StreamReader sr = new(ProjectFile))
            {
                ProjectData = sr.ReadToEnd();
            }

            IncrementProductVersion();
            ChangeGuidCode(productCodeMatch);
            ChangeGuidCode(packageCodeMatch);

            using StreamWriter sw = new(ProjectFile, false);
            sw.Write(ProjectData);

            FileInfo f = new(ProjectFile);
            Processor.WriteStarLine();
            Processor.WriteLine("VersionBump--updating Package version");
            Processor.WriteLine($"Project: {f.Name}");
            Processor.WriteLine($"Package Version updated to {NewVersion}");
            Processor.WriteStarLine();
        }
    }
}
