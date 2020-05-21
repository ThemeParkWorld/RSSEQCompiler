using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RSSEQCompiler
{
    class Program
    {
        enum ProgramMode
        {
            Compile,
            Decompile,
            BatchCompile
        }

        static void Main(string[] args)
        {
            ProgramMode programMode = ProgramMode.Compile;

            if (args.Length < 1)
            {
                Console.WriteLine("RSEEQ (de)compiler");
                Console.WriteLine("Usage: RSSEQCompiler.exe [Filename] (flags)");
                Console.WriteLine("Available flags:");
                Console.WriteLine("\t-d - decompile");
                Console.WriteLine("\t-a - compile all in folder");
                return;
            }

            for (int i = 0; i < args.Length; ++i)
            {
                if (args[i] == "-d")
                {
                    programMode = ProgramMode.Decompile;
                }
                else if (args[i] == "-a")
                {
                    programMode = ProgramMode.BatchCompile;
                }
            }

            if (programMode == ProgramMode.Decompile)
            {
                Console.WriteLine($"Decompiling {args[0]}.RSE");
                Decompiler d = new Decompiler($"{args[0]}.RSE", $"{args[0]}.rss");
            }
            else if (programMode == ProgramMode.BatchCompile)
            {
                Dictionary<string, int> newIdentifiers = new Dictionary<string, int>();
                foreach (var file in Directory.GetFiles("."))
                {
                    if (file.ToLower().EndsWith(".rss"))
                    {
                        string fileNoExt = Path.GetFileNameWithoutExtension(file);
                        Compiler c = new Compiler($"{fileNoExt}.rss", $"Recompiled_{fileNoExt}.RSE");

                        Console.WriteLine($"Compiled {file}");

                        using var fileStream = new FileStream($"{fileNoExt}.RSE", FileMode.Open);
                        using var binaryReader = new BinaryReader(fileStream);

                        foreach (var unknownIdentifier in c.unknownIdentifiers)
                        {
                            if (!newIdentifiers.ContainsKey(unknownIdentifier.Key))
                            {
                                if (binaryReader.BaseStream.Length < unknownIdentifier.Value)
                                    continue;
                                binaryReader.BaseStream.Seek(unknownIdentifier.Value - 4, SeekOrigin.Begin);
                                var value = binaryReader.ReadInt16();

                                if (newIdentifiers.ContainsValue(value))
                                {
                                    Console.WriteLine($"Warning: duplicate value '{value}' (existing: {newIdentifiers.First(t => t.Value == value)}) (new: {unknownIdentifier.Key})");
                                }

                                newIdentifiers.Add(unknownIdentifier.Key, value);
                            }
                        }
                    }
                }

                var newIdentifiersAsList = newIdentifiers.ToList();
                newIdentifiersAsList.Sort((a, b) => { return a.Value.CompareTo(b.Value); });
                foreach (var identifier in newIdentifiersAsList)
                {
                    Console.WriteLine($"{identifier.Key} = {identifier.Value},");
                }
            }
            else if (programMode == ProgramMode.Compile)
            {
                Console.WriteLine($"Compiling {args[0]}.rss");
                Compiler c = new Compiler($"{args[0]}.rss", $"Recompiled_{args[0]}.RSE");
            }
        }
    }
}
