using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ABI.ABIInfoSqliteStorage;
using Nethereum.ABI.ABIRepository;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.ABI.UnitTests.ABIRepository
{
    public class SqliteABIInfoStorageTests
    {
        private const string Erc20TransferAbi =
            "[{\"constant\":false,\"inputs\":[{\"name\":\"to\",\"type\":\"address\"},{\"name\":\"value\",\"type\":\"uint256\"}],\"name\":\"transfer\",\"outputs\":[{\"name\":\"\",\"type\":\"bool\"}],\"type\":\"function\"}," +
            "{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"name\":\"from\",\"type\":\"address\"},{\"indexed\":true,\"name\":\"to\",\"type\":\"address\"},{\"indexed\":false,\"name\":\"value\",\"type\":\"uint256\"}],\"name\":\"Transfer\",\"type\":\"event\"}]";

        private const string ContractAddress = "0x6b175474e89094c44da98b954eedeac495271d0f";
        private const long ChainId = 1;
        private const string TransferFunctionSignature = "0xa9059cbb";
        private const string TransferEventSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-info-sqlite-storage", "Look up a stored contract ABI, function and event", Order = 1)]
        public async Task Given_AnAbiStoredForAContract_When_LookedUpByAddressAsync_Then_ItsFunctionAndEventAreFound()
        {
            using var storage = SqliteABIInfoStorage.CreateInMemory();
            storage.AddABIInfo(ABIInfo.FromABI(Erc20TransferAbi, ContractAddress, "Dai", null, ChainId));

            var abiInfo = await storage.GetABIInfoAsync(ChainId, ContractAddress);
            var function = await storage.FindFunctionABIAsync(ChainId, ContractAddress, TransferFunctionSignature);
            var functionFromInput = await storage.FindFunctionABIFromInputDataAsync(ChainId, ContractAddress,
                TransferFunctionSignature + new string('0', 128));
            var evt = await storage.FindEventABIAsync(ChainId, ContractAddress, TransferEventSignature);

            Assert.NotNull(abiInfo);
            Assert.Equal("transfer", function.Name);
            Assert.Equal("transfer", functionFromInput.Name);
            Assert.Equal("Transfer", evt.Name);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-info-sqlite-storage", "Batch lookup of function and event signatures", Order = 2)]
        public async Task Given_AnAbiStored_When_SignaturesAreLookedUpInABatch_Then_EachKnownSignatureMapsToItsAbi()
        {
            using var storage = SqliteABIInfoStorage.CreateInMemory();
            storage.AddABIInfo(ABIInfo.FromABI(Erc20TransferAbi, ContractAddress, "Dai", null, ChainId));

            var batch = await storage.FindABIsBatchAsync(
                new[] { TransferFunctionSignature, "0x12345678" },
                new[] { TransferEventSignature });

            Assert.Equal("transfer", batch.Functions[TransferFunctionSignature].Name);
            Assert.False(batch.Functions.ContainsKey("0x12345678"));
            Assert.Equal("Transfer", batch.Events[TransferEventSignature].Name);
            Assert.Equal("transfer", (await storage.FindFunctionABIsBatchAsync(new[] { TransferFunctionSignature }))[TransferFunctionSignature].Name);
            Assert.Equal("Transfer", (await storage.FindEventABIsBatchAsync(new[] { TransferEventSignature }))[TransferEventSignature].Name);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-info-sqlite-storage", "Persist ABIs in a SQLite database file", Order = 3)]
        public async Task Given_AnAbiWrittenToADatabaseFile_When_AnotherStorageOpensTheFile_Then_TheAbiIsReadBackFromTheDatabase()
        {
            var path = Path.Combine(Path.GetTempPath(), "nethereum-abi-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var writer = new SqliteABIInfoStorage($"Data Source={path}"))
                {
                    writer.AddABIInfo(ABIInfo.FromABI(Erc20TransferAbi, ContractAddress, "Dai", null, ChainId));
                }

                using var reader = new SqliteABIInfoStorage($"Data Source={path}");
                var function = await reader.FindFunctionABIAsync(ChainId, ContractAddress, TransferFunctionSignature);

                Assert.Equal("transfer", function.Name);
                Assert.Equal("Dai", (await reader.GetABIInfoAsync(ChainId, ContractAddress)).ContractName);
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
