using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Model.UnitTests.Oracle
{
    public sealed class DifferentialHarness
    {
        public const string NonCanonical = "NONCANONICAL";

        private readonly ITestOutputHelper _output;

        public DifferentialHarness(ITestOutputHelper output)
        {
            _output = output;
        }

        public sealed class Outcome
        {
            public int Samples;
            public int BothAcceptEqual;
            public int BothReject;
            public int Tightened;
            public readonly List<string> Mismatches = new List<string>();
            public readonly List<string> AcceptedNonCanonical = new List<string>();
            public readonly List<string> NewAccepts = new List<string>();
            public readonly List<string> OldInt64Overflow = new List<string>();
            public readonly List<string> TightenedValid = new List<string>();
            public readonly Dictionary<string, int> TightenedKinds = new Dictionary<string, int>();
            public readonly Dictionary<string, string> TightenedSamples = new Dictionary<string, string>();
        }

        public static string Kind(Exception e)
        {
            var message = Regex.Replace(e.Message, "[0-9]+", "N");
            if (message.Length > 70) message = message.Substring(0, 70);
            return e.GetType().Name + ": " + message;
        }

        private static string Describe(CorpusSample sample, int hexChars)
        {
            return sample.Source + " 0x" + sample.Bytes.ToHex().Substring(0, Math.Min(hexChars, sample.Bytes.Length * 2));
        }

        public void Run<T>(IEnumerable<CorpusSample> samples, Func<byte[], T> oracle, Func<byte[], T> decoder,
            Func<T, T, byte[], string> compare, Outcome outcome)
        {
            foreach (var sample in samples)
            {
                outcome.Samples++;
                T expected = default(T), actual = default(T);
                Exception oldFault = null, newFault = null;
                try { expected = oracle(sample.Bytes); } catch (Exception e) { oldFault = e; }
                try { actual = decoder(sample.Bytes); } catch (Exception e) { newFault = e; }

                if (oldFault == null && newFault == null)
                {
                    var difference = compare(expected, actual, sample.Bytes);
                    if (difference == null) outcome.BothAcceptEqual++;
                    else if (difference == NonCanonical) outcome.AcceptedNonCanonical.Add(Describe(sample, 80));
                    else outcome.Mismatches.Add(Describe(sample, 80) + " " + difference);
                }
                else if (oldFault != null && newFault != null)
                {
                    outcome.BothReject++;
                }
                else if (oldFault != null)
                {
                    var line = Describe(sample, 80) + " old: " + oldFault.GetType().Name + ": " + oldFault.Message;
                    if (oldFault is OverflowException) outcome.OldInt64Overflow.Add(line);
                    else outcome.NewAccepts.Add(line);
                }
                else
                {
                    outcome.Tightened++;
                    var kind = Kind(newFault);
                    outcome.TightenedKinds[kind] = outcome.TightenedKinds.TryGetValue(kind, out var n) ? n + 1 : 1;
                    if (!outcome.TightenedSamples.ContainsKey(kind))
                        outcome.TightenedSamples[kind] = Describe(sample, 120);
                    if (sample.MustBeValid)
                        outcome.TightenedValid.Add(Describe(sample, 80) + " " + Kind(newFault));
                }
            }
        }

        public string Report(string name, Outcome outcome)
        {
            var builder = new StringBuilder();
            builder.AppendLine(name + ": samples=" + outcome.Samples + " bothAcceptEqual=" + outcome.BothAcceptEqual
                               + " bothReject=" + outcome.BothReject + " tightened=" + outcome.Tightened
                               + " mismatches=" + outcome.Mismatches.Count + " newAcceptsOldReject=" + outcome.NewAccepts.Count
                               + " tightenedOnValidInput=" + outcome.TightenedValid.Count + " acceptedNonCanonical=" + outcome.AcceptedNonCanonical.Count
                               + " oldInt64OverflowNowAccepted=" + outcome.OldInt64Overflow.Count);
            foreach (var m in outcome.AcceptedNonCanonical.Take(30)) builder.AppendLine("  NONCANONICAL " + m);
            foreach (var kind in outcome.TightenedKinds.OrderByDescending(k => k.Value))
                builder.AppendLine("  tightened " + kind.Value + " x " + kind.Key + "  e.g. " + outcome.TightenedSamples[kind.Key]);
            foreach (var m in outcome.Mismatches.Take(10)) builder.AppendLine("  MISMATCH " + m);
            foreach (var m in outcome.NewAccepts.Take(10)) builder.AppendLine("  NEWACCEPTS " + m);
            foreach (var m in outcome.TightenedValid.Take(10)) builder.AppendLine("  TIGHTENEDVALID " + m);
            var text = builder.ToString();
            _output.WriteLine(text);
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "differential-summary.txt"), text);
            return text;
        }

        public static void AssertEquivalent(Outcome outcome, string text)
        {
            Assert.True(outcome.Samples > 0, "no samples");
            Assert.True(outcome.Mismatches.Count == 0, text);
            Assert.True(outcome.NewAccepts.Count == 0, text);
            Assert.True(outcome.TightenedValid.Count == 0, text);
            Assert.True(outcome.AcceptedNonCanonical.Count == 0, text);
        }
    }
}
