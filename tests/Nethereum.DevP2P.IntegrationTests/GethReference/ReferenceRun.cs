using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Model.UnitTests.Oracle;
using Nethereum.Testing;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    public sealed class KindCounts
    {
        public int Inputs;
        public int AgreeAccept;
        public int AgreeRefuse;
        public int PolicyDeferred;
        public int Disagreements;
        public int Allowed;
    }

    public sealed class AllowlistEntry
    {
        public string Id { get; set; }
        public string Reason { get; set; }
        public string Finding { get; set; }
    }

    public sealed class Disagreement
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Origin { get; set; }
        public string Input { get; set; }
        public string Reason { get; set; }
        public string GethVerdict { get; set; }
        public string GethCanonical { get; set; }
        public string GethHash { get; set; }
        public string NethereumVerdict { get; set; }
        public string NethereumCanonical { get; set; }
        public string NethereumHash { get; set; }
    }

    public sealed class ReferenceRun
    {
        private const int BatchRequests = 400;
        private const int BatchBytes = 6 * 1024 * 1024;

        public const string ResultsFolder = ".testresults/geth-reference";

        public Dictionary<string, KindCounts> Counts { get; } = new Dictionary<string, KindCounts>();
        public List<Disagreement> Disagreements { get; } = new List<Disagreement>();
        public int Total => Counts.Values.Sum(c => c.Inputs);

        public static int ConfiguredInputs() => int.Parse(TestEnvironment.SettingOrNull("GethReference", "Inputs") ?? "200000");

        public static int ConfiguredSeed() => int.Parse(TestEnvironment.SettingOrNull("GethReference", "Seed") ?? "20261028");

        public static TimeSpan ConfiguredLimit() => TimeSpan.FromMinutes(int.Parse(TestEnvironment.SettingOrNull("GethReference", "MaxMinutes") ?? "20"));

        public static List<AllowlistEntry> LoadAllowlist()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "GethReference", "geth-reference-allowlist.json");
            if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "geth-reference-allowlist.json");
            if (!File.Exists(path)) throw new FileNotFoundException("The allowlist file was not copied to the test output: " + path);
            return JsonSerializer.Deserialize<List<AllowlistEntry>>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AllowlistEntry>();
        }

        public static string IdOf(string kind, byte[] input) =>
            kind + ":" + Convert.ToHexString(SHA256.HashData(input), 0, 8).ToLowerInvariant();

        public void Execute(IEnumerable<DiffInput> inputs, GethReferenceProcess geth, HashSet<string> allowed)
        {
            var batch = new List<DiffInput>();
            var batchBytes = 0;
            foreach (var input in inputs)
            {
                batch.Add(input);
                batchBytes += input.Bytes.Length * 2;
                if (batch.Count >= BatchRequests || batchBytes >= BatchBytes)
                {
                    Flush(batch, geth, allowed);
                    batch.Clear();
                    batchBytes = 0;
                }
            }
            if (batch.Count > 0) Flush(batch, geth, allowed);
        }

        private void Flush(List<DiffInput> batch, GethReferenceProcess geth, HashSet<string> allowed)
        {
            var answers = geth.Query(batch.Select(b => new KeyValuePair<string, byte[]>(b.Kind, b.Bytes)).ToList());
            for (var i = 0; i < batch.Count; i++)
            {
                var input = batch[i];
                var counts = CountsFor(input.Kind);
                counts.Inputs++;
                var verdict = NethereumDecoders.ByKind[input.Kind](input.Bytes);
                var result = DecodeComparer.Compare(input.Kind, answers[i], verdict);
                switch (result.Agreement)
                {
                    case Agreement.AgreeAccept: counts.AgreeAccept++; break;
                    case Agreement.AgreeRefuse: counts.AgreeRefuse++; break;
                    case Agreement.PolicyDeferred: counts.PolicyDeferred++; break;
                    default:
                        var id = IdOf(input.Kind, input.Bytes);
                        if (allowed.Contains(id) || (result.Class != null && allowed.Contains(result.Class))) { counts.Allowed++; break; }
                        counts.Disagreements++;
                        Disagreements.Add(Describe(id, input, answers[i], verdict, result.Reason));
                        break;
                }
            }
        }

        private KindCounts CountsFor(string kind)
        {
            if (!Counts.TryGetValue(kind, out var counts)) Counts[kind] = counts = new KindCounts();
            return counts;
        }

        private static Disagreement Describe(string id, DiffInput input, GethAnswer geth, NethereumVerdict nethereum, string reason) =>
            new Disagreement
            {
                Id = id,
                Kind = input.Kind,
                Origin = input.Origin,
                Input = Convert.ToHexString(input.Bytes).ToLowerInvariant(),
                Reason = reason,
                GethVerdict = geth.Ok && geth.ItemsOk ? (geth.PolicyRefusal != null ? "refuse(policy): " + geth.PolicyRefusal : "accept") : "refuse: " + geth.Error,
                GethCanonical = geth.Canonical,
                GethHash = geth.Hash,
                NethereumVerdict = nethereum.Accepted ? "accept" : "refuse: " + nethereum.Error,
                NethereumCanonical = nethereum.Canonical,
                NethereumHash = nethereum.Hash
            };

        public string Table()
        {
            var builder = new StringBuilder();
            builder.AppendLine("kind | inputs | agree-accept | agree-refuse | policy-deferred | disagreements | allowlisted");
            foreach (var kind in Counts.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var c = Counts[kind];
                builder.AppendLine($"{kind} | {c.Inputs} | {c.AgreeAccept} | {c.AgreeRefuse} | {c.PolicyDeferred} | {c.Disagreements} | {c.Allowed}");
            }
            builder.AppendLine($"total | {Counts.Values.Sum(c => c.Inputs)} | {Counts.Values.Sum(c => c.AgreeAccept)} | {Counts.Values.Sum(c => c.AgreeRefuse)} | {Counts.Values.Sum(c => c.PolicyDeferred)} | {Counts.Values.Sum(c => c.Disagreements)} | {Counts.Values.Sum(c => c.Allowed)}");
            return builder.ToString();
        }

        public string WriteResults()
        {
            var folder = Path.Combine(TestEnvironment.RepoRoot, ResultsFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.EnumerateFiles(folder, "*.jsonl")) File.Delete(old);
            File.WriteAllText(Path.Combine(folder, "counts.txt"), Table());
            foreach (var group in Disagreements.GroupBy(d => d.Kind))
                File.WriteAllLines(Path.Combine(folder, group.Key + ".jsonl"), group.Select(d => JsonSerializer.Serialize(d)));
            return folder;
        }
    }
}
