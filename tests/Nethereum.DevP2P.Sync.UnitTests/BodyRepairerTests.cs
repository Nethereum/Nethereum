using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BodyRepairerTests
    {
        private static readonly byte[] EmptyUnclesHash = DefaultValues.EMPTY_UNCLES_HASH;
        private static readonly byte[] EmptyTrieRoot = DefaultValues.EMPTY_TRIE_HASH;

        [Fact]
        public async Task Given_ValidatedBodiesWithUnclesWithdrawalsAndTransactions_When_Persisted_Then_AllThreeStoresReceiveThem()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var uncles = new List<BlockHeader> { Header(7, new byte[32]) };
            var withdrawals = Withdrawals(3);
            var txs = new List<ISignedTransaction> { Tx(1), Tx(2) };
            var header = BodyHeader(0, txs, uncles, withdrawals);
            var hash = Hash(0);
            await bundle.Blocks.SaveAsync(header, hash);
            var body = new BlockBody { Transactions = txs, Uncles = uncles, Withdrawals = withdrawals };

            var repairer = new BodyRepairer(new BodyScheduler(body), NullLogger.Instance);
            var repaired = await repairer.RepairAsync(0, 0, bundle, CancellationToken.None);

            Assert.True(repaired);
            var savedUncles = await bundle.Uncles.GetByBlockHashAsync(hash);
            Assert.Single(savedUncles);
            var savedWithdrawals = await bundle.Withdrawals.GetByBlockHashAsync(hash);
            Assert.Single(savedWithdrawals);
            Assert.Equal(withdrawals[0].Index, savedWithdrawals[0].Index);
            Assert.Equal(withdrawals[0].AmountInGwei, savedWithdrawals[0].AmountInGwei);
            var savedTxs = await bundle.Transactions.GetByBlockHashAsync(hash);
            Assert.Equal(2, savedTxs.Count);
        }

        [Fact]
        public async Task Given_ABodyRepairerHole_When_RepairAsync_Then_TheBodyIsPersisted()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var withdrawals = Withdrawals(5);
            var header = BodyHeader(0, new List<ISignedTransaction>(), new List<BlockHeader>(), withdrawals);
            var hash = Hash(0);
            await bundle.Blocks.SaveAsync(header, hash);
            Assert.Null(await bundle.Withdrawals.GetByBlockHashAsync(hash));
            var body = new BlockBody
            {
                Transactions = new List<ISignedTransaction>(),
                Uncles = new List<BlockHeader>(),
                Withdrawals = withdrawals,
            };

            var repairer = new BodyRepairer(new BodyScheduler(body), NullLogger.Instance);
            var repaired = await repairer.RepairAsync(0, 0, bundle, CancellationToken.None);

            Assert.True(repaired);
            var saved = await bundle.Withdrawals.GetByBlockHashAsync(hash);
            Assert.Single(saved);
            Assert.Equal(withdrawals[0].ValidatorIndex, saved[0].ValidatorIndex);
        }

        [Fact]
        public async Task Given_ABodyWhoseWithdrawalsDoNotMatchTheHeader_When_RepairAsync_Then_NothingIsPersisted()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var header = BodyHeader(0, new List<ISignedTransaction>(), new List<BlockHeader>(), Withdrawals(5));
            var hash = Hash(0);
            await bundle.Blocks.SaveAsync(header, hash);
            var body = new BlockBody
            {
                Transactions = new List<ISignedTransaction>(),
                Uncles = new List<BlockHeader>(),
                Withdrawals = Withdrawals(6),
            };

            var repairer = new BodyRepairer(new BodyScheduler(body), NullLogger.Instance);
            var repaired = await repairer.RepairAsync(0, 0, bundle, CancellationToken.None);

            Assert.False(repaired);
            Assert.Null(await bundle.Withdrawals.GetByBlockHashAsync(hash));
        }

        private static BlockHeader BodyHeader(
            long number, List<ISignedTransaction> txs, List<BlockHeader> uncles, List<Withdrawal> withdrawals)
        {
            var header = Header(number, new byte[32]);
            header.TransactionsHash = txs.Count == 0
                ? EmptyTrieRoot
                : PatriciaBlockRootsProvider.Instance.CalculateTransactionsRoot(txs);
            header.UnclesHash = uncles.Count == 0 ? EmptyUnclesHash : UnclesHash(uncles);
            header.WithdrawalsRoot = PatriciaBlockRootsProvider.Instance.CalculateWithdrawalsRoot(withdrawals);
            return header;
        }

        private static byte[] UnclesHash(List<BlockHeader> uncles)
        {
            var encoded = new byte[uncles.Count][];
            for (int i = 0; i < uncles.Count; i++)
                encoded[i] = BlockHeaderEncoder.Current.Encode(uncles[i]);
            return new Sha3Keccack().CalculateHash(Nethereum.RLP.RLP.EncodeList(encoded));
        }

        private static List<Withdrawal> Withdrawals(ulong seed) => new List<Withdrawal>
        {
            new Withdrawal { Index = seed, ValidatorIndex = seed + 10, Address = new byte[20], AmountInGwei = seed * 100 },
        };

        private static ISignedTransaction Tx(byte seed)
        {
            var receiver = new byte[20];
            receiver[0] = seed;
            return new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: receiver,
                value: new byte[] { },
                data: new byte[0]);
        }

        private static BlockHeader Header(long number, byte[] parentHash) => new BlockHeader
        {
            BlockNumber = new EvmUInt256((ulong)number),
            ParentHash = (byte[])parentHash.Clone(),
            TransactionsHash = (byte[])EmptyTrieRoot.Clone(),
            UnclesHash = (byte[])EmptyUnclesHash.Clone(),
            ReceiptHash = (byte[])EmptyTrieRoot.Clone(),
            StateRoot = new byte[32],
            Difficulty = new EvmUInt256(1UL),
            GasLimit = 1,
            Timestamp = 1,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };

        private static byte[] Hash(long number)
        {
            var hash = new byte[32];
            BitConverter.GetBytes(number + 1).CopyTo(hash, 0);
            return hash;
        }

        private sealed class BodyScheduler : IFetchRequestScheduler
        {
            private readonly BlockBody _body;

            public BodyScheduler(BlockBody body)
            {
                _body = body;
            }

            public Task<BodyFetchResult> FetchBodiesAsync(
                IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid>? excludePeers, CancellationToken ct)
                => Task.FromResult(new BodyFetchResult(new List<BlockBody> { _body }, new HashSet<Guid> { Guid.Empty }));

            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
                => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }
    }
}
