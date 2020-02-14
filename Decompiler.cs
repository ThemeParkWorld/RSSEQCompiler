using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RSSEQCompiler
{
    public class Decompiler
    {
        public Decompiler(string sourceFilePath, string destFilePath)
        {
            this.Decompile(sourceFilePath);
        }

        private void Decompile(string sourceFilePath)
        {
            using var sourceFileStream = new FileStream(sourceFilePath, FileMode.Open);
            using var binaryReader = new BinaryReader(sourceFileStream);

            /* File format:
             * File header:
             * 5 bytes - RSSEQ (Magic number)
             * 3 bytes - ???
             * 24 bytes - unknown
             * 16 bytes - "Pad Pad Pad Pad"
             */
            // Write to console as we go along
            Log("Checking for magic number");
            char[] magicNumber = binaryReader.ReadChars(5);
            if (!Enumerable.SequenceEqual(magicNumber, new[] { 'R', 'S', 'S', 'E', 'Q' }))
                Log("Magic number was not 'RSSEQ'");

            Log("Skipping 27 bytes");
            binaryReader.ReadBytes(27);

            Log("Checking for padding");
            for (int i = 0; i < 4; ++i)
            {
                char[] paddingChars = binaryReader.ReadChars(4);
                if (!Enumerable.SequenceEqual(paddingChars, new[] { 'P', 'a', 'd', ' ' }))
                    Log("Invalid padding!");
            }

            Log("Getting string table...");
            // TODO: Find string table offset from file header?

            // Instructions follow order operand -> opcode
            // Opcode (4 bytes) always ends in "80"
            List<Instruction> instructions = new List<Instruction>();

            List<int> currentOperands = new List<int>();
            int currentOpcode = 0;

            List<int> jumps = new List<int>();

            // First 4 bytes are # of expected opcodes & operands
            int expectedInstructions = binaryReader.ReadInt32();

            while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length - 1)
            {
                int currentValue = binaryReader.ReadInt32();

                if ((currentValue >> 24 & 0xFF) == 0x80)
                {
                    if ((Opcode)currentOpcode == Opcode.BRANCH || (Opcode)currentOpcode == Opcode.BRANCH_NZ ||
                        (Opcode)currentOpcode == Opcode.BRANCH_PV || (Opcode)currentOpcode == Opcode.BRANCH_NZ ||
                        (Opcode)currentOpcode == Opcode.JSR)
                    {
                        jumps.Add(currentOperands[0] + (instructions[^1].GetCount() - 1));
                    }
                    instructions.Add(new Instruction(currentOpcode, currentOperands.ToArray()));
                    currentOpcode = (short)currentValue;
                    currentOperands = new List<int>();
                }
                else
                {
                    currentOperands.Add((short)currentValue);
                }

                int counter = 0;
                foreach (var instruction in instructions)
                    counter += instruction.GetCount();

                if (counter >= expectedInstructions - 1)
                {
                    Log("Hit max opcode operand count");
                    instructions.Add(new Instruction(currentOpcode, currentOperands.ToArray()));
                    break;
                }
            }


            int currentCount = 0;
            for (int i = 0; i < instructions.Count; ++i)
            {
                // TODO: Fix offsets for jumps
                if (jumps.Contains(currentCount) && false)
                {
                    Log($".fn{currentCount}\t{instructions[i].ToString()}");
                }
                else
                {
                    Log($"\t\t{instructions[i].ToString()}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
                currentCount += instructions[i].GetCount();
            }
            Log($"Count: {currentCount}");
        }


        private void Log(string str)
        {
            Console.WriteLine($"{DateTime.Now.ToString()} {str}");
        }
    }
}
