using System.Numerics;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class StateSnapshotTests
    {
        private readonly BigInteger _chainId = 420420;

        [Fact]
        public async Task WriteAndReadSnapshot_RoundTrip_PreservesAccounts()
        {
            // Arrange
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            await SetupTestState(stateStore, blockStore);

            var writer = new StateSnapshotWriter(stateStore, blockStore, _chainId);
            var reader = new StateSnapshotReader();

            using var stream = new MemoryStream();

            // Act - Write
            var snapshotInfo = await writer.WriteSnapshotAsync(0, stream);

            // Assert write
            Assert.NotNull(snapshotInfo);
            Assert.Equal(_chainId, snapshotInfo.ChainId);
            Assert.Equal(0, snapshotInfo.BlockNumber);
            Assert.Equal(3, snapshotInfo.AccountCount);
            Assert.True(snapshotInfo.SnapshotHash.Length == 32);

            // Act - Read header
            stream.Position = 0;
            var header = await reader.ReadHeaderAsync(stream);

            // Assert header
            Assert.Equal((ulong)_chainId, header.ChainId);
            Assert.Equal(0UL, header.BlockNumber);
            Assert.Equal(3UL, header.AccountCount);

            // Act - Read accounts
            stream.Position = 0;
            var accounts = new List<StateAccount>();
            await foreach (var account in reader.ReadAccountsAsync(stream))
            {
                accounts.Add(account);
            }

            // Assert accounts
            Assert.Equal(3, accounts.Count);
            var account1 = accounts.FirstOrDefault(a => NormalizeAddress(a.Address) == NormalizeAddress("0x0000000000000000000000000000000000000001"));
            Assert.NotNull(account1);
            Assert.Equal(BigInteger.Parse("1000000000000000000"), account1.Balance);
            Assert.Equal(1, account1.Nonce);
        }

        [Fact]
        public async Task WriteAndReadSnapshot_PreservesStorageSlots()
        {
            // Arrange
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            await SetupTestState(stateStore, blockStore);

            // Add storage slots (address will be normalized when saved)
            var address = "0x0000000000000000000000000000000000000001";
            await stateStore.SaveStorageAsync(address, 0, PadTo32Bytes(new byte[] { 0x42 }));
            await stateStore.SaveStorageAsync(address, 1, PadTo32Bytes(new byte[] { 0x43 }));

            var writer = new StateSnapshotWriter(stateStore, blockStore, _chainId);
            var reader = new StateSnapshotReader();

            using var stream = new MemoryStream();
            await writer.WriteSnapshotAsync(0, stream);

            // Act - Read storage (use normalized address since that's how it's stored in snapshot)
            stream.Position = 0;
            var slots = new List<StateStorageSlot>();
            var normalizedAddress = NormalizeAddress(address);
            await foreach (var slot in reader.ReadStorageSlotsAsync(stream, normalizedAddress))
            {
                slots.Add(slot);
            }

            // Assert
            Assert.Equal(2, slots.Count);
            Assert.Contains(slots, s => s.Slot == 0);
            Assert.Contains(slots, s => s.Slot == 1);
        }

        [Fact]
        public async Task WriteAndReadSnapshot_PreservesCode()
        {
            // Arrange
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            await SetupTestState(stateStore, blockStore);

            // Add contract code
            var code = new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 };
            var codeHash = new Nethereum.Util.Sha3Keccack().CalculateHash(code);
            await stateStore.SaveCodeAsync(codeHash, code);

            var contractAddress = "0x0000000000000000000000000000000000000003";
            var contractAccount = await stateStore.GetAccountAsync(contractAddress);
            contractAccount.CodeHash = codeHash;
            await stateStore.SaveAccountAsync(contractAddress, contractAccount);

            var writer = new StateSnapshotWriter(stateStore, blockStore, _chainId);
            var reader = new StateSnapshotReader();

            using var stream = new MemoryStream();
            var snapshotInfo = await writer.WriteSnapshotAsync(0, stream);

            // Act - Read codes
            stream.Position = 0;
            var codes = new List<StateCode>();
            await foreach (var c in reader.ReadCodesAsync(stream))
            {
                codes.Add(c);
            }

            // Assert
            Assert.Single(codes);
            Assert.Equal(codeHash, codes[0].CodeHash);
            Assert.Equal(code, codes[0].Code);
        }

        [Fact]
        public async Task ReadAndVerify_WithExpectedStateRoot_Succeeds()
        {
            // Arrange
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            await SetupTestState(stateStore, blockStore);

            var writer = new StateSnapshotWriter(stateStore, blockStore, _chainId);
            var reader = new StateSnapshotReader();

            using var stream = new MemoryStream();
            var snapshotInfo = await writer.WriteSnapshotAsync(0, stream);

            // Get the block's state root
            var block = await blockStore.GetByNumberAsync(0);

            // Act
            stream.Position = 0;
            var verifiedInfo = await reader.ReadAndVerifyAsync(stream, block.StateRoot);

            // Assert
            Assert.NotNull(verifiedInfo);
            Assert.Equal(snapshotInfo.AccountCount, verifiedInfo.AccountCount);
        }

        [Fact]
        public async Task WriteToFile_CreatesCompressedSnapshot()
        {
            // Arrange
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            await SetupTestState(stateStore, blockStore);

            var writer = new StateSnapshotWriter(stateStore, blockStore, _chainId);
            var tempPath = Path.GetTempFileName() + ".state.gz";

            try
            {
                // Act
                var snapshotInfo = await writer.WriteSnapshotToFileAsync(0, tempPath, compress: true);

                // Assert
                Assert.True(File.Exists(tempPath));
                Assert.NotNull(snapshotInfo);
                Assert.Equal(3, snapshotInfo.AccountCount);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void StateSnapshotFileName_FormatsCorrectly()
        {
            // Act
            var compressedName = BatchFileFormat.GetStateSnapshotFileName(420420, 10000, compressed: true);
            var uncompressedName = BatchFileFormat.GetStateSnapshotFileName(420420, 10000, compressed: false);

            // Assert
            Assert.Equal("state_420420_10000.state.zst", compressedName);
            Assert.Equal("state_420420_10000.state", uncompressedName);
        }

        private async Task SetupTestState(InMemoryStateStore stateStore, InMemoryBlockStore blockStore)
        {
            // Create accounts
            await stateStore.SaveAccountAsync("0x0000000000000000000000000000000000000001", new Account
            {
                Balance = BigInteger.Parse("1000000000000000000"),
                Nonce = 1,
                CodeHash = new byte[32],
                StateRoot = new byte[32]
            });

            await stateStore.SaveAccountAsync("0x0000000000000000000000000000000000000002", new Account
            {
                Balance = BigInteger.Parse("2000000000000000000"),
                Nonce = 5,
                CodeHash = new byte[32],
                StateRoot = new byte[32]
            });

            await stateStore.SaveAccountAsync("0x0000000000000000000000000000000000000003", new Account
            {
                Balance = 0,
                Nonce = 0,
                CodeHash = new byte[32],
                StateRoot = new byte[32]
            });

            // Create genesis block
            var header = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000000",
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                Difficulty = 1,
                BlockNumber = 0,
                GasLimit = 30000000,
                GasUsed = 0,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                BaseFee = 1000000000
            };

            var blockHash = new Nethereum.Util.Sha3Keccack().CalculateHash(
                BlockHeaderEncoder.Current.Encode(header));
            await blockStore.SaveAsync(header, blockHash);
        }

        private byte[] PadTo32Bytes(byte[] data)
        {
            if (data.Length >= 32) return data;
            var padded = new byte[32];
            Array.Copy(data, 0, padded, 32 - data.Length, data.Length);
            return padded;
        }

        private static string NormalizeAddress(string address)
        {
            return AddressUtil.Current.ConvertToValid20ByteAddress(address);
        }
    }
}
