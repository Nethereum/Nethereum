using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Util;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class CompositeStoreBodyPresenceTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"compositebody_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        [Fact]
        public async Task Given_TheHotWindowHoldsOnlyTheHeader_When_ABodyIsPersistedToHistoryOnly_Then_TransactionsReceiptsWithdrawalsAndUnclesComeFromHistory()
        {
            using var bundle = RocksDbChainStoreBundle.Open(
                Path.Combine(_root, "db"), storageOptions: new RocksDbStorageOptions { SplitHistoryStore = true });

            var signer = new LegacyTransactionSigner();
            var tx = TransactionFactory.CreateTransaction(signer.SignTransaction(
                "0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318".HexToByteArray(),
                1, "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC", 1, 0, 2_000_000_000, 21_000, ""));
            var header = new BlockHeader
            {
                BlockNumber = new EvmUInt256(5UL),
                ParentHash = new byte[32],
                UnclesHash = new byte[32],
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                MixHash = new byte[32],
                Nonce = new byte[8],
                Difficulty = new EvmUInt256(1UL),
                GasLimit = 1,
                Timestamp = 1,
                ExtraData = Array.Empty<byte>(),
                Coinbase = "0x0000000000000000000000000000000000000000",
            };
            var hash = new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header));
            await bundle.Blocks.SaveAsync(header, hash);

            var withdrawal = new Withdrawal { Index = 1, ValidatorIndex = 2, Address = new byte[20], AmountInGwei = 3 };
            var uncle = new BlockHeader { BlockNumber = new EvmUInt256(4UL), ParentHash = new byte[32], UnclesHash = new byte[32], StateRoot = new byte[32], TransactionsHash = new byte[32], ReceiptHash = new byte[32], LogsBloom = new byte[256], MixHash = new byte[32], Nonce = new byte[8], Difficulty = new EvmUInt256(1UL), GasLimit = 1, Timestamp = 1, ExtraData = Array.Empty<byte>(), Coinbase = "0x0000000000000000000000000000000000000000" };
            var receipts = new List<ReceiptSaveItem>
            {
                new ReceiptSaveItem(new Receipt { CumulativeGasUsed = new EvmUInt256(21000UL) }, tx.Hash, 0, 21000, null, 1),
            };
            var persisted = new PersistableBlock(
                header, hash, new List<BlockHeader> { uncle }, new List<Withdrawal> { withdrawal },
                new List<ISignedTransaction> { tx }, receipts, null, null);
            await ((IBatchedBlockPersister)bundle).PersistBlocksAsync(new[] { persisted });

            Assert.Single(await bundle.Transactions.GetByBlockHashAsync(hash));
            Assert.Single(await bundle.Transactions.GetByBlockNumberAsync(5));
            Assert.Single(await bundle.Receipts.GetByBlockHashAsync(hash));
            Assert.Single(await bundle.Receipts.GetByBlockNumberAsync(5));
            Assert.Single(await bundle.Withdrawals.GetByBlockHashAsync(hash));
            Assert.Single(await bundle.Withdrawals.GetByBlockNumberAsync(5));
            Assert.Single(await bundle.Uncles.GetByBlockHashAsync(hash));
            Assert.Single(await bundle.Uncles.GetByBlockNumberAsync(5));
        }
    }
}
