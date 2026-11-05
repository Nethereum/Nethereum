using System.Collections.Generic;

namespace Nethereum.EVM
{
    internal sealed class ProgramCodeAnalysis
    {
        private const long InstructionObjectBytes = 48;
        private const long ListSlotBytes = 8;
        private const long ArrayHeaderBytes = 24;
        private const long JumpDestinationEntryBytes = 24;

        private ProgramCodeAnalysis(List<ProgramInstruction> instructions, Dictionary<int, int> jumpDestinationIndexesByStep)
        {
            Instructions = instructions;
            JumpDestinationIndexesByStep = jumpDestinationIndexesByStep;
            EstimatedBytes = EstimateBytes(instructions, jumpDestinationIndexesByStep.Count);
        }

        public long EstimatedBytes { get; }

        public List<ProgramInstruction> Instructions { get; }
        public Dictionary<int, int> JumpDestinationIndexesByStep { get; }

        public static ProgramCodeAnalysis Build(byte[] byteCode, bool eip8024Enabled)
        {
            var instructions = ProgramInstructionsUtils.GetProgramInstructions(byteCode, eip8024Enabled);
            return new ProgramCodeAnalysis(instructions, MapJumpDestinations(instructions));
        }

        private static long EstimateBytes(List<ProgramInstruction> instructions, int jumpDestinationCount)
        {
            var bytes = instructions.Count * (InstructionObjectBytes + ListSlotBytes)
                        + jumpDestinationCount * JumpDestinationEntryBytes;
            foreach (var instruction in instructions)
            {
                if (instruction.Arguments != null)
                    bytes += (ArrayHeaderBytes + instruction.Arguments.Length + 7) / 8 * 8;
            }
            return bytes;
        }

        private static Dictionary<int, int> MapJumpDestinations(List<ProgramInstruction> instructions)
        {
            var map = new Dictionary<int, int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].Instruction == Instruction.JUMPDEST)
                {
                    map[instructions[i].Step] = i;
                }
            }
            return map;
        }
    }
}
