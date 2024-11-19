using RussJudge.AutoApplicationUpdater;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;

namespace ManifestBuilder
{
    internal class Processor
    {
        public const int ConsoleLineLength = 100;
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
        public static void BuildFile(string assemblyPath, string packageFilePath, string outputFile, string installerURL, bool isRequired = false)
        {
            UpdateManifest manifest = new(assemblyPath);
            manifest.IsRequired = isRequired;

            FileInfo packageFile = new(packageFilePath);
            manifest.FilePackageName = packageFile.Name;
            manifest.FilePackageSize = packageFile.Length;
            manifest.FilePackageChecksum = UpdateManifest.GetFileChecksum(packageFilePath);
            manifest.RemoteURLSourcePackage = installerURL;

            FileInfo assembly = new(assemblyPath);
            FileInfo targetFile;
            if (outputFile.EndsWith('\\'))
            {
                DirectoryInfo dir = new(outputFile[..^1]);
                targetFile = new(Path.Combine(dir.FullName, assembly.Name[..^assembly.Extension.Length] + ".json"));
            }
            else
            {

                targetFile = new(outputFile);
                if (string.IsNullOrEmpty(targetFile.Extension))
                {
                    targetFile = new(outputFile + ".json");
                }
            }
            using StreamWriter sw = new(targetFile.FullName);
            sw.Write(manifest.Serialize());
        }
        const string assemblyParm = "assembly";
        const string outParm = "out";
        const string remoteurlParm = "remoteurl";
        const string installerParm = "installer";
        const string requiredParm = "required";
        const string configParm = "config";

