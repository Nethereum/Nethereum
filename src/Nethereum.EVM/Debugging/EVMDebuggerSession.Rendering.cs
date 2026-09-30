using System;
using System.Text;

namespace Nethereum.EVM.Debugging
{
    public partial class EVMDebuggerSession
    {
        private const int DebugStringPreviewRows = 10;

        public string ToDebugString()
        {
            var trace = CurrentTrace;
            if (trace == null)
                return "No trace available";

            var sb = new StringBuilder();
            AppendStepHeader(sb, trace);
            AppendSourceContext(sb);
            AppendStackPreview(sb, trace);
            AppendStoragePreview(sb, trace);

            return sb.ToString();
        }

        public string ToSummaryString()
        {
            var trace = CurrentTrace;
            if (trace == null)
                return "No trace";

            var sourceLocation = GetCurrentSourceLocation();
            var sourcePart = sourceLocation != null
                ? $" | {sourceLocation}"
                : "";

            return $"[{CurrentStep + 1}/{TotalSteps}] {trace.Instruction?.Instruction?.ToString() ?? "???"}{sourcePart}";
        }

        private void AppendStepHeader(StringBuilder sb, ProgramTrace trace)
        {
            sb.AppendLine($"=== Step {CurrentStep + 1}/{TotalSteps} ===");
            sb.AppendLine($"Address: {trace.CodeAddress}");
            sb.AppendLine($"Depth: {trace.Depth}");
            sb.AppendLine($"Gas: {trace.GasCost}");

            if (trace.Instruction != null)
            {
                sb.AppendLine($"Instruction: {trace.Instruction.ToDisassemblyLine()}");
            }
        }

        private void AppendSourceContext(StringBuilder sb)
        {
            var sourceLocation = GetCurrentSourceLocation();
            if (sourceLocation == null)
                return;

            sb.AppendLine();
            sb.AppendLine($"Source: {sourceLocation}");
            sb.AppendLine(sourceLocation.GetContextLines());
        }

        private static void AppendStackPreview(StringBuilder sb, ProgramTrace trace)
        {
            if (trace.Stack == null || trace.Stack.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine("Stack:");
            for (int i = 0; i < Math.Min(trace.Stack.Count, DebugStringPreviewRows); i++)
            {
                sb.AppendLine($"  [{i}] {trace.Stack[i]}");
            }
            if (trace.Stack.Count > DebugStringPreviewRows)
                sb.AppendLine($"  ... ({trace.Stack.Count - DebugStringPreviewRows} more)");
        }

        private static void AppendStoragePreview(StringBuilder sb, ProgramTrace trace)
        {
            if (trace.Storage == null || trace.Storage.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine("Storage:");
            var count = 0;
            foreach (var kvp in trace.Storage)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                if (++count >= DebugStringPreviewRows)
                {
                    sb.AppendLine($"  ... ({trace.Storage.Count - DebugStringPreviewRows} more)");
                    break;
                }
            }
        }
    }
}
