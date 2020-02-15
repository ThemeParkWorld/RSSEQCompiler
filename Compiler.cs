using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;

namespace RSSEQCompiler
{
    public class Compiler
    {
        private string[] fileContents;
        private Dictionary<string, int> branches;
        private List<string> strings = new List<string>();
        private List<string> variables = new List<string>();
        private int instructionCount;

        private int stackSize;
        private int bounceSize;
        private int walkSize;
        private int limboSize;

        public Compiler(string sourceFilePath, string destFilePath)
        {
            Compile(sourceFilePath, destFilePath);
        }

        void ReadFileContents(string sourceFilePath)
        {
            using var sourceFileReader = new StreamReader(sourceFilePath);
            fileContents = sourceFileReader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        Dictionary<string, int> FindAllBranches()
        {
            Dictionary<string, int> branches = new Dictionary<string, int>();

            int currentPos = 1; // starts at 1 for some dumb reason. bullfrog really like doing this?

            // First, go through the source file and find all branches
            foreach (var line in fileContents)
            {
                if (line.StartsWith(";") || line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                    continue;

                var lineSplit = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var opcode = lineSplit[0];
                var operands = new string[lineSplit.Length - 1];
                
                Array.Copy(lineSplit, 1, operands, 0, lineSplit.Length - 1);

                int operandCount = 0;
                bool isCurrentlyString = false;
                foreach (var operand in operands)
                {
                    if (operand == null)
                        continue;

                    if (operand.StartsWith("\""))
                        isCurrentlyString = true;

                    if (operand.EndsWith("\""))
                        isCurrentlyString = false;

                    if (operand.StartsWith(";"))
                        break;

                    if (!isCurrentlyString)
                        operandCount++;
                }

                if (opcode == "variable")
                    continue;
                
                if (opcode.StartsWith("."))
                {
                    currentPos--;
                    branches.Add(opcode.Substring(1), currentPos);
                }

                // Console.WriteLine($"{currentPos} {opcode}");
                currentPos += operandCount + 1;
            }

            return branches;
        }

        void WriteHeader(BinaryWriter binaryWriter)
        {
            // Write file header
            binaryWriter.Write(new char[] { 'R', 'S', 'S', 'E', 'Q', (char)0x0F, (char)0x01, (char)0x00 });
            binaryWriter.Write((int)0x11); // String count?
            binaryWriter.Write((int)0x12); // Stack size
            binaryWriter.Write((int)0x32); // Unknown 1
            binaryWriter.Write((int)0x0); // Unknown 2
            binaryWriter.Write((int)0x0); // unknown 3
            binaryWriter.Write((int)0x12); // Walk size

            for (int i = 0; i < 4; ++i)
            {
                binaryWriter.Write(new char[] { 'P', 'a', 'd', ' ' });
            }

            // TODO: Write instruction count here (0x30)
            binaryWriter.Write(0xFFFFFFFF);
        }

        void WriteInstructions(BinaryWriter binaryWriter)
        {
            // Write instructions
            foreach (var line in fileContents)
            {
                if (line.StartsWith(";")|| string.IsNullOrWhiteSpace(line))
                    continue;

                var lineSplit = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var opcode = lineSplit[0];
                var operands = new string[lineSplit.Length - 1];

                Array.Copy(lineSplit, 1, operands, 0, lineSplit.Length - 1);
                
                if (opcode.StartsWith("#"))
                {
                    switch (opcode)
                    {
                        case "#setstack":
                            stackSize = int.Parse(operands[0]);
                            break;
                        case "#setwalk":
                            walkSize = int.Parse(operands[0]);
                            break;
                        case "#setlimbo":
                            limboSize = int.Parse(operands[0]);
                            break;
                        case "#setbounce":
                            bounceSize = int.Parse(operands[0]);
                            break;
                        case "#include": break; // Ignore includes, since they're useless to us
                        default:
                            Console.WriteLine($"Unknown preprocessor instruction {opcode}");
                            break;
                    }

                    continue;
                }

                if (opcode.StartsWith(".") && operands.Length > 0)
                {
                    opcode = operands[0];

                    var tmp = new string[operands.Length - 1];
                    Array.Copy(operands, 1, tmp, 0, operands.Length - 1);
                    operands = tmp;
                }

                if (opcode == "variable")
                {
                    variables.Add(operands[0]);
                }
                else if (!opcode.StartsWith("."))
                {
                    binaryWriter.Write((short)(int)Enum.Parse(typeof(Opcode), opcode));
                    binaryWriter.Write(new byte[] { 0x00, 0x80 });

                    Type[] scriptDefEnums = { typeof(ScriptDefs), typeof(SfxEvent), typeof(Particles) };

                    bool isParsingString = false;
                    int currentString = 0;

                    instructionCount++;

                    // Write operands
                    foreach (var operand in operands)
                    {
                        if (operand.StartsWith(";"))
                            break;
                        if (operand.StartsWith("\""))
                        {
                            strings.Add("");
                            isParsingString = true;
                        }

                        if (isParsingString)
                        {
                            strings[currentString] += operand.Replace("\"", "");
                        }
                        else
                        {
                            instructionCount++;

                            if (int.TryParse(operand, out int operandInt))
                            {
                                binaryWriter.Write((short)operandInt);
                                binaryWriter.Write((short)0x00);
                            }
                            else
                            {
                                bool found = false;
                                foreach (var scriptDefEnum in scriptDefEnums)
                                {
                                    if (Enum.TryParse(scriptDefEnum, operand, out object objResult))
                                    {
                                        binaryWriter.Write((int)objResult);
                                        Console.WriteLine($"{operand} = {(int)objResult}");
                                        found = true;
                                    }
                                }

                                if (!found)
                                {
                                    if (variables.Contains(operand))
                                    {
                                        binaryWriter.Write((short)variables.IndexOf(operand));
                                        binaryWriter.Write((short)0x4000);
                                    }
                                    else if (branches.ContainsKey(operand))
                                    {
                                        binaryWriter.Write((short)branches[operand]);
                                        binaryWriter.Write((short)0x2000);
                                    }
                                    else
                                    {
                                        binaryWriter.Write((int)0);
                                        Console.WriteLine($"Unknown identifier {operand}");
                                    }
                                }
                            }
                        }

                        if (operand.EndsWith("\""))
                        {
                            isParsingString = false;
                            binaryWriter.Write((short)currentString);
                            binaryWriter.Write((short)0x1000);
                            currentString++;
                        }
                        else if (isParsingString)
                        {
                            strings[currentString] += " ";
                        }
                    }
                }
            }
            instructionCount++;
        }

        void WriteStringTable(BinaryWriter binaryWriter)
        {
            foreach (var str in strings)
            {
                binaryWriter.Write(str.Length + 1);
                binaryWriter.Write(str.ToCharArray());
                binaryWriter.Write((byte)0x00);
            }
            foreach (var variable in variables)
            {
                binaryWriter.Write(variable.Length + 1);
                binaryWriter.Write(variable.ToCharArray());
                binaryWriter.Write((byte)0x00);
            }
        }

        void Compile(string sourceFilePath, string destFilePath)
        {
            using var destFileStream = new FileStream(destFilePath, FileMode.OpenOrCreate);
            using var binaryWriter = new BinaryWriter(destFileStream);
            int currentPos = 0;

            ReadFileContents(sourceFilePath);
            branches = FindAllBranches();

            WriteHeader(binaryWriter);
            WriteInstructions(binaryWriter);
            WriteStringTable(binaryWriter);
            WriteHeaderInfo(binaryWriter);
            WriteInstructionCount(binaryWriter);
        }

        private void WriteHeaderInfo(BinaryWriter binaryWriter)
        {
            Dictionary<int, int> headerValues = new Dictionary<int, int>()
            {
                {0x08, variables.Count},
                {0x0C, stackSize},
                {0x14, limboSize},
                {0x18, bounceSize},
                {0x1C, walkSize}
            };

            foreach (var headerValue in headerValues)
            {
                binaryWriter.Seek(headerValue.Key, SeekOrigin.Begin);
                binaryWriter.Write(headerValue.Value);
            }
        }

        private void WriteInstructionCount(BinaryWriter binaryWriter)
        {
            binaryWriter.Seek(0x30, SeekOrigin.Begin);
            binaryWriter.Write(instructionCount);
        }
    }
}
