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

        /// <summary>
        /// Finds text that the project file is required to have.  Failing here, before anything has been written,
        /// keeps a project file that is not laid out as expected from being modified in the wrong place.
        /// </summary>
        private int Find(string value, int startIndex, string description)
        {
            int retVal = ProjectData.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (retVal < 0)
            {
                throw new InvalidDataException($"{description} was not found in the project file.  The project file was not changed.");
            }
            return retVal;
        }
        private void IncrementProductVersion()
        {
            //The line being updated looks like:  "ProductVersion" = "8:1.0.16"
            int position = Find(productVersionMatch, 0, productVersionMatch) + productVersionMatch.Length;
            int startPosition = Find(":", position, $"The value of {productVersionMatch}") + 1;
            int endPosition = Find("\"", startPosition, $"The end of the value of {productVersionMatch}");

            //Only the last node of the version is incremented.
            position = ProjectData.LastIndexOf('.', endPosition - 1, endPosition - startPosition) + 1;
            if (position > 0)
            {
                string oldVer = ProjectData[position..endPosition];
                if (int.TryParse(oldVer, out int ver))
                {
                    ver++;
                    NewVersion = ver;
                    ProjectData = string.Concat(ProjectData.AsSpan(0, position), ver.ToString(), ProjectData.AsSpan(endPosition));

                }
            }
        }
        private void ChangeGuidCode(string key)
        {
            string newGuid = Guid.NewGuid().ToString().ToUpperInvariant();

            int position = Find(productMatch, 0, $"The {productMatch} section");
            position = Find(key, position, key);
            position = Find("{", position, $"The value of {key}") + 1;
            int endPosition = Find("}", position, $"The end of the value of {key}");
            ProjectData = string.Concat(ProjectData.AsSpan(0, position), newGuid, ProjectData.AsSpan(endPosition));
        }
        public void Process()
        {
            ProjectData = Processor.ReadProjectFile(ProjectFile, out System.Text.Encoding encoding);

            IncrementProductVersion();
            ChangeGuidCode(productCodeMatch);
            ChangeGuidCode(packageCodeMatch);

            Processor.WriteProjectFile(ProjectFile, ProjectData, encoding);

            FileInfo f = new(ProjectFile);
            Processor.WriteStarLine();
            Processor.WriteLine("VersionBump--updating Package version");
            Processor.WriteLine($"Project: {f.Name}");
            Processor.WriteLine($"Package Version updated to {NewVersion}");
            Processor.WriteStarLine();
        }
    }
}
