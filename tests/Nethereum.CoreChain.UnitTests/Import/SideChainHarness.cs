using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain;
using Nethereum.DevChain.Composition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.UnitTests.Import
{
    internal sealed class ProducedBlock
    {
        public BlockHeader Header { get; init; }
        public byte[] Hash { get; init; }
        public IList<ISignedTransaction> Transactions { get; init; }
        public IList<Withdrawal> Withdrawals { get; init; }
    }

    internal sealed class SideChainHarness : IDisposable
    {
        public const string RecipientA = "0x00000000000000000000000000000000000000aa";
        public const string RecipientB = "0x00000000000000000000000000000000000000bb";
        private const string Coinbase = "0x0000000000000000000000000000000000009999";

        private SideChainHarness(
            DevChainNode node, DevChainChainStoreBundle bundle, BlockImporter importer, SideChainSupport sideChain)
        {
            Node = node;
            Bundle = bundle;
            Importer = importer;
            SideChain = sideChain;
        }

        public DevChainNode Node { get; }
        public DevChainChainStoreBundle Bundle { get; }
        public BlockImporter Importer { get; }
        public SideChainSupport SideChain { get; }

        public static async Task<SideChainHarness> CreateAsync(
            bool withSideChain = true, int maxReorgDepth = SideChainSupport.DefaultMaxReorgDepth, DevChainConfig config = null,
            bool adoptByDifficulty = false)
        {
            config ??= new DevChainConfig { Hardfork = "cancun", AutoMine = false };
            var bundle = DevChainChainStoreBundle.OpenInMemory();
            var node = new DevChainNode(
                config, bundle.Blocks, bundle.Transactions, bundle.Receipts, bundle.Logs, bundle.State,
                new InMemoryFilterStore(), bundle.TrieNodes, blobStore: null, bundle.BlockAccessLists, bundle.Uncles);
            await node.StartAsync(new[] { BlockPipelineHarness.SenderAddress });

            var sideChain = withSideChain
                ? new SideChainSupport(
                    bundle.Blocks, new RewindCoordinator(bundle), bundle.Metadata, adoptByDifficulty, maxReorgDepth)
                : null;
            var importer = new BlockImporter(
                node.BlockManager.Engine, node.Blocks, node.State, node.Transactions, node.Receipts, node.Logs,
                bundle.Uncles, logger: null, nodeCommitBlockContext: null, atomicFlush: null, flushCadence: null,
                bundle.BlockAccessLists, bundle.Withdrawals, sideChain);
            return new SideChainHarness(node, bundle, importer, sideChain);
        }

        public async Task<ProducedBlock> ProduceAsync(
            string withdrawalRecipient, long secondsAfterParent = 12, List<ISignedTransaction> transactions = null,
            bool withWithdrawals = true)
        {
            var head = await Node.Blocks.GetLatestAsync();
            var number = (ulong)head.BlockNumber + 1;
            var withdrawals = !withWithdrawals ? null : new List<Withdrawal>
            {
                new Withdrawal
                {
                    Index = number,
                    ValidatorIndex = 1,
                    Address = withdrawalRecipient.HexToByteArray(),
                    AmountInGwei = 1
                }
            };

            var result = await Node.BlockManager.BlockProducer.ProduceBlockAsync(
                transactions ?? new List<ISignedTransaction>(),
                new BlockProductionOptions
                {
                    Timestamp = head.Timestamp + secondsAfterParent,
                    Coinbase = Coinbase,
                    BaseFee = Node.Config.BaseFee,
                    BlockGasLimit = Node.Config.BlockGasLimit,
                    Difficulty = 0,
                    PrevRandao = new byte[32],
                    ExtraData = Array.Empty<byte>(),
                    ChainId = Node.Config.ChainId,
                    ParentBeaconBlockRoot = new byte[32],
                    Withdrawals = withdrawals
                });

            return new ProducedBlock
            {
                Header = result.Header,
                Hash = result.BlockHash,
                Transactions = result.IncludedTransactions,
                Withdrawals = withdrawals
            };
        }

        public async Task<List<ProducedBlock>> ProduceChainAsync(string withdrawalRecipient, int count, bool withWithdrawals = true)
        {
            var blocks = new List<ProducedBlock>();
            for (var i = 0; i < count; i++)
                blocks.Add(await ProduceAsync(withdrawalRecipient, withWithdrawals: withWithdrawals));
            return blocks;
        }

        public Task<BlockImporterResult> ImportAsync(ProducedBlock block) =>
            Importer.ImportAsync(block.Header, block.Transactions, uncles: null, block.Withdrawals, CancellationToken.None);

        public async Task<BigInteger> BalanceOfAsync(string address)
        {
            var account = await Node.State.GetAccountAsync(address);
            return account == null ? BigInteger.Zero : (BigInteger)account.Balance;
        }

        public async Task<BigInteger> StorageAtAsync(string address, BigInteger slot)
        {
            var stored = await Node.State.GetStorageAsync(address, slot);
            return stored == null || stored.Length == 0 ? BigInteger.Zero : new BigInteger(stored, isUnsigned: true, isBigEndian: true);
        }

        public async Task<byte[]> HeadHashAsync()
        {
            var head = await Node.Blocks.GetLatestAsync();
            return await Node.Blocks.GetHashByNumberAsync((BigInteger)head.BlockNumber);
        }

        public void Dispose() => Node.Dispose();
    }
}
