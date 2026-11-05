using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    internal static class FreezerBlockFactory
    {
        internal const long ImmutabilityThreshold = 90_000;

        private static readonly EthECKey SenderKey = EthECKey.GenerateKey();
        private static readonly LegacyTransactionSigner TxSigner = new LegacyTransactionSigner();
        private static readonly object SignLock = new object();

        internal static readonly ITransactionVerificationAndRecovery Signer = new TransactionVerificationAndRecoveryImp();

        internal static RocksDbStorageOptions FreezerOptions(string root, bool backgroundIndexing, long? maxFileSizeBytes = 8)
        {
            var options = new RocksDbStorageOptions
            {
                UseFreezerHistory = true,
                FreezerHistoryDirectory = Path.Combine(root, "freezer"),
                PromotionEnabled = true,
                BackgroundFreezeIndexing = backgroundIndexing,
            };
            if (maxFileSizeBytes.HasValue) options.FreezerMaxFileSizeBytes = maxFileSizeBytes.Value;
            return options;
        }

        internal static string DataDir(string root) => Path.Combine(root, "data");

        internal static RocksDbChainStoreBundle OpenFreezerBundle(string root, RocksDbStorageOptions options)
            => RocksDbChainStoreBundle.Open(DataDir(root), null, false, options, Signer);

        internal static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string failure)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            Xunit.Assert.True(condition(), failure);
        }

        internal static void DeleteQuietly(string root)
        {
            if (Directory.Exists(root)) { try { Directory.Delete(root, true); } catch { } }
        }

        internal static void StampTipHeight(RocksDbManager rocks, long height)
            => rocks.Put(RocksDbManager.CF_METADATA, System.Text.Encoding.UTF8.GetBytes("height"),
                RocksDbSerializer.BigIntegerToBytes(height));

        internal static async Task WriteToHotAsync(RocksDbChainStoreBundle bundle, PersistableBlock block)
        {
            await bundle.Blocks.SaveAsync(block.Header, block.Hash).ConfigureAwait(false);
            await bundle.Transactions.SaveManyAsync(block.Hash, block.Header.BlockNumber.ToBigInteger(), block.Transactions).ConfigureAwait(false);
            await bundle.Uncles.SaveAsync(block.Hash, block.Uncles).ConfigureAwait(false);
            await bundle.Withdrawals.SaveAsync(block.Hash, block.Withdrawals).ConfigureAwait(false);
            await bundle.Receipts.SaveManyAsync(block.Hash, block.Header.BlockNumber.ToBigInteger(), block.Receipts).ConfigureAwait(false);
        }

        internal static List<PersistableBlock> MakeChainedBlocks(long start, int count)
        {
            var list = new List<PersistableBlock>(count);
            for (var i = 0; i < count; i++) list.Add(MakeBlock(start + i));
            return list;
        }

        internal static PersistableBlock MakeBareBlock(long number)
        {
            var parentHash = number > 0 ? Fill32(number - 1) : new byte[32];
            return new PersistableBlock(
                MakeHeader(number, parentHash), Fill32(number),
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: new List<ISignedTransaction>(),
                receipts: new List<ReceiptSaveItem>(),
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: new byte[256]);
        }

        internal static PersistableBlock MakeBlock(long number)
        {
            var hash = Fill32(number);
            var parentHash = number > 0 ? Fill32(number - 1) : new byte[32];
            var header = MakeHeader(number, parentHash);

            var tx = MakeTx(number);
            var receipt = new Receipt { PostStateOrStatus = new byte[] { 1 }, CumulativeGasUsed = 21000, Logs = new List<Log>() };

            return new PersistableBlock(
                header, hash,
                uncles: new List<BlockHeader>(),
                withdrawals: null,
                transactions: new List<ISignedTransaction> { tx },
                receipts: new List<ReceiptSaveItem> { new ReceiptSaveItem(receipt, tx.Hash, 0, 21000, null, 1_000_000_000) },
                logs: new List<(List<Log> Logs, byte[] TxHash, int TxIndex)>(),
                bloom: new byte[256]);
        }

        internal static BlockHeader MakeHeader(long number, byte[] parentHash) => new BlockHeader
        {
            BlockNumber = new EvmUInt256((ulong)number),
            ParentHash = parentHash,
            TransactionsHash = new byte[32],
            UnclesHash = new byte[32],
            ReceiptHash = new byte[32],
            StateRoot = new byte[32],
            Difficulty = new EvmUInt256(1UL),
            GasLimit = 1,
            Timestamp = 1000 + number,
            ExtraData = Array.Empty<byte>(),
            MixHash = new byte[32],
            Nonce = new byte[8],
            LogsBloom = new byte[256],
            Coinbase = "0x0000000000000000000000000000000000000000",
        };

        internal static ISignedTransaction MakeTx(long number)
        {
            var tx = new LegacyTransaction(
                nonce: CanonicalTestScalars.Scalar(number + 1),
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: new byte[20],
                value: Array.Empty<byte>(),
                data: Array.Empty<byte>());
            lock (SignLock) TxSigner.SignTransaction(SenderKey.GetPrivateKeyAsBytes(), tx);
            return tx;
        }

        internal static byte[] Fill32(long number)
        {
            var b = new byte[32];
            b[24] = 0x77;
            b[28] = (byte)(number >> 24);
            b[29] = (byte)(number >> 16);
            b[30] = (byte)(number >> 8);
            b[31] = (byte)number;
            return b;
        }
    }
}
