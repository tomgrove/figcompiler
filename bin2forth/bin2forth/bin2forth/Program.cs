using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace bin2forth
{
    class Program
    {
        static void Main(string[] args)
        {
            var bytes = File.ReadAllBytes(args[0]);
            Console.WriteLine("CREATE {0} ", args[1]);
            Console.WriteLine("HEX");
            var numerals = "0123456789ABCDEF";
            foreach (var b in bytes)
            {
                char high = numerals[(int)(b / 16)];
                char low = numerals[(int)(b % 16)];
                Console.WriteLine("\t{0}{1} C,", high, low);
            }
            Console.WriteLine("SMUDGE");
            Console.WriteLine("DECIMAL");
        }
    }
}
