using System.Collections.Generic;
using System.Text;

namespace Nethereum.EVM
{
    public class ProgramTrace
    {
        public int TraceStep { get; set; }
        public List<string> Stack { get; set; }
        public ProgramInstruction Instruction { get; set; }
        public string Memory { get; set; }
        public Dictionary<string, string> Storage { get; set; }

        public override string ToString()
        {
            var builder = new StringBuilder();
           
            builder.AppendLine("Trace step:" + TraceStep);
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

        public static ProgramTrace CreateTraceFromCurrentProgram(int counter, Program program, ProgramInstruction programInstructionExecuted)
        {
            var trace = new ProgramTrace()
            {
                TraceStep = counter,
                Instruction = programInstructionExecuted,
                Stack = program.GetCurrentStackAsHex(),
                Memory = program.GetCurrentMemoryAsHex(),
                Storage = program.ProgramContext.GetProgramContextStorageAsHex(),
            };
            return trace;
        }
    }
}