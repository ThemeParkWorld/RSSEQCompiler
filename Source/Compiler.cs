using System;
using System.Collections.Generic;
using System.IO;

namespace RSSEQCompiler
{
    public class Compiler
    {
        private string[] fileContents;
        private Dictionary<string, int> branches = new Dictionary<string, int>();

        private readonly List<string> strings = new List<string>();
        private readonly List<string> variables = new List<string>();

        private int instructionCount;
        // TODO: defaults for everything that isnt timeSlice
        private int stackSize, bounceSize, walkSize, limboSize, timeSlice = 50;

        public Dictionary<string, int> unknownIdentifiers = new Dictionary<string, int>();

        public Compiler(string sourceFilePath, string destFilePath)
        {
            Compile(sourceFilePath, destFilePath);
        }

        private void ReadFileContents(string sourceFilePath)
        {
            using var sourceFileReader = new StreamReader(sourceFilePath);
            fileContents = sourceFileReader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private Dictionary<string, int> FindAllBranches()
        {
            int currentPos = 1; // starts at 1 for some dumb reason. bullfrog really like doing this?

            // First, go through the source file and find all branches
            foreach (var line in fileContents)
            {
                if (line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("//") || string.IsNullOrWhiteSpace(line))
                    continue;

                var lineSplit = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var opcode = lineSplit[0];
                var operands = lineSplit[1..]; // Remove opcode from line - rest are operands

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

        private void WriteHeader(BinaryWriter binaryWriter)
        {
            // Write file header
            binaryWriter.Write(new char[] { 'R', 'S', 'S', 'E', 'Q', (char)0x0F, (char)0x01, (char)0x00 });
            binaryWriter.Write(0x11); // String count?
            binaryWriter.Write(0x12); // Stack size
            binaryWriter.Write(0x32); // Time slice - almost always 50 (haven't seen the preprocessor directive for this one yet).
            binaryWriter.Write(0x0); // Limbo size
            binaryWriter.Write(0x0); // Bounce size
            binaryWriter.Write(0x12); // Walk size

            // Write padding - 12 bytes
            for (int i = 0; i < 4; ++i)
            {
                binaryWriter.Write(new char[] { 'P', 'a', 'd', ' ' });
            }

            // TODO: Write instruction count here (0x30)
            binaryWriter.Write(0xFFFFFFFF);
        }

        private void ReadPreprocessorDirective(string opcode, string[] operands)
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
                case "#include": break; // Ignore includes, since they're useless to us (we don't have the source files)
                default:
                    Console.WriteLine($"Unknown preprocessor directive {opcode}");
                    break;
            }
        }

        private void WriteInstructions(BinaryWriter binaryWriter)
        {
            // TODO: rewrite to reduce nesting
            foreach (var line in fileContents)
            {
                if (line.StartsWith(";") || line.StartsWith("//") || string.IsNullOrWhiteSpace(line))
                    continue;

                var lineSplit = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var opcode = lineSplit[0];
                var operands = lineSplit[1..]; // Remove opcode from line - rest are operands

                if (opcode.StartsWith("#"))
                {
                    ReadPreprocessorDirective(opcode, operands);
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
                else if (!opcode.StartsWith(".") && !opcode.StartsWith(";") && !opcode.StartsWith("//"))
                {
                    binaryWriter.Write((short)(int)Enum.Parse(typeof(Opcode), opcode));
                    binaryWriter.Write(new byte[] { 0x00, 0x80 });

                    Type[] scriptDefEnums = { typeof(ScriptDefs), typeof(Events), typeof(Particles) };

                    bool isParsingString = false;
                    int currentString = 0;

                    instructionCount++;

                    // Write operands
                    foreach (var operand in operands)
                    {
                        if (operand.StartsWith(";") || operand.StartsWith("//"))
                        {
                            break;
                        }

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
                                bool isDefined = false;
                                foreach (var scriptDefEnum in scriptDefEnums)
                                {
                                    if (Enum.TryParse(scriptDefEnum, operand, out object objResult))
                                    {
                                        binaryWriter.Write((int)objResult);
                                        isDefined = true;
                                    }
                                }

                                if (!isDefined)
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
                                        if (!unknownIdentifiers.ContainsKey(operand))
                                            unknownIdentifiers.Add(operand, (int)binaryWriter.BaseStream.Position);
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

                        if (operand.EndsWith(";"))
                        {
                            break;
                        }
                    }
                }
            }

            instructionCount++;
        }

        private void WriteStringTable(BinaryWriter binaryWriter)
        {
            var valuesToWrite = strings;
            valuesToWrite.AddRange(variables);

            foreach (var value in valuesToWrite)
            {
                binaryWriter.Write(value.Length + 1);
                binaryWriter.Write(value.ToCharArray());
                binaryWriter.Write((byte)0x00);
            }
        }

        private void Compile(string sourceFilePath, string destFilePath)
        {
            using var destFileStream = new FileStream(destFilePath, FileMode.OpenOrCreate);
            using var binaryWriter = new BinaryWriter(destFileStream);

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
                // Offset, value
                {0x08, variables.Count},
                {0x0C, stackSize},
                {0x10, timeSlice},
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
