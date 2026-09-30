using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class ReorgHistoryTruncationTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private readonly RocksDbChainStoreBundle _bundle;
        private readonly HistoricalStateStore _hist;
        private readonly EthECKey _key = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private int _nonce;

        private const string ContractAddr = "0x00000000000000000000000000000000c0ffee";

        public ReorgHistoryTruncationTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-reorg-history-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            _bundle = RocksDbChainStoreBundle.FromManager(_mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            _hist = (HistoricalStateStore)_bundle.State;
        }

        public void Dispose()
        {
            _bundle.Dispose();
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        [Fact]
        public async Task Reorg_ToShorterChain_TruncatesHistory_NoOrphansNoStaleHashLookups()
        {
            var tx1 = MakeTx();
            await ApplyBlockAsync(1, Hash(1), tx1, withLog: false);
            var tx2 = MakeTx();
            await ApplyBlockAsync(2, Hash(2), tx2, withLog: false);
            var tx3 = MakeTx();
            await ApplyBlockAsync(3, Hash(3), tx3, withLog: false);

            var tx4A = MakeTx();
            await ApplyBlockAsync(4, Hash(0x4A), tx4A, withLog: true);
            var tx5A = MakeTx();
            await ApplyBlockAsync(5, Hash(0x5A), tx5A, withLog: false);
            var tx6A = MakeTx();
            await ApplyBlockAsync(6, Hash(0x6A), tx6A, withLog: false);

            Assert.Equal(6UL, _bundle.Metadata.GetLastBlock());
            Assert.Equal(6, (int)await _bundle.Blocks.GetHeightAsync());
            Assert.NotNull(await _bundle.Transactions.GetByHashAsync(tx4A.Hash));
            Assert.NotEmpty(await _bundle.Logs.GetLogsByBlockNumberAsync(4));

            var result = await new RewindCoordinator(_bundle).RewindToAsync(3, RewindPolicy.JournalOnly);
            Assert.Equal(RewindOutcome.JournalUsed, result.Outcome);
            Assert.Equal(3UL, _bundle.Metadata.GetLastBlock());

            var tx4B = MakeTx();
            await ApplyBlockAsync(4, Hash(0x4B), tx4B, withLog: false);

            Assert.Equal(4, (int)await _bundle.Blocks.GetHeightAsync());
            Assert.Null(await _bundle.Blocks.GetByNumberAsync(5));
            Assert.Null(await _bundle.Blocks.GetByNumberAsync(6));
            Assert.Empty(await _bundle.Transactions.GetByBlockNumberAsync(5));
            Assert.Empty(await _bundle.Transactions.GetByBlockNumberAsync(6));
            Assert.Empty(await _bundle.Receipts.GetByBlockNumberAsync(5));
            Assert.Empty(await _bundle.Receipts.GetByBlockNumberAsync(6));

            Assert.Null(await _bundle.Transactions.GetByHashAsync(tx4A.Hash));
            Assert.Null(await _bundle.Receipts.GetByTxHashAsync(tx4A.Hash));
            Assert.Null(await _bundle.Transactions.GetByHashAsync(tx5A.Hash));
            Assert.Null(await _bundle.Transactions.GetByHashAsync(tx6A.Hash));

            var block4Txs = await _bundle.Transactions.GetByBlockNumberAsync(4);
            Assert.Single(block4Txs);
            Assert.Equal(tx4B.Hash, block4Txs[0].Hash);

            Assert.Empty(await _bundle.Logs.GetLogsByBlockNumberAsync(4));
            Assert.Empty(await _bundle.Logs.GetLogsByBlockNumberAsync(5));
            Assert.Empty(await _bundle.Logs.GetLogsByBlockNumberAsync(6));
        }

        [Fact]
        public async Task Rewind_ThatIsNoOp_DoesNotTouchHistory()
        {
            var tx1 = MakeTx();
            await ApplyBlockAsync(1, Hash(1), tx1, withLog: true);

            var result = await new RewindCoordinator(_bundle).RewindToAsync(1, RewindPolicy.JournalOnly);
            Assert.Equal(RewindOutcome.NoOp, result.Outcome);

            Assert.Equal(1, (int)await _bundle.Blocks.GetHeightAsync());
            Assert.NotNull(await _bundle.Blocks.GetByNumberAsync(1));
            Assert.NotNull(await _bundle.Transactions.GetByHashAsync(tx1.Hash));
            Assert.NotEmpty(await _bundle.Logs.GetLogsByBlockNumberAsync(1));
        }

        [Fact]
        public async Task Reorg_ToShorterChain_ReclaimsTheOrphanedBlockAccessLists()
        {
            for (ulong n = 1; n <= 3; n++) await ApplyBlockAsync(n, Hash((byte)n), MakeTx(), withLog: false);
            await ApplyBlockAsync(4, Hash(0x4A), MakeTx(), withLog: false);
            await ApplyBlockAsync(5, Hash(0x5A), MakeTx(), withLog: false);
            await ApplyBlockAsync(6, Hash(0x6A), MakeTx(), withLog: false);
            Assert.Equal(BlockAccessListFor(5), await _bundle.BlockAccessLists.GetByBlockNumberAsync(5));
            Assert.Equal(BlockAccessListFor(6), await _bundle.BlockAccessLists.GetByBlockNumberAsync(6));

            var result = await new RewindCoordinator(_bundle).RewindToAsync(3, RewindPolicy.JournalOnly);

            Assert.Equal(RewindOutcome.JournalUsed, result.Outcome);
            Assert.Null(await _bundle.BlockAccessLists.GetByBlockNumberAsync(4));
            Assert.Null(await _bundle.BlockAccessLists.GetByBlockNumberAsync(5));
            Assert.Null(await _bundle.BlockAccessLists.GetByBlockNumberAsync(6));
            Assert.Equal(BlockAccessListFor(3), await _bundle.BlockAccessLists.GetByBlockNumberAsync(3));
            Assert.Equal(BlockAccessListFor(1), await _bundle.BlockAccessLists.GetByBlockNumberAsync(1));
        }

        private static byte[] BlockAccessListFor(ulong blockNumber) =>
            BlockAccessListRLPEncoder.Current.Encode(new List<AccountChanges>
            {
                new AccountChanges { Address = "0x" + blockNumber.ToString("x40") }
            });

        private ISignedTransaction MakeTx()
        {
            var tx = new Transaction1559(
                chainId: BigInteger.One,
                nonce: _nonce++,
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1_000_000_000),
                gasLimit: new BigInteger(21000),
                receiverAddress: "0x1111111111111111111111111111111111111111",
                amount: BigInteger.One,
                data: null,
                accessList: null);
            var sig = _key.SignAndCalculateYParityV(tx.RawHash);
            tx.SetSignature(new Signature { R = sig.R, S = sig.S, V = sig.V });
            return tx;
        }

        private async Task ApplyBlockAsync(ulong blockNumber, byte[] blockHash, ISignedTransaction tx, bool withLog)
        {
            _hist.SetCurrentBlockNumber((int)blockNumber);
            await _hist.SaveAccountAsync(ContractAddr, new Account { Balance = blockNumber, Nonce = 1 });
            await _hist.ClearCurrentBlockNumberAsync();

            var header = MakeHeader(blockNumber, blockHash);
            await _bundle.Blocks.SaveAsync(header, blockHash);

            await _bundle.Transactions.SaveAsync(tx, blockHash, txIndex: 0, blockNumber: blockNumber);
            var logs = withLog ? new List<Log> { new Log { Address = ContractAddr } } : new List<Log>();
            await _bundle.Receipts.SaveAsync(
                new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = logs },
                tx.Hash, blockHash, blockNumber, txIndex: 0, gasUsed: 21000, contractAddress: null, effectiveGasPrice: 0);
            if (withLog)
                await _bundle.Logs.SaveLogsAsync(logs, tx.Hash, blockHash, blockNumber, txIndex: 0);
            // BlockImporter retains the block's EIP-7928 access list here too (AMS-7928-06), so the orphaned
            // tail this drill creates carries one per block.
            await _bundle.BlockAccessLists.SaveAsync(blockHash, BlockAccessListFor(blockNumber));

            _bundle.Metadata.Commit(blockNumber, blockHash);
        }

        private static BlockHeader MakeHeader(ulong blockNumber, byte[] blockHash) => new BlockHeader
        {
            BlockNumber = blockNumber,
            ParentHash = blockNumber == 0 ? new byte[32] : Hash((byte)(blockNumber - 1)),
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            UnclesHash = new byte[32],
            ExtraData = Array.Empty<byte>(),
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
            Difficulty = 0,
            GasLimit = 0,
            GasUsed = 0,
            Timestamp = 0,
            MixHash = new byte[32],
            Nonce = new byte[8],
        };

        private static byte[] Hash(byte fill)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = fill;
            return h;
        }
    }
}
