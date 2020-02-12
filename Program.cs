using System;
using System.IO;

namespace RSSEQCompiler
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("RSEEQ (de)compiler");
                Console.WriteLine("Usage: RSSEQCompiler.exe [Filename] (flags)");
            }
            else
            {
                bool decompile = false;
                for (int i = 1; i < args.Length; ++i)
                {
                    if (args[i] == "-d")
                    {
                        // Decompile
                        decompile = true;
                    }
                }

                if (decompile)
                {
                    Console.WriteLine($"Decompiling {args[0]}.RSE");
                    Decompiler d = new Decompiler($"{args[0]}.RSE", $"{args[0]}.rss");
                }
                else
                {
                    Console.WriteLine($"Compiling {args[0]}.rss");
                    Compiler c = new Compiler($"{args[0]}.rss", $"{args[0]}.RSE");
                }
            }
        }
    }
}
