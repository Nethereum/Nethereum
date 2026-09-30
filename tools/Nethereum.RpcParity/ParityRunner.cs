using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Nethereum.RpcParity
{
    /// <summary>
    /// Calls one RPC method against both nodes in parallel, tolerating a
    /// per-node failure (reported as an error case, never fatal — a node
    /// that lacks a method must not abort the whole comparison run), then
    /// hands the two results to the matching comparison engine and records
    /// the outcome on the shared <see cref="ReportWriter"/>.
    /// </summary>
    public sealed class ParityRunner
    {
        private readonly ReportWriter _report;
        private readonly ISet<string> _ignore;

        public ParityRunner(ReportWriter report, ISet<string> ignore)
        {
            _report = report;
            _ignore = ignore;
        }

        public async Task RunAsync<T>(string group, string method, string input, Func<Task<T>> callX, Func<Task<T>> callY)
        {
            var taskX = SafeCallAsync(callX);
            var taskY = SafeCallAsync(callY);
            await Task.WhenAll(taskX, taskY).ConfigureAwait(false);
            var (xValue, xError) = taskX.Result;
            var (yValue, yError) = taskY.Result;

            _report.Record(BuildResult(group, method, input, xError, yError,
                () => DeepComparer.Compare(xValue, yValue, _ignore)));
        }

        public async Task RunTraceAsync(string group, string method, string input, Func<Task<JToken>> callX, Func<Task<JToken>> callY)
        {
            var taskX = SafeCallAsync(callX);
            var taskY = SafeCallAsync(callY);
            await Task.WhenAll(taskX, taskY).ConfigureAwait(false);
            var (xValue, xError) = taskX.Result;
            var (yValue, yError) = taskY.Result;

            _report.Record(BuildResult(group, method, input, xError, yError,
                () => JTokenComparer.Compare(xValue, yValue)));
        }

        private static CaseResult BuildResult(string group, string method, string input, string xError, string yError, Func<List<Diff>> compare)
        {
            switch (ClassifyErrorVerdict(xError, yError))
            {
                case Verdict.Match:
                    return CaseResult.Match(group, method, input, BothErroredNote(xError, yError));
                case Verdict.Error:
                    return CaseResult.ErrorResult(group, method, input, DescribeError(xError, yError));
            }

            var diffs = compare();
            return diffs.Count == 0
                ? CaseResult.Match(group, method, input)
                : CaseResult.DiffResult(group, method, input, diffs);
        }

        // Both nodes rejecting the same call is agreement on behaviour, even
        // when the error message text differs (e.g. "execution reverted:
        // revert" vs "execution reverted") — only one side erroring is a
        // real divergence. Returns null when neither side errored, meaning
        // the caller still needs to run the value comparison.
        internal static Verdict? ClassifyErrorVerdict(string xError, string yError)
        {
            if (xError != null && yError != null) return Verdict.Match;
            if (xError != null || yError != null) return Verdict.Error;
            return null;
        }

        private static string BothErroredNote(string xError, string yError) =>
            $"both errored (X: {xError} | Y: {yError})";

        // ClassifyErrorVerdict only routes here when exactly one side errored.
        private static string DescribeError(string xError, string yError) =>
            xError != null ? $"X: {xError}" : $"Y: {yError}";

        private static async Task<(T Value, string Error)> SafeCallAsync<T>(Func<Task<T>> call)
        {
            try
            {
                var value = await call().ConfigureAwait(false);
                return (value, null);
            }
            catch (Exception ex)
            {
                return (default, ex.Message);
            }
        }
    }
}
