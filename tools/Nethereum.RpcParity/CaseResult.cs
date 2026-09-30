using System;
using System.Collections.Generic;

namespace Nethereum.RpcParity
{
    /// <summary>The outcome of running one method+input pair against both nodes.</summary>
    public sealed class CaseResult
    {
        private CaseResult(string group, string method, string input, Verdict verdict, IReadOnlyList<Diff> diffs, string errorMessage, string note = null)
        {
            Group = group;
            Method = method;
            Input = input;
            Verdict = verdict;
            Diffs = diffs;
            ErrorMessage = errorMessage;
            Note = note;
        }

        public string Group { get; }
        public string Method { get; }
        public string Input { get; }
        public Verdict Verdict { get; }
        public IReadOnlyList<Diff> Diffs { get; }
        public string ErrorMessage { get; }

        /// <summary>
        /// Optional context on a MATCH — e.g. both nodes rejected the call,
        /// which counts as agreement even though the error text differs.
        /// </summary>
        public string Note { get; }

        public static CaseResult Match(string group, string method, string input, string note = null) =>
            new CaseResult(group, method, input, Verdict.Match, Array.Empty<Diff>(), null, note);

        public static CaseResult DiffResult(string group, string method, string input, IReadOnlyList<Diff> diffs) =>
            new CaseResult(group, method, input, Verdict.Diff, diffs, null);

        public static CaseResult ErrorResult(string group, string method, string input, string errorMessage) =>
            new CaseResult(group, method, input, Verdict.Error, Array.Empty<Diff>(), errorMessage);
    }
}
