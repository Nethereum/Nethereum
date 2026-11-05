using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Nethereum.Model.UnitTests.Oracle;
using Nethereum.RLP;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    public sealed class DiffInput
    {
        public string Kind { get; set; }
        public byte[] Bytes { get; set; }
        public string Origin { get; set; }
    }

    public sealed class InputPlanner
    {
        private const int MutantsPerSeed = 5;

        private static readonly Dictionary<string, double> Weights = new Dictionary<string, double>
        {
            ["tx_binary"] = 9, ["tx_rlp"] = 7, ["header"] = 9, ["block"] = 3, ["body"] = 4, ["withdrawal"] = 2, ["log"] = 2,
            ["receipt_binary"] = 3, ["receipt_rlp"] = 3, ["receipt_eth69"] = 4, ["bal"] = 6,
            ["eth_Transactions"] = 3, ["eth_PooledTransactions"] = 3, ["eth_BlockHeaders"] = 3, ["eth_BlockBodies"] = 3,
            ["eth_Receipts69"] = 2.5, ["eth_Receipts70"] = 2.5, ["eth_BlockAccessLists"] = 2, ["eth_NewPooledTransactionHashes"] = 1.5,
            ["eth_Status"] = 1, ["eth_BlockRangeUpdate"] = 1, ["eth_GetBlockHeaders"] = 1, ["eth_GetBlockBodies"] = 1,
            ["eth_GetPooledTransactions"] = 1, ["eth_GetReceipts69"] = 1, ["eth_GetReceipts70"] = 1, ["eth_GetBlockAccessLists"] = 1,
            ["snap_GetAccountRange"] = 1, ["snap_AccountRange"] = 2, ["snap_GetStorageRanges"] = 1, ["snap_StorageRanges"] = 2,
            ["snap_GetByteCodes"] = 1, ["snap_ByteCodes"] = 1.5, ["snap_GetTrieNodes"] = 2, ["snap_TrieNodes"] = 1.5,
            ["snap_GetAccessLists"] = 1, ["snap_AccessLists"] = 1.5
        };

        public static IReadOnlyCollection<string> Kinds => Weights.Keys;

        private readonly int _total;
        private readonly Random _random;
        private readonly ObjectGenerator _generator;
        private readonly List<RlpNode> _corpusTransactions;
        private readonly List<RlpNode> _corpusHeaders;
        private readonly List<RlpNode> _corpusBlocks;

        private static int PerKindCap(int total) => Math.Max(50, total / 8);

        private static int BlockCap(int total) => Math.Max(20, total / 40);

        public static CorpusBounds BoundsFor(int total) => new CorpusBounds
        {
            Transactions = PerKindCap(total) * 2,
            Headers = PerKindCap(total) * 2,
            Blocks = BlockCap(total) * 2
        };

        public InputPlanner(int total, int seed, DifferentialCorpus corpus)
        {
            _total = total;
            _random = new Random(seed);
            _generator = new ObjectGenerator(_random);
            _corpusTransactions = Sample(corpus.Transactions, PerKindCap(total), TransactionTree);
            _corpusHeaders = Sample(corpus.Headers, PerKindCap(total), ListTree);
            _corpusBlocks = Sample(corpus.BlockRlps, BlockCap(total), ListTree);
        }

        private static List<RlpNode> Sample(IEnumerable<CorpusSample> samples, int count, Func<byte[], RlpNode> toTree)
        {
            var ordered = samples.OrderBy(s => Convert.ToHexString(SHA256.HashData(s.Bytes), 0, 8)).Take(count * 2);
            var trees = new List<RlpNode>();
            foreach (var sample in ordered)
            {
                var tree = toTree(sample.Bytes);
                if (tree != null) trees.Add(tree);
                if (trees.Count == count) break;
            }
            return trees;
        }

        private static RlpNode ListTree(byte[] bytes) =>
            RlpNode.TryParse(bytes, out var node) && node.IsList ? node : null;

        private static RlpNode TransactionTree(byte[] bytes)
        {
            if (bytes.Length == 0) return null;
            if (bytes[0] >= 0x80 && bytes[0] < 0xc0)
            {
                if (!RlpNode.TryParse(bytes, out var wrapper) || wrapper.IsList) return null;
                bytes = wrapper.Data;
                if (bytes.Length == 0) return null;
            }
            if (bytes[0] >= 0xc0) return ListTree(bytes);
            if (!RlpNode.TryParse(bytes.Skip(1).ToArray(), out var body)) return null;
            return RlpNode.Typed(bytes[0], body);
        }

        public IEnumerable<DiffInput> Plan()
        {
            var weightSum = Weights.Values.Sum();
            foreach (var kind in Weights.Keys)
            {
                var budget = Math.Max(30, (int)(_total * Weights[kind] / weightSum));
                foreach (var input in PlanKind(kind, budget)) yield return input;
            }
        }

        private IEnumerable<DiffInput> PlanKind(string kind, int budget)
        {
            var binary = kind == "tx_binary" || kind == "receipt_binary";
            var seedCount = Math.Max(5, budget / (1 + MutantsPerSeed));
            var emitted = 0;
            foreach (var seedTree in Seeds(kind, seedCount))
            {
                yield return Input(kind, Encode(kind, seedTree), "seed");
                emitted++;
                foreach (var (site, bytes) in StructuralMutator.Sample(seedTree, MutantsPerSeed, _random, binary))
                {
                    yield return Input(kind, bytes, "mutant:" + site);
                    emitted++;
                }
                if (emitted >= budget) break;
            }

            if (kind == "tx_binary" || kind == "tx_rlp" || kind == "receipt_binary" || kind == "receipt_rlp")
                foreach (var input in TypeByteSweeps(kind)) yield return input;
        }

        private static DiffInput Input(string kind, byte[] bytes, string origin) =>
            new DiffInput { Kind = kind, Bytes = bytes, Origin = origin };

        private static byte[] Encode(string kind, RlpNode tree) =>
            kind == "tx_binary" || kind == "receipt_binary" ? tree.EncodeBinary() : tree.Encode();

        private IEnumerable<DiffInput> TypeByteSweeps(string kind)
        {
            var isTx = kind.StartsWith("tx");
            var bodies = isTx
                ? new[] { _generator.Tx2930().TypedBody, _generator.Tx1559().TypedBody, _generator.Tx4844().TypedBody, _generator.Tx7702().TypedBody }
                : new[] { _generator.ReceiptBody(), _generator.ReceiptBody() };
            var binary = kind.EndsWith("binary");
            foreach (var body in bodies)
                for (var type = 0; type < 256; type++)
                {
                    var node = RlpNode.Typed((byte)type, body);
                    yield return Input(kind, binary ? node.EncodeBinary() : node.Encode(), "type-byte:" + type);
                }
        }

        private T Pick<T>(List<T> pool) => pool[_random.Next(pool.Count)];

        private RlpNode CorpusOrGenerated(List<RlpNode> pool, Func<RlpNode> generate) =>
            pool.Count > 0 && _random.Next(2) == 0 ? Pick(pool).Clone() : generate();

        private RlpNode Transaction() => CorpusOrGenerated(_corpusTransactions, _generator.AnyTransaction);

        private RlpNode Header() => CorpusOrGenerated(_corpusHeaders, _generator.AnyHeader);

        private RlpNode BodyFromCorpusOrGenerated()
        {
            if (_corpusBlocks.Count > 0 && _random.Next(2) == 0)
            {
                var block = Pick(_corpusBlocks);
                if (block.Items.Count >= 3) return RlpNode.List(block.Items.Skip(1).Select(i => i.Clone()));
            }
            return _generator.Body(_random.Next(2) == 0);
        }

        private IEnumerable<RlpNode> Seeds(string kind, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var seed = Seed(kind, i);
                if (seed != null) yield return seed;
            }
        }

        private RlpNode Seed(string kind, int index)
        {
            switch (kind)
            {
                case "tx_binary":
                case "tx_rlp":
                    if (index % 20 == 0) return _generator.Tx4844WithSidecar(index % 40 == 0 ? 0 : 1);
                    return Transaction();
                case "header": return Header();
                case "block": return CorpusOrGenerated(_corpusBlocks, () => _generator.Block(_random.Next(2) == 0));
                case "body": return BodyFromCorpusOrGenerated();
                case "withdrawal": return _generator.Withdrawal();
                case "log": return _generator.Log();
                case "receipt_binary":
                case "receipt_rlp": return _generator.Receipt(new[] { 0, 0, 1, 2, 3, 4 }[index % 6]);
                case "receipt_eth69": return _generator.ReceiptEth69();
                case "bal": return _generator.BlockAccessList();
                case "eth_Transactions": return _generator.TransactionsMessage(Transaction);
                case "eth_PooledTransactions": return _generator.PooledTransactionsMessage(Transaction);
                case "eth_BlockHeaders": return _generator.BlockHeadersMessage(Header);
                case "eth_BlockBodies": return _generator.BlockBodiesMessage(BodyFromCorpusOrGenerated);
                case "eth_Receipts69": return _generator.ReceiptsEth69Message();
                case "eth_Receipts70": return _generator.ReceiptsEth70Message();
                case "eth_BlockAccessLists": return _generator.BlockAccessListsMessage();
                case "eth_NewPooledTransactionHashes": return _generator.NewPooledTransactionHashes();
                case "eth_Status": return _generator.Status69();
                case "eth_BlockRangeUpdate": return _generator.BlockRangeUpdate();
                case "eth_GetBlockHeaders": return _generator.GetBlockHeaders();
                case "eth_GetBlockBodies":
                case "eth_GetPooledTransactions":
                case "eth_GetReceipts69":
                case "eth_GetBlockAccessLists": return _generator.IdAndHashes();
                case "eth_GetReceipts70": return _generator.GetReceipts70();
                case "snap_GetAccountRange": return _generator.GetAccountRange();
                case "snap_AccountRange": return _generator.AccountRange();
                case "snap_GetStorageRanges": return _generator.GetStorageRanges();
                case "snap_StorageRanges": return _generator.StorageRanges();
                case "snap_GetByteCodes": return _generator.GetByteCodes();
                case "snap_ByteCodes": return _generator.ByteCodes();
                case "snap_GetTrieNodes": return _generator.GetTrieNodes();
                case "snap_TrieNodes": return _generator.TrieNodes();
                case "snap_GetAccessLists": return _generator.GetAccessLists();
                case "snap_AccessLists": return _generator.AccessLists();
                default: throw new InvalidOperationException("no seed for " + kind);
            }
        }
    }
}
