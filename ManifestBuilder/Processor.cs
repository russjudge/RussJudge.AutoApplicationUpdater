using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManifestBuilder
{
    internal class Processor
    {
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
    }
}
