// See https://aka.ms/new-console-template for more information
using VersionBump;
try
{
    Processor.Process(Environment.GetCommandLineArgs());
}
catch (Exception ex)
{
    Console.WriteLine($"Error in VersionBump:\r\n{ex.Message}");
}