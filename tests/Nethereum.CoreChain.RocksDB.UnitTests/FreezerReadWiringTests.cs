using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FreezerReadWiringTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezerread_{Guid.NewGuid():N}");
        private readonly ITransactionVerificationAndRecovery _signer = new TransactionVerificationAndRecoveryImp();
        private readonly EthECKey _senderKey = EthECKey.GenerateKey();
        private readonly LegacyTransactionSigner _txSigner = new LegacyTransactionSigner();

        private const long TipHeight = 90_010;
        private const long FreezeBoundary = 10;
        private const int DrainCount = 16;
        private const int WithdrawalBlockNumber = 7;
        private const int UncleBlockNumber = 4;

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private string DataDir => Path.Combine(_root, "data");
        private string FreezerDir => Path.Combine(_root, "freezer");

        private RocksDbStorageOptions FreezerOptions() => new RocksDbStorageOptions
        {
            UseFreezerHistory = true,
            FreezerHistoryDirectory = FreezerDir,
        };

        private RocksDbChainStoreBundle OpenBundle()
            => RocksDbChainStoreBundle.Open(DataDir, null, false, FreezerOptions(), _signer);

        private async Task<List<PersistableBlock>> PersistDrainAsync()
        {
            var blocks = MakeBlocks(0, DrainCount);
            blocks[WithdrawalBlockNumber] = MakeBlock(WithdrawalBlockNumber, withWithdrawals: true);
            blocks[UncleBlockNumber] = MakeBlock(UncleBlockNumber, withUncle: true);

            using (var bundle = OpenBundle())
            {
                await SetTipHeightAsync(bundle, TipHeight);
                await bundle.PersistBlocksAsync(blocks);
            }
            return blocks;
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "freezer", "Frozen block reads back through the FreezerAware block store")]
        public async Task Given_FrozenBlock_When_GetByNumber_Then_ServedFromFreezer()
        {
            var blocks = await PersistDrainAsync();
            var frozen = blocks[3];
            var number = frozen.Header.BlockNumber.ToBigInteger();

            using var bundle = OpenBundle();

            var header = await bundle.Blocks.GetByNumberAsync(number);
            Assert.NotNull(header);
            Assert.Equal(number, header.BlockNumber.ToBigInteger());
            Assert.Equal(frozen.Header.ParentHash.ToHex(), header.ParentHash.ToHex());

            var hashByNumber = await bundle.Blocks.GetHashByNumberAsync(number);
            Assert.Equal(frozen.Hash.ToHex(), hashByNumber.ToHex());

            var txs = await bundle.Transactions.GetByBlockNumberAsync(number);
            Assert.Equal(frozen.Transactions.Count, txs.Count);
            Assert.Equal(frozen.Transactions[0].Hash.ToHex(), txs[0].Hash.ToHex());
            Assert.Equal(((LegacyTransaction)frozen.Transactions[0]).Nonce.ToHex(), ((LegacyTransaction)txs[0]).Nonce.ToHex());

            var receipts = await bundle.Receipts.GetByBlockNumberAsync(number);
            Assert.Equal(frozen.Receipts.Count, receipts.Count);
            Assert.Equal(frozen.Receipts[0].Receipt.PostStateOrStatus, receipts[0].PostStateOrStatus);
            Assert.Equal(frozen.Receipts[0].Receipt.Logs.Count, receipts[0].Logs.Count);
            Assert.Equal(frozen.Receipts[0].Receipt.Logs[0].Address, receipts[0].Logs[0].Address);
        }

        [Fact]
        public async Task Given_FrozenBlock_When_GetByHash_Then_ServedFromFreezer()
        {
            var blocks = await PersistDrainAsync();
            var frozen = blocks[3];

            using var bundle = OpenBundle();

            var header = await bundle.Blocks.GetByHashAsync(frozen.Hash);
            Assert.NotNull(header);
            Assert.Equal(frozen.Header.BlockNumber.ToBigInteger(), header.BlockNumber.ToBigInteger());

            Assert.True(await bundle.Blocks.ExistsAsync(frozen.Hash));

            var txs = await bundle.Transactions.GetByBlockHashAsync(frozen.Hash);
            Assert.Equal(frozen.Transactions.Count, txs.Count);
            Assert.Equal(frozen.Transactions[1].Hash.ToHex(), txs[1].Hash.ToHex());

            var receipts = await bundle.Receipts.GetByBlockHashAsync(frozen.Hash);
            Assert.Equal(frozen.Receipts.Count, receipts.Count);
        }

        [Fact]
        public async Task Given_RecentBlock_When_GetByNumber_Then_ServedFromRocksDb()
        {
            var blocks = await PersistDrainAsync();
            var recent = blocks[12];
            var number = recent.Header.BlockNumber.ToBigInteger();

            using var bundle = OpenBundle();

            var header = await bundle.Blocks.GetByNumberAsync(number);
            Assert.NotNull(header);
            Assert.Equal(number, header.BlockNumber.ToBigInteger());

            var byHash = await bundle.Blocks.GetByHashAsync(recent.Hash);
            Assert.NotNull(byHash);
            Assert.Equal(number, byHash.BlockNumber.ToBigInteger());

            var txs = await bundle.Transactions.GetByBlockNumberAsync(number);
            Assert.Equal(recent.Transactions.Count, txs.Count);
            Assert.Equal(recent.Transactions[0].Hash.ToHex(), txs[0].Hash.ToHex());

            var receipts = await bundle.Receipts.GetByBlockNumberAsync(number);
            Assert.Equal(recent.Receipts.Count, receipts.Count);
            Assert.Equal(recent.Receipts[0].Receipt.PostStateOrStatus, receipts[0].PostStateOrStatus);
        }

        [Fact]
        public async Task Given_FrozenBlockTx_When_GetTransactionByHash_Then_Resolves()
        {
            var blocks = await PersistDrainAsync();
            var frozen = blocks[3];
            var txHash = frozen.Transactions[1].Hash;

            using var bundle = OpenBundle();

            var tx = await bundle.Transactions.GetByHashAsync(txHash);
            Assert.NotNull(tx);
            Assert.Equal(txHash.ToHex(), tx.Hash.ToHex());
            Assert.Equal(((LegacyTransaction)frozen.Transactions[1]).Nonce.ToHex(), ((LegacyTransaction)tx).Nonce.ToHex());

            var location = await bundle.Transactions.GetLocationAsync(txHash);
            Assert.NotNull(location);
            Assert.Equal(frozen.Header.BlockNumber.ToBigInteger(), location.BlockNumber);
            Assert.Equal(1, location.TransactionIndex);
            Assert.Equal(frozen.Hash.ToHex(), location.BlockHash.ToHex());

            var receipt = await bundle.Receipts.GetByTxHashAsync(txHash);
            Assert.NotNull(receipt);
            Assert.Equal(frozen.Receipts[1].Receipt.PostStateOrStatus, receipt.PostStateOrStatus);

            var info = await bundle.Receipts.GetInfoByTxHashAsync(txHash);
            Assert.NotNull(info);
            Assert.Equal(frozen.Header.BlockNumber.ToBigInteger(), info.BlockNumber);
            Assert.Equal(1, info.TransactionIndex);
            Assert.Equal(txHash.ToHex(), info.TxHash.ToHex());
            Assert.Equal(frozen.Hash.ToHex(), info.BlockHash.ToHex());
        }

        [Fact]
        public async Task Given_FrozenPostShanghaiBlock_When_GetWithdrawals_Then_NotEmpty()
        {
            var blocks = await PersistDrainAsync();
            var withWithdrawals = blocks[WithdrawalBlockNumber];
            Assert.True(WithdrawalBlockNumber <= FreezeBoundary);

            using var bundle = OpenBundle();

            var byNumber = await bundle.Withdrawals.GetByBlockNumberAsync(withWithdrawals.Header.BlockNumber.ToBigInteger());
            Assert.NotNull(byNumber);
            Assert.NotEmpty(byNumber);
            Assert.Equal(withWithdrawals.Withdrawals.Count, byNumber.Count);
            Assert.Equal(withWithdrawals.Withdrawals[0].Index, byNumber[0].Index);
            Assert.Equal(withWithdrawals.Withdrawals[0].ValidatorIndex, byNumber[0].ValidatorIndex);
            Assert.Equal(withWithdrawals.Withdrawals[0].Address.ToHex(), byNumber[0].Address.ToHex());
            Assert.Equal(withWithdrawals.Withdrawals[0].AmountInGwei, byNumber[0].AmountInGwei);

            var byHash = await bundle.Withdrawals.GetByBlockHashAsync(withWithdrawals.Hash);
            Assert.NotNull(byHash);
            Assert.NotEmpty(byHash);
            Assert.Equal(withWithdrawals.Withdrawals.Count, byHash.Count);
        }

        [Fact]
        public async Task Given_UnknownHash_When_GetByHash_Then_Null()
        {
            await PersistDrainAsync();
            var unknownHash = Fill(0x99, 32);

            using var bundle = OpenBundle();

            Assert.Null(await bundle.Blocks.GetByHashAsync(unknownHash));
            Assert.False(await bundle.Blocks.ExistsAsync(unknownHash));

            Assert.Null(await bundle.Transactions.GetByHashAsync(unknownHash));
            Assert.Null(await bundle.Transactions.GetLocationAsync(unknownHash));
            Assert.Empty(await bundle.Transactions.GetByBlockHashAsync(unknownHash));
            Assert.Empty(await bundle.Transactions.GetHashesByBlockHashAsync(unknownHash));

            Assert.Null(await bundle.Receipts.GetByTxHashAsync(unknownHash));
            Assert.Null(await bundle.Receipts.GetInfoByTxHashAsync(unknownHash));
            Assert.Empty(await bundle.Receipts.GetByBlockHashAsync(unknownHash));

            Assert.Null(await bundle.Withdrawals.GetByBlockHashAsync(unknownHash));
        }

        [Fact]
        public async Task Given_FrozenBlockWithUncles_When_GetUnclesThroughBundle_Then_ReturnsUncles()
        {
            var blocks = await PersistDrainAsync();
            var frozen = blocks[UncleBlockNumber];
            Assert.True(UncleBlockNumber <= FreezeBoundary);
            var number = frozen.Header.BlockNumber.ToBigInteger();
            var expectedUncle = frozen.Uncles[0];

            using var bundle = OpenBundle();

            var byNumber = await bundle.Uncles.GetByBlockNumberAsync(number);
            Assert.NotNull(byNumber);
            Assert.Single(byNumber);
            Assert.Equal(expectedUncle.BlockNumber.ToBigInteger(), byNumber[0].BlockNumber.ToBigInteger());

            var byHash = await bundle.Uncles.GetByBlockHashAsync(frozen.Hash);
            Assert.NotNull(byHash);
            Assert.Single(byHash);
            Assert.Equal(expectedUncle.BlockNumber.ToBigInteger(), byHash[0].BlockNumber.ToBigInteger());
            Assert.Equal(expectedUncle.ParentHash.ToHex(), byHash[0].ParentHash.ToHex());
        }

        [Fact]
        public async Task Given_FrozenPreActivationBlock_When_GetBlockAccessListThroughBundle_Then_Null()
        {
            var blocks = await PersistDrainAsync();
            var frozen = blocks[3];
            var number = frozen.Header.BlockNumber.ToBigInteger();

            using var bundle = OpenBundle();

            Assert.Null(await bundle.BlockAccessLists.GetByBlockNumberAsync(number));
            Assert.Null(await bundle.BlockAccessLists.GetByBlockHashAsync(frozen.Hash));
        }

        private static async Task SetTipHeightAsync(RocksDbChainStoreBundle bundle, long height)
        {
            var hash = Fill(0xEE, 32);
            await bundle.Blocks.SaveAsync(MakeHeader(height, hash), hash);
        }

        private List<PersistableBlock> MakeBlocks(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlock(start + i));
            return list;
        }

        private PersistableBlock MakeBlock(long number, bool withWithdrawals = false, bool withUncle = false)
        {
            var hash = Fill((byte)(number & 0xFF), 32);
            var header = MakeHeader(number, hash);

            var txs = new List<ISignedTransaction> { MakeTx(number, 0), MakeTx(number, 1) };

            var receipts = new List<ReceiptSaveItem>();
            BigInteger cumulative = 0;
            for (var j = 0; j < txs.Count; j++)
            {
                cumulative += 21000;
                var rcptLogs = new List<Log>
                {
                    new Log { Address = "0x" + new string('a', 40), Topics = new List<byte[]> { Fill(0x11, 32) }, Data = new byte[] { (byte)j } }
                };
                var rcpt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = cumulative, Logs = rcptLogs };
                receipts.Add(new ReceiptSaveItem(rcpt, txs[j].Hash, j, 21000, null, 1_000_000_000));
            }

            List<Withdrawal> withdrawals = null;
            if (withWithdrawals)
            {
                withdrawals = new List<Withdrawal>
                {
                    new Withdrawal { Index = (ulong)number, ValidatorIndex = 7, Address = Fill(0x22, 20), AmountInGwei = 5_000_000 }
                };
            }

            var uncles = withUncle
                ? new List<BlockHeader> { MakeHeader(number > 0 ? number - 1 : 0, Fill((byte)((number + 0x40) & 0xFF), 32)) }
                : new List<BlockHeader>();

            return new PersistableBlock(
                header, hash,
                uncles: uncles,
                withdrawals: withdrawals,
                transactions: txs,
                receipts: receipts,
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: Fill(0x00, 256));
        }

        private static BlockHeader MakeHeader(long number, byte[] hash)
        {
            return new BlockHeader
            {
                BlockNumber = new EvmUInt256((ulong)number),
                ParentHash = new byte[32],
                TransactionsHash = new byte[32],
                UnclesHash = new byte[32],
                ReceiptHash = new byte[32],
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
        }

        private ISignedTransaction MakeTx(long number, int index)
        {
            var tx = new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar((number + 1) * 1000 + index),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: new byte[] { },
                data: Array.Empty<byte>());
            _txSigner.SignTransaction(_senderKey.GetPrivateKeyAsBytes(), tx);
            return tx;
        }

        private static byte[] Fill(byte v, int len)
        {
            var b = new byte[len];
            for (var i = 0; i < len; i++) b[i] = v;
            return b;
        }
    }
}
