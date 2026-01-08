using Nethereum.ABI.ABIRepository;
using Nethereum.EVM.SourceInfo;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

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

        public static EVMDebuggerSession CreateDebugSession(this List<ProgramTrace> trace, IABIInfoStorage abiStorage, long chainId)
        {
            var session = new EVMDebuggerSession(abiStorage);
            session.LoadFromTrace(trace, chainId);
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
                    var sourceLocation = session.GetCurrentSourceLocation();
                    var sourcePart = sourceLocation != null
                        ? $" | {sourceLocation.FilePath}:{sourceLocation.LineNumber}"
                        : "";

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

                if (sourceLocation != null)
                {
                    var currentSourceLine = $"{sourceLocation.FilePath}:{sourceLocation.LineNumber}";
                    if (currentSourceLine != lastSourceLine)
                    {
                        if (lastSourceLine != null)
                            sb.AppendLine();

                        sb.AppendLine($"// {currentSourceLine}");
                        if (!string.IsNullOrWhiteSpace(sourceLocation.SourceCode))
                        {
                            sb.AppendLine($"// {sourceLocation.SourceCode.Trim()}");
                        }
                        lastSourceLine = currentSourceLine;
                    }
                }

                if (trace.Instruction != null)
                {
                    var prefix = new string(' ', trace.Depth * 2);
                    sb.AppendLine($"  {prefix}{trace.Instruction.ToDisassemblyLine()}");
                }
            }

            session.GoToStep(originalStep);

            return sb.ToString();
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

            var currentSource = session.GetCurrentSourceLocation();
            var currentKey = currentSource != null
                ? $"{currentSource.FilePath}:{currentSource.LineNumber}"
                : null;

            while (session.CanStepForward)
            {
                session.StepForward();
                var newSource = session.GetCurrentSourceLocation();
                var newKey = newSource != null
                    ? $"{newSource.FilePath}:{newSource.LineNumber}"
                    : null;

                if (newKey != null && newKey != currentKey)
                    break;
            }
        }

        public static void StepToPreviousSourceLine(this EVMDebuggerSession session)
        {
            if (!session.CanStepBack) return;

            var currentSource = session.GetCurrentSourceLocation();
            var currentKey = currentSource != null
                ? $"{currentSource.FilePath}:{currentSource.LineNumber}"
                : null;

            while (session.CanStepBack)
            {
                session.StepBack();
                var newSource = session.GetCurrentSourceLocation();
                var newKey = newSource != null
                    ? $"{newSource.FilePath}:{newSource.LineNumber}"
                    : null;

                if (newKey != null && newKey != currentKey)
                    break;
            }
        }
    }
}
