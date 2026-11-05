using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;

namespace Nethereum.Model.UnitTests.Oracle
{
    public sealed class CorpusSample
    {
        public byte[] Bytes { get; set; }
        public bool MustBeValid { get; set; }
        public string Source { get; set; }
    }

    public sealed class CorpusBounds
    {
        public int Transactions { get; set; }
        public int Headers { get; set; }
        public int Blocks { get; set; }
    }

    internal sealed class SampleSet
    {
        private readonly int _bound;
        private readonly object _gate = new object();
        private readonly Dictionary<string, CorpusSample> _byHex = new Dictionary<string, CorpusSample>();
        private readonly SortedSet<(string Rank, string Hex)> _ranks = new SortedSet<(string, string)>();

        public SampleSet(int bound)
        {
            _bound = bound;
        }

        public IEnumerable<CorpusSample> Values
        {
            get { lock (_gate) return _byHex.Values.ToList(); }
        }

        public void Add(byte[] bytes, bool valid, string file)
        {
            var key = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            var rank = key.Substring(0, 16);
            lock (_gate)
            {
                if (_byHex.TryGetValue(key, out var existing))
                {
                    if (valid) existing.MustBeValid = true;
                    return;
                }

                if (_bound > 0 && _byHex.Count >= _bound && string.CompareOrdinal(rank, _ranks.Max.Rank) >= 0) return;

                _byHex[key] = new CorpusSample { Bytes = bytes, MustBeValid = valid, Source = file };
                if (_bound <= 0) return;

                _ranks.Add((rank, key));
                if (_byHex.Count > _bound)
                {
                    var evicted = _ranks.Max;
                    _ranks.Remove(evicted);
                    _byHex.Remove(evicted.Hex);
                }
            }
        }
    }

    public sealed class DifferentialCorpus
    {
        public List<CorpusSample> Transactions { get; } = new List<CorpusSample>();
        public List<CorpusSample> Headers { get; } = new List<CorpusSample>();
        public List<CorpusSample> BlockRlps { get; } = new List<CorpusSample>();
        public int Files { get; set; }
        public int Blocks { get; set; }
        public int GethBlocks { get; set; }

        public static string RootOverride { get; set; }

