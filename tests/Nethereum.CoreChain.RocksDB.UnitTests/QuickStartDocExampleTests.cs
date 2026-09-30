using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class QuickStartDocExampleTests : IDisposable
    {
        private readonly string _dataDir =
            Path.Combine(Path.GetTempPath(), $"quickstart_{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, recursive: true); } catch { }
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "rocksdb-quick-start",
            "Open a store bundle, persist a block and an account, and read both back", Order = 1)]
        public async Task Given_AFreshDataDirectory_When_ABlockAndAccountArePersisted_Then_BothReadBackEqual()
        {
            using var bundle = RocksDbChainStoreBundle.Open(_dataDir);

            var header = new BlockHeader
            {
                BlockNumber = 1,
                ParentHash = new byte[32],
                UnclesHash = new byte[32],
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                GasLimit = 30_000_000,
                GasUsed = 21_000,
                Timestamp = 1_700_000_000,
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8],
                Coinbase = AddressUtil.ZERO_ADDRESS
            };
            var blockHash = new byte[32];
            blockHash[0] = 0xAB;

            await bundle.Blocks.SaveAsync(header, blockHash);
            await bundle.State.SaveAccountAsync(
                AddressUtil.ZERO_ADDRESS, new Account { Balance = 100, Nonce = 1 });

            var byNumber = await bundle.Blocks.GetByNumberAsync(1);
            var byHash = await bundle.Blocks.GetByHashAsync(blockHash);
            var account = await bundle.State.GetAccountAsync(AddressUtil.ZERO_ADDRESS);

            Assert.Equal(1UL, (ulong)byNumber.BlockNumber);
            Assert.Equal(1UL, (ulong)byHash.BlockNumber);
            Assert.Equal(blockHash, await bundle.Blocks.GetHashByNumberAsync(1));
            Assert.Equal(100UL, (ulong)account.Balance);
            Assert.Equal(1UL, (ulong)account.Nonce);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "rocksdb-quick-start",
            "Reopening the same directory serves the rows written before the close", Order = 2)]
        public async Task Given_ABundleClosedAfterWriting_When_TheSameDirectoryIsReopened_Then_TheRowsAreStillServed()
        {
            var blockHash = new byte[32];
            blockHash[0] = 0xCD;

            using (var bundle = RocksDbChainStoreBundle.Open(_dataDir))
            {
                await bundle.Blocks.SaveAsync(new BlockHeader
                {
                    BlockNumber = 7,
                    ParentHash = new byte[32],
                    UnclesHash = new byte[32],
                    StateRoot = new byte[32],
                    TransactionsHash = new byte[32],
                    ReceiptHash = new byte[32],
                    LogsBloom = new byte[256],
                    GasLimit = 30_000_000,
                    GasUsed = 0,
                    Timestamp = 1_700_000_001,
                    ExtraData = Array.Empty<byte>(),
                    MixHash = new byte[32],
                    Nonce = new byte[8],
                    Coinbase = AddressUtil.ZERO_ADDRESS
                }, blockHash);
            }

            using var reopened = RocksDbChainStoreBundle.Open(_dataDir);

            var header = await reopened.Blocks.GetByHashAsync(blockHash);
            Assert.Equal(7UL, (ulong)header.BlockNumber);
            Assert.Equal(7UL, (ulong)await reopened.Blocks.GetHeightAsync());
        }
    }
}
