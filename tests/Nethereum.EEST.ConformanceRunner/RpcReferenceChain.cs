using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Genesis;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Sync;
using Nethereum.DevChain.Rpc;
using Nethereum.Model;
using Nethereum.RLP;
using Newtonsoft.Json.Linq;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class RpcReferenceChain : IAsyncDisposable
    {
        public RpcDispatcher Dispatcher { get; }
        public int ImportedBlocks { get; }
        public int ExpectedBlocks { get; }
        private readonly FullNodeHarness _harness;

        public Nethereum.CoreChain.Storage.IStateStore State => _harness.Node.State;

        private RpcReferenceChain(FullNodeHarness harness, RpcDispatcher dispatcher, int imported, int expected)
        {
            _harness = harness;
            Dispatcher = dispatcher;
            ImportedBlocks = imported;
            ExpectedBlocks = expected;
        }

        public static async Task<RpcReferenceChain> CreateAsync()
        {
            var genesisPath = Path.Combine(FixtureProvisioning.ExecutionApisTestsRoot, "genesis.json");
            var chainRlpPath = Path.Combine(FixtureProvisioning.ExecutionApisTestsRoot, "chain.rlp");

            var document = StandardGenesisLoader.Parse(JObject.Parse(File.ReadAllText(genesisPath)));
            var schedule = StandardGenesisLoader.BuildForkSchedule(document);

            var harness = await FullNodeHarness.ComposeAsync(
                document, schedule, EestBlockchainTestsRlpDriver.FixtureRegistry).ConfigureAwait(false);

            var blocks = DecodeConsecutiveBlocks(File.ReadAllBytes(chainRlpPath));
            var imported = 0;
            foreach (var block in blocks)
            {
                var result = await harness.RlpImporter.ImportAsync(
                    BlockHeaderEncoder.Current.Decode(block[0].RLPData),
                    DecodeTransactions(block),
                    DecodeUncles(block),
                    DecodeWithdrawals(block),
                    CancellationToken.None).ConfigureAwait(false);

                if (result.BlockHash == null || !result.RootMatches) break;
                imported++;
            }

            var chainId = document.Config?.ChainId ?? BigInteger.One;
            var context = new RpcContext(harness.Node, chainId, new EmptyServiceProvider());
            var registry = DevRpcHandlerExtensions.CreateDevChainRegistry();
            var dispatcher = new RpcDispatcher(registry, context);

            return new RpcReferenceChain(harness, dispatcher, imported, blocks.Count);
        }

        private static List<RLPCollection> DecodeConsecutiveBlocks(byte[] bytes)
        {
            var result = new List<RLPCollection>();
            if (Nethereum.RLP.RLP.DecodeCollection(bytes) is not RLPCollection stream) return result;
            foreach (var element in stream)
                if (element is RLPCollection block) result.Add(block);
            return result;
        }

        private static IList<ISignedTransaction> DecodeTransactions(RLPCollection block)
        {
            var result = new List<ISignedTransaction>();
            if (block.Count < 2 || block[1] is not RLPCollection txList) return result;

            foreach (var txItem in txList)
            {
                var txBytes = txItem.RLPData;
                if ((txBytes == null || txBytes.Length == 0) && txItem is RLPCollection legacyFields)
                    txBytes = ReEncodeLegacyFields(legacyFields);

                if (txBytes != null && txBytes.Length > 0)
                    result.Add(TransactionFactory.CreateTransaction(txBytes, allowBlobNetworkWrapper: false));
            }

            return result;
        }

        private static byte[] ReEncodeLegacyFields(RLPCollection fields)
        {
            var encoded = new byte[fields.Count][];
            for (var i = 0; i < fields.Count; i++)
                encoded[i] = Nethereum.RLP.RLP.EncodeElement(fields[i].RLPData);
            return Nethereum.RLP.RLP.EncodeList(encoded);
        }

        private static IList<BlockHeader> DecodeUncles(RLPCollection block)
        {
            if (block.Count < 3 || block[2] is not RLPCollection uncleList || uncleList.Count == 0)
                return null;

            var result = new List<BlockHeader>(uncleList.Count);
            foreach (var uncle in uncleList)
                result.Add(BlockHeaderEncoder.Current.Decode(uncle.RLPData));
            return result;
        }

        private static IList<Withdrawal> DecodeWithdrawals(RLPCollection block)
        {
            if (block.Count < 4 || block[3] is not RLPCollection withdrawalList)
                return null;

            var withdrawals = new List<Withdrawal>(withdrawalList.Count);
            foreach (var withdrawal in withdrawalList)
                withdrawals.Add(WithdrawalEncoder.Current.Decode(withdrawal.RLPData));

            return withdrawals;
        }

        public ValueTask DisposeAsync() => _harness.DisposeAsync();

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType) => null;
        }
    }
}
