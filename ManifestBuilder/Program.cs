using ManifestBuilder;
using System.Xml.Linq;

if (Environment.GetCommandLineArgs().Length <= 1)
{
    Processor.WriteLine("ManifestBuilder: This program examines an executable and generates an Update");
    Processor.WriteLine("Manifest file that can be uploaded to provide self-update functionality to  ");
    Processor.WriteLine("applications.".PadRight(Processor.ConsoleLineLength));
    Processor.WriteLine("Syntax:".PadRight(Processor.ConsoleLineLength));
    Console.WriteLine("ManifestBuilder -assembly <EXE or DLL to get version data from> -out <Output Path> -remoteurl <remoteURL>");
    Console.WriteLine("        -installer <installer Package Path> -required True");
    Console.WriteLine();
    Console.WriteLine("-assembly <EXE or DLL to get version data from> : The DLL or EXE that has Version data set on.");
    Console.WriteLine("-out <Output Path>                              : The output file to generate.");
    Console.WriteLine("                                                  If only the path");
    Console.WriteLine("                                                  is specified, then the output");
    Console.WriteLine("                                                  name generated");
    Console.WriteLine("                                                  into the path will be the executable");
    Console.WriteLine("                                                  name, with a .json extension.");
    Console.WriteLine("                                                  Be sure to end the output path with");
    Console.WriteLine("                                                  backslash (\\) to indicate the");
    Console.WriteLine("                                                  path and use the default filename.");
    Console.WriteLine("-remoteurl <remoteURL>                          : The URL where the package installer can");
    Console.WriteLine("                                                  be downloaded from.");
    Console.WriteLine("-installer <installer Package path>             : The local path to the installer package. this is required");
    Console.WriteLine("                                                  for generating the checksums to validate the the download");
    Console.WriteLine("                                                  of the installer package.");
    Console.WriteLine("-required <true or false>                       : Optional parameter that if set indicates");
    Console.WriteLine("                                                  that the update is required (and must be applied).");
    Console.WriteLine("                                                  Default is False (not required).");
    Console.WriteLine("-config <parameter file>                        : Full path to a file that contains all the above settings.");
    Console.WriteLine("                                                  This will make all the previous settings optional, but any");
    Console.WriteLine("                                                  settings entered on the command line will override the");
    Console.WriteLine("                                                  parameter file.");
    Console.WriteLine("    The <parameter file> format is to have each parameter set on a single line in any order: ");
    Console.WriteLine("-parameter=value");
    Console.WriteLine("    So, an example parameter file is:");
    Console.WriteLine(@"-assembly=C:\MyProject\bin\Debug\net8.0\example.exe");
    Console.WriteLine(@"-out=C:\MyProjectSetup\Debug\example.json");
    Console.WriteLine(@"-installer=C:\MyProjectSetup\Debug\example.msi");
    Console.WriteLine(@"-remoteurl=https://mywebsite.com/software/example.msi");
    Console.WriteLine(@"-required=false");
}
else
{
    try
    {
        Processor prc = new();
        prc.Process();
    }
    catch (Exception ex)
    {
        Processor.WriteLine($"ERROR: {ex.Message}");
    }
}


//"C:\Program Files (x86)\Windows Kits\10\bin\10.0.17763.0\x86\MsiInfo.exe" "$(ProjectDir)Debug\<projectname>.msi"  -w 8
//"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x86\MsiInfo.exe"
//Above for not requiring admin privileges


//Windows SDK URL (to install MsiInfo.exe): https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/

//MsiInfo.exe documentation: https://learn.microsoft.com/en-us/windows/win32/msi/msiinfo-exe