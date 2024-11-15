// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

//"C:\Program Files (x86)\Windows Kits\10\bin\10.0.17763.0\x86\MsiInfo.exe" "$(ProjectDir)Debug\<projectname>.msi"  -w 8
//"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x86\MsiInfo.exe"
//Above for not requiring admin privileges
//Creates manifest file
//Command line program. parameters are:
//"<pathToManifestBuilderEXE>\ManifestBuilder.exe" "$(ProjectDir)..\<MainProjectFolder>\bin\<Debug/Release>\<.NET core version>\<project.exe>" -url="http://url for where the .manifest file will be located." <MsiInfo parameters (including path to MSI file)>


//$(ProjectDir)..\VersionBump\bin\Debug\net8.0\VersionBump.exe $(ProjectDir)ExampleSetup.vdproj


//Windows SDK URL (to install MsiInfo.exe): https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/

//MsiInfo.exe documentation: https://learn.microsoft.com/en-us/windows/win32/msi/msiinfo-exe