        public static string ExternalRoot()
        {
            if (RootOverride != null) return RootOverride;
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "external");
                if (Directory.Exists(Path.Combine(candidate, "ethereum-tests")) || Directory.Exists(Path.Combine(candidate, "eest")))
                    return candidate;
                directory = directory.Parent;
            }
            return null;
        }

        private const long LargeFileBytes = 8L * 1024 * 1024;

        private static readonly string[] Roots =
        {
            "ethereum-tests/TransactionTests",
            "ethereum-tests/BlockchainTests",
            "eest",
            "execution-spec-tests",
            "legacytests"
        };

        public static DifferentialCorpus Load() => Load(null);

        public static DifferentialCorpus Load(CorpusBounds bounds)
        {
            var external = ExternalRoot();
            if (external == null)
                throw new InvalidOperationException("The external fixtures are missing: follow external/README.md.");

            var files = new List<string>();
            foreach (var root in Roots)
            {
                var path = Path.Combine(external, root);
                if (!Directory.Exists(path)) continue;
                files.AddRange(Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories).Where(Wanted));
            }

            var transactions = new SampleSet(bounds?.Transactions ?? 0);
            var headers = new SampleSet(bounds?.Headers ?? 0);
            var blockSamples = new SampleSet(bounds?.Blocks ?? 0);
            var blocks = 0;
            void Process(string file)
            {
                try
                {
                    using (var document = JsonDocument.Parse(File.ReadAllBytes(file), new JsonDocumentOptions { MaxDepth = 256 }))
                    {
                        var local = 0;
                        Walk(document.RootElement, file, transactions, headers, blockSamples, ref local);
                        System.Threading.Interlocked.Add(ref blocks, local);
                    }
                }
                catch (JsonException)
                {
                }
            }

            foreach (var file in files.Where(f => new FileInfo(f).Length > LargeFileBytes))
                Process(file);

            var ordinary = files.Where(f => new FileInfo(f).Length <= LargeFileBytes).ToList();
            var options = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = System.Math.Min(Environment.ProcessorCount, 4) };
            System.Threading.Tasks.Parallel.ForEach(ordinary, options, Process);

            var gethBlocks = 0;
            var gethChain = GethChainRlp(external);
            if (gethChain != null)
            {
                var data = File.ReadAllBytes(gethChain);
                var offset = 0;
                while (offset < data.Length)
                {
                    var length = RLP.RLP.GetFirstElementLength(data, offset);
                    var block = new byte[length];
                    Array.Copy(data, offset, block, 0, length);
                    AddBlock(block.ToHex(true), true, gethChain, transactions, headers, blockSamples);
                    offset += length;
                    gethBlocks++;
                }
            }

            var corpus = new DifferentialCorpus { Files = files.Count, Blocks = blocks, GethBlocks = gethBlocks };
            corpus.Transactions.AddRange(transactions.Values);
            corpus.Headers.AddRange(headers.Values);
            corpus.BlockRlps.AddRange(blockSamples.Values);
            return corpus;
        }

        private static string GethChainRlp(string external)
        {
            var relative = Path.Combine("cmd", "devp2p", "internal", "ethtest", "testdata", "chain.rlp");
            foreach (var root in new[]
                     {
                         Path.Combine(external, "go-ethereum"),
                         Path.Combine(Directory.GetParent(external).Parent.FullName, "go-ethereum")
                     })
            {
                var candidate = Path.Combine(root, relative);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static bool Wanted(string file)
        {
            var normalised = file.Replace('\\', '/');
            if (normalised.Contains("/.git/")) return false;
            if (normalised.Contains("/blockchain_tests_engine")) return false;
            return normalised.Contains("/TransactionTests/")
                   || normalised.Contains("/transaction_tests/")
                   || normalised.Contains("/BlockchainTests/")
                   || normalised.Contains("/blockchain_tests/")
                   || normalised.Contains("/blockchain_tests_sync/");
        }

        private static void Walk(JsonElement element, string file,
            SampleSet transactions,
            SampleSet headers,
            SampleSet blockSamples, ref int blocks)
        {
            if (element.ValueKind != JsonValueKind.Object) return;

            foreach (var test in element.EnumerateObject())
            {
                var body = test.Value;
                if (body.ValueKind != JsonValueKind.Object) continue;

                if (body.TryGetProperty("txbytes", out var txBytes) && txBytes.ValueKind == JsonValueKind.String)
                {
                    var valid = body.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object
                                && result.EnumerateObject().Any(f => f.Value.ValueKind == JsonValueKind.Object
                                                                     && !f.Value.TryGetProperty("exception", out _));
                    Add(transactions, txBytes.GetString(), valid, file);
                }

                if (body.TryGetProperty("genesisRLP", out var genesis) && genesis.ValueKind == JsonValueKind.String)
                {
                    blocks++;
                    AddBlock(genesis.GetString(), true, file, transactions, headers, blockSamples);
                }

                if (body.TryGetProperty("blocks", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var block in list.EnumerateArray())
                    {
                        if (block.ValueKind != JsonValueKind.Object) continue;
                        if (!block.TryGetProperty("rlp", out var rlp) || rlp.ValueKind != JsonValueKind.String) continue;
                        blocks++;
                        var valid = !block.TryGetProperty("expectException", out _) && block.TryGetProperty("blockHeader", out _);
                        AddBlock(rlp.GetString(), valid, file, transactions, headers, blockSamples);
                    }
                }
            }
        }

        private static void AddBlock(string hex, bool valid, string file,
            SampleSet transactions,
            SampleSet headers,
            SampleSet blockSamples)
        {
            RLPCollection block;
            try
            {
                block = RLP.RLP.Decode(hex.HexToByteArray()) as RLPCollection;
            }
            catch
            {
                return;
            }

            if (block == null || block.Count < 3) return;
            Add(blockSamples, block.RLPData.ToHex(true), valid, file);
            if (block[0] is RLPCollection header) Add(headers, header.RLPData.ToHex(true), valid, file);
            if (block[1] is RLPCollection txs)
            {
                foreach (var tx in txs)
                    if (tx.RLPData != null) Add(transactions, tx.RLPData.ToHex(true), valid, file);
            }
            if (block[2] is RLPCollection uncles)
            {
                foreach (var uncle in uncles)
                    if (uncle is RLPCollection u) Add(headers, u.RLPData.ToHex(true), valid, file);
            }
        }

        private static void Add(SampleSet into, string hex, bool valid, string file)
        {
            if (string.IsNullOrEmpty(hex)) return;
            byte[] bytes;
            try
            {
                bytes = hex.HexToByteArray();
            }
            catch
            {
                return;
            }

            into.Add(bytes, valid, file);
        }
    }
}
