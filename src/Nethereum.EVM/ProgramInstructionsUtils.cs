using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.EVM
{
    public static class ProgramInstructionsUtils
    {
        public static bool ContainsFunctionSignature(List<ProgramInstruction> instructions, string signature)
        {
            signature = signature.EnsureHexPrefix();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].Instruction == Instruction.PUSH4 && instructions[i].ArgumentsAsHex().ToLower() == signature.ToLower())
                {
                    if (instructions[i + 1].Instruction == Instruction.EQ && instructions[i + 2].Instruction == Instruction.PUSH2)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool ContainsFunctionSignatures(List<ProgramInstruction> instructions, string[] signatures)
        {
            foreach (var signature in signatures)
            {
                if (!ContainsFunctionSignature(instructions, signature))
                {
                    return false;
                }
            }
            return true;
        }

        public static List<ProgramInstruction> GetProgramInstructions(byte[] byteCodeArray)
        {
            var i = 0;
            var programInstructions = new List<ProgramInstruction>();
            while (i < byteCodeArray.Length)
            {
                var currentByte = byteCodeArray[i];
                if (currentByte >= 0x60 && currentByte <= 0x60 + 31)
                {
                    var opcode = (Instruction)byteCodeArray[i];
                    var dataLength = currentByte - 0x60 + 1;
                    var availableBytes = byteCodeArray.Length - i - 1;
                    var actualDataLength = Math.Min(dataLength, availableBytes);
                    var dataBytes = new byte[dataLength];
                    for (int x = 0; x < actualDataLength; x++)
                    {
                        dataBytes[x] = byteCodeArray[i + x + 1];
                    }

                    programInstructions.Add(new ProgramInstruction()
                    {
                        Step = i,
                        Instruction = opcode,
                        Arguments = dataBytes,
                        Value = byteCodeArray[i]
                    });
                    i = i + 1 + dataLength;
                }
                else
                {

                    if (Enum.IsDefined(typeof(Instruction), (int)byteCodeArray[i]))
                    {
                        programInstructions.Add(new ProgramInstruction()
                        {
                            Step = i,
                            Instruction = (Instruction)byteCodeArray[i],
                            Value = byteCodeArray[i]
                        });
                        i++;
                    }
                    else
                    {
                        programInstructions.Add(new ProgramInstruction()
                        {
                            Step = i,
                            Value = byteCodeArray[i]
                        });
                        i++;
                    }
                }
            }
            return programInstructions;
        }


        public static List<ProgramInstruction> GetProgramInstructions(string byteCode)
        {
            return GetProgramInstructions(byteCode.HexToByteArray());
        }

        public static string DisassembleToString(List<ProgramInstruction> programInstructions)
        {
            var stringBuilder = new StringBuilder();
            foreach (var instruction in programInstructions)
            {
                stringBuilder.AppendLine(instruction.ToDisassemblyLine());
            }
            return stringBuilder.ToString();
        }

        public static string DisassembleToString(string byteCode)
        {
            var programInstructions = GetProgramInstructions(byteCode);
            return DisassembleToString(programInstructions);
        }

        public static string DisassembleSimplifiedToString(string byteCode)
        {
            var instructions = GetProgramInstructions(byteCode);
            var output = "";
            foreach (var instruction in instructions)
            {
                if (instruction.Instruction != null)
                {
                    output = output + " " + instruction.Instruction.ToString();
                    if (instruction.Arguments != null)
                    {
                        var argument = instruction.Arguments.ToHexCompact().ToUpper();
                        if (string.IsNullOrEmpty(argument))
                        {
                            argument = "0";
                        }
                        output = output + " 0x" + argument;
                    }
                }
                else
                {
                    output = output + " " + "0x" + instruction.Value.ToString("X").ToUpper();
                }
            }

            return output;
        }

    }
}