        private void ProcessConfigurationFile(string configurationPath)
        {
            if (File.Exists(configurationPath))
            {
                WriteLine("Processing Configuration File");
                using StreamReader sr = new(configurationPath);
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith('-'))
                    {
                        int idx = line.IndexOf('=');
                        if (idx > -1)
                        {
                            string parameter = line[1..idx];
                            string value = line[(idx + 1)..];
                            switch (parameter.ToLowerInvariant())
                            {
                                case assemblyParm:
                                    AssemblyPath = value;
                                    break;
                                case outParm:
                                    OutPath = value;
                                    break;
                                case remoteurlParm:
                                    InstallerURL = value;
                                    break;
                                case installerParm:
                                    PackageFilePath = value;
                                    break;
                                case requiredParm:
                                    IsRequired = value.Equals("true", StringComparison.InvariantCultureIgnoreCase);
                                    break;
                                default:
                                    WriteLine($"!!!!!  WARNING: INVALID PARAMETER ({parameter}) in Configuration file (ignored)   !!!!");
                                    break;
                            }
                        }
                        else
                        {
                            WriteLine("ERROR: Invalid line in configuration file.  Correct line syntax is:");
                            WriteLine("-parameter=value");
                        }
                    }
                }
            }
            else
            {
                WriteLine("ERROR: Parameter to process configuration file provided--but does not exist.");
            }
        }
        public Processor()
        {
            int idx = 0;
            var args = Environment.GetCommandLineArgs();
            string currentParameter = string.Empty;
            string assemblyPath = string.Empty;
            string packageFilePath = string.Empty;
            string outPath = string.Empty;
            string installerURL = string.Empty;
            bool isRequired = false;
            bool isRequiredWasSet = false;
            while (++idx < args.Length)
            {
                string currentValue;
                if (args[idx].StartsWith('-'))
                {
                    currentParameter = args[idx];
                }
                else
                {
                    currentValue = args[idx];
                    switch (currentParameter.ToLowerInvariant())
                    {
                        case $"-{assemblyParm}":
                            assemblyPath = currentValue;
                            break;
                        case $"-{outParm}":
                            outPath = currentValue;
                            break;
                        case $"-{remoteurlParm}":
                            installerURL = currentValue;
                            break;
                        case $"-{installerParm}":
                            packageFilePath = currentValue;
                            break;
                        case $"-{requiredParm}":
                            isRequired = currentValue.Equals("true", StringComparison.InvariantCultureIgnoreCase);
                            isRequiredWasSet = true;
                            break;
                        case $"-{configParm}":
                            ProcessConfigurationFile(currentValue);
                            break;
                        default:
                            WriteLine($"!!!!!  INVALID PARAMETER ({currentParameter}) PROVIDED (ignored)   !!!!");
                            break;
                    }
                    currentParameter = string.Empty;
                }
            }
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                AssemblyPath = assemblyPath;
            }
            if (!string.IsNullOrEmpty(packageFilePath))
            {
                PackageFilePath = packageFilePath;
            }
            if (!string.IsNullOrEmpty(outPath))
            {
                OutPath = outPath;
            }
            if (!string.IsNullOrEmpty(installerURL))
            {
                InstallerURL = installerURL;
            }
            if (isRequiredWasSet)
            {
                IsRequired = isRequired;
            }
        }
        public string AssemblyPath { get; private set; } = string.Empty;
        public string PackageFilePath { get; private set; } = string.Empty;
        public string OutPath { get; private set; } = string.Empty;
        public string InstallerURL { get; private set; } = string.Empty;
        public bool IsRequired { get; private set; }

        private readonly List<string> missingParameters = [];
        private bool ValidateParameters()
        {
            if (string.IsNullOrEmpty(AssemblyPath))
            {
                missingParameters.Add($"-{assemblyParm}");
            }
            if (string.IsNullOrEmpty(PackageFilePath))
            {
                missingParameters.Add($"-{installerParm}");
            }
            if (string.IsNullOrEmpty(OutPath))
            {
                missingParameters.Add($"-{outParm}");
            }
            if (string.IsNullOrEmpty(InstallerURL))
            {
                missingParameters.Add($"-{remoteurlParm}");
            }
            return missingParameters.Count == 0;
        }
        private string GetTargetInstallerPath(string localPath)
        {
            int TargetNameStart = InstallerURL.LastIndexOf('/');
            string TargetName = InstallerURL[(TargetNameStart + 1)..];
            string retVal = Path.Combine(localPath, TargetName);
            return retVal;
        }
        public void Process()
        {
            WriteStarLine();
            WriteLine("ManifestBuilder--Creating UpdateManifest file.");
            if (ValidateParameters())
            {
                FileInfo installer = new(PackageFilePath);
                if (!string.IsNullOrEmpty(installer.DirectoryName))
                {
                    string targetCompressedPackagePath = GetTargetInstallerPath(installer.DirectoryName);

                    if (targetCompressedPackagePath.EndsWith(UpdateManifest.ZIPExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        if (CreateZip(targetCompressedPackagePath, installer))
                        {
                            PackageFilePath = targetCompressedPackagePath;
                        }
                        else
                        {
                            WriteLine("Update manifest package checksum will be based on the installer package file and not the compressed file.");
                        }
                    }
                    else
                    {
                        FileInfo targetCompressed = new(targetCompressedPackagePath);
                        if (targetCompressed.Extension.StartsWith(installer.Extension, StringComparison.OrdinalIgnoreCase))
                        {
                            if (UpdateManifest.CompressionTypes.TryGetValue(targetCompressed.Extension.ToLowerInvariant(), out Type? type))
                            {
                                if (type != null)
                                {

                                    if (CompressFile(type, PackageFilePath, targetCompressedPackagePath))
                                    {
                                        PackageFilePath = targetCompressedPackagePath;
                                    }
                                    else
                                    {
                                        WriteLine("Update manifest package checksum will be based on the installer package file and not the compressed file.");
                                    }
                                }
                            }
                        }
                        else
                        {
                            WriteLine($"Error: The target compressed package MUST include the package extension ({installer.Extension}) in the file name.");
                            string expectedExtension = $"{installer.Extension}.{targetCompressed.Extension}";
                            string compressedName = targetCompressed.Name[..^targetCompressed.Extension.Length];
                            WriteLine($"---> \"{targetCompressed.Name}\" is not valid!  \"{compressedName}{expectedExtension}\" is expected.");
                            WriteLine("Cannot proceed with compression.");
                        }
                    }
                }
                BuildFile(AssemblyPath, PackageFilePath, OutPath, InstallerURL, IsRequired);

            }
            else
            {
                WriteLine($"Error: Missing Parameters: {string.Join(", ", missingParameters.ToArray())}");
            }
            WriteStarLine();
        }

        private static bool CreateZip(string zipPath, FileInfo installer)
        {
            bool retVal = false;
            WriteLine("Compressing installer package to match file extension on installer URL...");
            try
            {
                using var fileStream = new FileStream(zipPath, FileMode.Create);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true);
                var zipArchiveEntry = archive.CreateEntry(installer.Name, CompressionLevel.Optimal);
                using var installerInStream = File.OpenRead(installer.FullName);
                using var zipStream = zipArchiveEntry.Open();
                installerInStream.CopyTo(zipStream);
                WriteLine("Compression Successful.");
                retVal = true;
            }
            catch (Exception ex)
            {
                WriteLine("Error: Unable to compress installer package--");
                WriteLine(ex.Message);
                retVal = false;
            }
            return retVal;
        }

        private static bool CompressFile(Type compressorType, string OriginalFileName, string CompressedFileName)
        {
            WriteLine("Compressing installer package to match file extension on installer URL...");

            bool retVal = false;
            ConstructorInfo? constructor = compressorType.GetConstructor([typeof(Stream), typeof(CompressionLevel)]);
            if (constructor != null)
            {
                try
                {
                    using FileStream originalFileStream = File.Open(OriginalFileName, FileMode.Open);
                    using FileStream compressedFileStream = File.Create(CompressedFileName);
                    using var compressor = (Stream)constructor.Invoke([compressedFileStream, CompressionLevel.Optimal]);
                    originalFileStream.CopyTo(compressor);
                    retVal = true;
                    WriteLine("Compression Successful.");
                }
                catch (Exception ex)
                {
                    WriteLine("Error: Unable to compress installer package--");
                    WriteLine(ex.Message);
                    retVal = false;
                }
            }
            else
            {
                WriteLine("Constructor for compression stream type was not found.  Expected parameters are: (<System.IO.Stream>, <System.IO.Compression.CompressionLevel>)");
            }

            return retVal;
        }
    }
}
