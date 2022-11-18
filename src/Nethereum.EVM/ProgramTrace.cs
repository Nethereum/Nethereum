using System.Collections.Generic;
using System.Text;

namespace Nethereum.EVM
{
    public class ProgramTrace
    {
        public string ProgramAddress { get; set; }
        public int VMTraceStep { get; set; }
        public int ProgramTraceStep { get; set; }
        public List<string> Stack { get; set; }
        public ProgramInstruction Instruction { get; set; }
        public string Memory { get; set; }
        public Dictionary<string, string> Storage { get; set; }
        public int Depth { get; set; }
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Address:" + ProgramAddress);
            builder.AppendLine("VMTraceStep Trace step:" + VMTraceStep);
            builder.AppendLine("Depth: " + Depth);

            builder.AppendLine("Program Trace step:" + ProgramTraceStep);
            builder.AppendLine(Instruction.ToDisassemblyLine());
            if (Stack != null)
            {
                builder.AppendLine("Stack:");
            
                foreach (var stackItem in Stack)
                {
                    builder.AppendLine(stackItem.ToString());
                }
            }
            if (Storage != null)
            {
                builder.AppendLine("Storage:");
                foreach (var storageItem in Storage)
                {
                    builder.AppendLine(storageItem.Key.ToString() + ":" + storageItem.Value.ToString());
                }
            }
            if (Memory != null)
            {
                builder.AppendLine("Memory:");
                builder.AppendLine(Memory);
            }
            return builder.ToString();
        }

        public static ProgramTrace CreateTraceFromCurrentProgram(string programAddress, int vmTraceStep, int programTraceStep, int depth, Program program, ProgramInstruction programInstructionExecuted)
        {
            var trace = new ProgramTrace()
            {
                ProgramAddress = programAddress,
                VMTraceStep = vmTraceStep,
                ProgramTraceStep = programTraceStep,
                Instruction = programInstructionExecuted,
                Stack = program.GetCurrentStackAsHex(),
                Memory = program.GetCurrentMemoryAsHex(),
                Storage = program.ProgramContext.GetProgramContextStorageAsHex(),
                Depth = depth
            };
            return trace;
        }
    }
}