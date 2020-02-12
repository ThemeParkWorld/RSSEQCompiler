using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Transactions;

namespace RSSEQCompiler
{
    public class Compiler
    {
        public Compiler(string sourceFilePath, string destFilePath)
        {
            Compile(sourceFilePath, destFilePath);
        }

        public void Compile(string sourceFilePath, string destFilePath)
        {
            using var sourceFileReader = new StreamReader(sourceFilePath);
            using var destFileStream = new FileStream(destFilePath, FileMode.OpenOrCreate);
            using var binaryWriter = new BinaryWriter(destFileStream);

            var variables = new List<string>();
            var branches = new Dictionary<string, int>();

            int currentPos = 0;

            string fileContents = sourceFileReader.ReadToEnd();
            string[] fileLines = fileContents.Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);

            // First, go through the source file and find all branches
            foreach (var line in fileLines)
            {
                if (line.StartsWith(";") || line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                    continue;

                var lineSplit = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var opcode = lineSplit[0];
                var operands = new string[lineSplit.Length - 1];

                Array.Copy(lineSplit, 1, operands, 0, lineSplit.Length - 1);

                currentPos += 1 + operands.Length;

                if (opcode.StartsWith("."))
                {
                    branches.Add(opcode.Substring(1), currentPos);
                }
            }


            // Reset back to the beginning of the source stream
            sourceFileReader.BaseStream.Seek(0, SeekOrigin.Begin);

            binaryWriter.Write(new char[] { 'R', 'S', 'S', 'E', 'Q' });

            byte[] header = new byte[27];
            binaryWriter.Write(header);
            for (int i = 0; i < 4; ++i)
            {
                binaryWriter.Write(new char[]{'P', 'a', 'd', ' '});
            }

            // TODO: Write instruction count here
            binaryWriter.Write(0xFFFFFFFF);

            foreach (var line in fileLines)
            {
                if (line.StartsWith(";") || line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                    continue;

                var lineSplit = line.Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                var opcode = lineSplit[0];
                var operands = new string[lineSplit.Length - 1];

                Array.Copy(lineSplit, 1, operands, 0, lineSplit.Length - 1);

                if (opcode.StartsWith(".") && operands.Length > 0)
                {
                    opcode = operands[0];
                }

                if (opcode == "variable")
                {
                    //Console.WriteLine($"Variable {operands[0]}");
                    variables.Add(operands[0]);
                }
                else if (opcode.StartsWith("."))
                {
                    // Do nothing
                }
                else
                {
                    //Console.WriteLine($"Opcode {opcode} ({(int)Enum.Parse(typeof(Opcode), opcode)})");
                    binaryWriter.Write((short)(int)Enum.Parse(typeof(Opcode), opcode));
                    binaryWriter.Write((byte)0x00);
                    binaryWriter.Write((byte)0x80);

                    // Write operands
                    foreach (var operand in operands)
                    {
                        if (operand.StartsWith(";"))
                            break;
                        if (int.TryParse(operand, out int operandInt))
                        {
                            binaryWriter.Write(operandInt);
                        }
                        else
                        {
                            if (variables.Contains(operand))
                            {
                                binaryWriter.Write(variables.IndexOf(operand));
                            }
                            else if (branches.ContainsKey(operand))
                            {
                                binaryWriter.Write(branches[operand]);
                            }
                            else
                            {
                                Console.WriteLine($"Unknown identifier {operand}");
                            }
                        }
                    }
                }

                // Console.WriteLine($"{currentLine}");
            }
        }
    }
}
