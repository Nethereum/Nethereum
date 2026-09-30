using Nethereum.ABI.ABIRepository;
using Nethereum.EVM.SourceInfo;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.EVM.Debugging
{
    public class DebugStepInfo
    {
        public int Step { get; set; }
        public ProgramTrace Trace { get; set; }
        public SourceLocation Source { get; set; }
    }

    public static class EVMDebuggerExtensions
    {
        public static EVMDebuggerSession CreateDebugSession(this Program program, IABIInfoStorage abiStorage, long chainId)
        {
            var session = new EVMDebuggerSession(abiStorage);
            session.LoadFromProgram(program, chainId);
            return session;
        }

        public static async Task<EVMDebuggerSession> CreateDebugSessionAsync(this Program program, IABIInfoStorage abiStorage, long chainId)
        {
            var session = new EVMDebuggerSession(abiStorage);
            await session.LoadFromProgramAsync(program, chainId);
            return session;
        }

        public static EVMDebuggerSession CreateDebugSession(this List<ProgramTrace> trace, IABIInfoStorage abiStorage, long chainId)
        {
            var session = new EVMDebuggerSession(abiStorage);
            session.LoadFromTrace(trace, chainId);
            return session;
        }

        public static async Task<EVMDebuggerSession> CreateDebugSessionAsync(this List<ProgramTrace> trace, IABIInfoStorage abiStorage, long chainId)
        {
            var session = new EVMDebuggerSession(abiStorage);
            await session.LoadFromTraceAsync(trace, chainId);
            return session;
        }

        public static string GenerateFullTraceString(this EVMDebuggerSession session)
        {
            if (session.Trace == null || session.Trace.Count == 0)
                return "No trace available";

            var sb = new StringBuilder();
            sb.AppendLine("=== EVM Execution Trace ===");
            sb.AppendLine();

            var originalStep = session.CurrentStep;

            for (int i = 0; i < session.TotalSteps; i++)
            {
                session.GoToStep(i);
                var trace = session.CurrentTrace;

                if (trace.Instruction != null)
                {
                    var sourceLine = SourceLineKey(session.GetCurrentSourceLocation());
                    var sourcePart = sourceLine != null ? " | " + sourceLine : "";

                    sb.AppendLine($"[{i + 1:D4}] {trace.Instruction.ToDisassemblyLine()}{sourcePart}");
                }
            }

            session.GoToStep(originalStep);

            return sb.ToString();
        }

        public static string GenerateSourceAnnotatedTrace(this EVMDebuggerSession session)
        {
            if (session.Trace == null || session.Trace.Count == 0)
                return "No trace available";

            var sb = new StringBuilder();
            sb.AppendLine("=== Source-Annotated EVM Trace ===");
            sb.AppendLine();

            var originalStep = session.CurrentStep;
            string lastSourceLine = null;

            for (int i = 0; i < session.TotalSteps; i++)
            {
                session.GoToStep(i);
                var trace = session.CurrentTrace;
                var sourceLocation = session.GetCurrentSourceLocation();

                lastSourceLine = AppendSourceLineHeaderWhenLineChanges(sb, sourceLocation, lastSourceLine);
                AppendDepthIndentedInstruction(sb, trace);
            }

            session.GoToStep(originalStep);

            return sb.ToString();
        }

        private static string AppendSourceLineHeaderWhenLineChanges(StringBuilder sb, SourceLocation sourceLocation, string lastSourceLine)
        {
            if (sourceLocation == null)
                return lastSourceLine;

            var currentSourceLine = SourceLineKey(sourceLocation);
            if (currentSourceLine == lastSourceLine)
                return lastSourceLine;

            if (lastSourceLine != null)
                sb.AppendLine();

            sb.AppendLine($"// {currentSourceLine}");
            if (!string.IsNullOrWhiteSpace(sourceLocation.SourceCode))
            {
                sb.AppendLine($"// {sourceLocation.SourceCode.Trim()}");
            }

            return currentSourceLine;
        }

        private static void AppendDepthIndentedInstruction(StringBuilder sb, ProgramTrace trace)
        {
            if (trace.Instruction == null)
                return;

            var prefix = new string(' ', trace.Depth * 2);
            sb.AppendLine($"  {prefix}{trace.Instruction.ToDisassemblyLine()}");
        }

        public static List<SourceLocation> GetUniqueSourceLocations(this EVMDebuggerSession session)
        {
            var result = new List<SourceLocation>();
            var seen = new HashSet<string>();

            if (session.Trace == null)
                return result;

            var originalStep = session.CurrentStep;

            for (int i = 0; i < session.TotalSteps; i++)
            {
                session.GoToStep(i);
                var sourceLocation = session.GetCurrentSourceLocation();

                if (sourceLocation != null)
                {
                    var key = $"{sourceLocation.FilePath}:{sourceLocation.Position}:{sourceLocation.Length}";
                    if (!seen.Contains(key))
                    {
                        seen.Add(key);
                        result.Add(sourceLocation);
                    }
                }
            }

            session.GoToStep(originalStep);

            return result;
        }

        public static bool HasDebugInfo(this EVMDebuggerSession session)
        {
            if (session.Trace == null || session.Trace.Count == 0)
                return false;

            var originalStep = session.CurrentStep;
            session.GoToStep(0);

            var hasDebugInfo = false;
            for (int i = 0; i < session.TotalSteps && !hasDebugInfo; i++)
            {
                session.GoToStep(i);
                hasDebugInfo = session.GetCurrentSourceLocation() != null;
            }

            session.GoToStep(originalStep);
            return hasDebugInfo;
        }

        public static IEnumerable<DebugStepInfo> EnumerateWithSource(this EVMDebuggerSession session)
        {
            if (session.Trace == null)
                yield break;

            var originalStep = session.CurrentStep;

            for (int i = 0; i < session.TotalSteps; i++)
            {
                session.GoToStep(i);
                yield return new DebugStepInfo
                {
                    Step = i,
                    Trace = session.CurrentTrace,
                    Source = session.GetCurrentSourceLocation()
                };
            }

            session.GoToStep(originalStep);
        }

        public static void StepToNextSourceLine(this EVMDebuggerSession session)
        {
            if (!session.CanStepForward) return;

            var currentKey = SourceLineKey(session.GetCurrentSourceLocation());

            while (session.CanStepForward)
            {
                session.StepForward();
                var newKey = SourceLineKey(session.GetCurrentSourceLocation());

                if (newKey != null && newKey != currentKey)
                    break;
            }
        }

        public static void StepToPreviousSourceLine(this EVMDebuggerSession session)
        {
            if (!session.CanStepBack) return;

            var currentKey = SourceLineKey(session.GetCurrentSourceLocation());

            while (session.CanStepBack)
            {
                session.StepBack();
                var newKey = SourceLineKey(session.GetCurrentSourceLocation());

                if (newKey != null && newKey != currentKey)
                    break;
            }
        }

        private static string SourceLineKey(SourceLocation location) =>
            location != null ? $"{location.FilePath}:{location.LineNumber}" : null;
    }
}
