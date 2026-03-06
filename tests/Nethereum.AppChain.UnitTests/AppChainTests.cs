using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.AppChain.Genesis;
using Xunit;

namespace Nethereum.AppChain.UnitTests
{
    public class AppChainTests : IAsyncLifetime
    {
        private AppChainTestFixture _fixture = null!;

        public Task InitializeAsync()
        {
            _fixture = new AppChainTestFixture();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _fixture.Dispose();
            return Task.CompletedTask;
        }

        private AppChain CreateAppChain(AppChainConfig? config = null)
        {
            return new AppChain(
                config ?? AppChainConfig.Default,
                _fixture.BlockStore,
                _fixture.TransactionStore,
                _fixture.ReceiptStore,
                _fixture.LogStore,
                _fixture.StateStore);
        }

        [Fact]
        public async Task InitializeAsync_CreatesGenesisBlock()
        {
            var appChain = CreateAppChain();
            await appChain.InitializeAsync();

            var blockNumber = await appChain.GetBlockNumberAsync();
            Assert.Equal(0, blockNumber);

            var genesisBlock = await appChain.GetBlockByNumberAsync(0);
            Assert.NotNull(genesisBlock);
            Assert.Equal(0, genesisBlock.BlockNumber);
        }

        [Fact]
        public async Task InitializeAsync_WithPrefundedAccounts_SetsBalance()
        {
            var appChain = CreateAppChain();
            var addresses = new[] { "0x1234567890123456789012345678901234567890" };
            var options = new GenesisOptions
            {
                PrefundedAddresses = addresses,
            };

            await appChain.InitializeAsync(options);

            var balance = await appChain.GetBalanceAsync("0x1234567890123456789012345678901234567890");
            Assert.Equal(BigInteger.Parse("10000000000000000000000"), balance);
        }

        [Fact]
        public async Task InitializeAsync_WithCustomBalance_SetsCorrectBalance()
        {
            var appChain = CreateAppChain();
            var addresses = new[] { "0x1234567890123456789012345678901234567890" };
            var customBalance = BigInteger.Parse("1000000000000000000");
            var options = new GenesisOptions
            {
                PrefundedAddresses = addresses,
                PrefundBalance = customBalance,
            };

            await appChain.InitializeAsync(options);

            var balance = await appChain.GetBalanceAsync("0x1234567890123456789012345678901234567890");
            Assert.Equal(customBalance, balance);
        }

        [Fact]
        public async Task InitializeAsync_WithoutMudWorld_NoWorldCodeAtGenesis()
        {
            var config = AppChainConfig.Default;
            var appChain = CreateAppChain(config);
            var options = new GenesisOptions
            {
            };

            await appChain.InitializeAsync(options);

            var code = await appChain.GetCodeAsync(config.WorldAddress);
            Assert.Null(code);
        }

        [Fact]
        public async Task GetLatestBlockAsync_ReturnsGenesisBlock()
        {
            var appChain = CreateAppChain();
            await appChain.InitializeAsync();

            var latestBlock = await appChain.GetLatestBlockAsync();

            Assert.NotNull(latestBlock);
            Assert.Equal(0, latestBlock.BlockNumber);
        }

        [Fact]
        public async Task GetNonce_ReturnsZeroForNonExistentAccount()
        {
            var appChain = CreateAppChain();
            await appChain.InitializeAsync();

            var nonce = await appChain.GetNonceAsync("0x0000000000000000000000000000000000000001");

            Assert.Equal(0, nonce);
        }

        [Fact]
        public async Task GetBalance_ReturnsZeroForNonExistentAccount()
        {
            var appChain = CreateAppChain();
            await appChain.InitializeAsync();

            var balance = await appChain.GetBalanceAsync("0x0000000000000000000000000000000000000001");

            Assert.Equal(0, balance);
        }

        [Fact]
        public async Task Config_ExposesConfiguration()
        {
            var config = AppChainConfig.CreateWithName("TestChain", 999999);
            var appChain = CreateAppChain(config);
            await appChain.InitializeAsync();

            Assert.Equal("TestChain", appChain.Config.AppChainName);
            Assert.Equal(999999, appChain.Config.ChainId);
        }

        [Fact]
        public async Task WorldAddress_ExposesConfiguredAddress()
        {
            var config = AppChainConfig.Default;
            var appChain = CreateAppChain(config);
            await appChain.InitializeAsync();

            Assert.Equal(config.WorldAddress, appChain.WorldAddress);
        }

        [Fact]
        public async Task GetBlockByHash_ReturnsCorrectBlock()
        {
            var appChain = CreateAppChain();
            await appChain.InitializeAsync();

            var genesisBlock = await appChain.GetBlockByNumberAsync(0);
            Assert.NotNull(genesisBlock);

            var hashBytes = await _fixture.BlockStore.GetHashByNumberAsync(0);
            var blockByHash = await appChain.GetBlockByHashAsync(hashBytes);

            Assert.NotNull(blockByHash);
            Assert.Equal(0, blockByHash.BlockNumber);
        }

        [Fact]
        public async Task InitializeAsync_IsIdempotent()
        {
            var appChain = CreateAppChain();
            await appChain.InitializeAsync();
            await appChain.InitializeAsync();

            var blockNumber = await appChain.GetBlockNumberAsync();
            Assert.Equal(0, blockNumber);
        }

        [Fact]
        public async Task GenesisBlock_HasCorrectExtraData()
        {
            var config = AppChainConfig.CreateWithName("MyAppChain", 123);
            var appChain = CreateAppChain(config);
            await appChain.InitializeAsync();

            var genesisBlock = await appChain.GetBlockByNumberAsync(0);
            Assert.NotNull(genesisBlock);
            Assert.NotNull(genesisBlock.ExtraData);

            var extraDataString = System.Text.Encoding.UTF8.GetString(genesisBlock.ExtraData);
            Assert.Contains("AppChain:MyAppChain", extraDataString);
        }

        [Fact]
        public async Task DataPersistence_SurvivesRestart()
        {
            var address = "0x1234567890123456789012345678901234567890";
            var balance = BigInteger.Parse("5000000000000000000");
            var dbPath = _fixture.DatabasePath;

            var config = AppChainConfig.Default;
            var appChain = CreateAppChain(config);
            var options = new GenesisOptions
            {
                PrefundedAddresses = new[] { address },
                PrefundBalance = balance,
                DeployCreate2Factory = true,
            };
            await appChain.InitializeAsync(options);

            _fixture.Manager.Dispose();

            var rocksOptions = new RocksDbStorageOptions { DatabasePath = dbPath };
            using var newManager = new RocksDbManager(rocksOptions);
            var newBlockStore = new RocksDbBlockStore(newManager);
            var newStateStore = new RocksDbStateStore(newManager);

            var retrievedBlock = await newBlockStore.GetByNumberAsync(0);
            Assert.NotNull(retrievedBlock);
            Assert.Equal(0, retrievedBlock.BlockNumber);

            var retrievedAccount = await newStateStore.GetAccountAsync(address);
            Assert.NotNull(retrievedAccount);
            Assert.Equal(balance, retrievedAccount.Balance);

            var create2Account = await newStateStore.GetAccountAsync(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);
            Assert.NotNull(create2Account);
            var create2Code = await newStateStore.GetCodeAsync(create2Account.CodeHash!);
            Assert.NotNull(create2Code);
            Assert.True(create2Code.Length > 0);
        }

        [Fact]
        public async Task InitializeAsync_WithCreate2Factory_DeploysFactory()
        {
            var appChain = CreateAppChain();
            var options = new GenesisOptions
            {
                DeployCreate2Factory = true
            };

            await appChain.InitializeAsync(options);

            var code = await appChain.GetCodeAsync(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);
            Assert.NotNull(code);
            Assert.True(code.Length > 0);

            var account = await appChain.GetAccountAsync(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);
            Assert.NotNull(account);
            Assert.Equal(1, account.Nonce);
        }

        [Fact]
        public async Task InitializeAsync_WithoutCreate2Factory_NoFactoryCode()
        {
            var appChain = CreateAppChain();
            var options = new GenesisOptions
            {
                DeployCreate2Factory = false
            };

            await appChain.InitializeAsync(options);

            var code = await appChain.GetCodeAsync(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);
            Assert.Null(code);
        }

        [Fact]
        public async Task InitializeAsync_DefaultOptions_DeploysCreate2FactoryOnly()
        {
            var config = AppChainConfig.Default;
            var appChain = CreateAppChain(config);

            await appChain.InitializeAsync();

            var create2Code = await appChain.GetCodeAsync(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);
            Assert.NotNull(create2Code);
            Assert.True(create2Code.Length > 0);

            var worldCode = await appChain.GetCodeAsync(config.WorldAddress);
            Assert.Null(worldCode);
        }

        [Fact]
        public void Create2FactoryAddress_ExposesCorrectAddress()
        {
            var appChain = CreateAppChain();
            Assert.Equal(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS, appChain.Create2FactoryAddress);
        }

        [Fact]
        public void CalculateCreate2Address_ComputesCorrectAddress()
        {
            var deployer = "0x4e59b44847b379578588920cA78FbF26c0B4956C";
            var salt = new byte[32];
            var initCode = new byte[] { 0x60, 0x80 };

            var address = Create2FactoryGenesisBuilder.CalculateCreate2Address(deployer, salt, initCode);

            Assert.NotNull(address);
            Assert.StartsWith("0x", address);
            Assert.Equal(42, address.Length);
        }

        [Fact]
        public void CalculateCreate2Address_IsDeterministic()
        {
            var deployer = "0x4e59b44847b379578588920cA78FbF26c0B4956C";
            var salt = new byte[32];
            salt[31] = 0x01;
            var initCode = new byte[] { 0x60, 0x80, 0x60, 0x40 };

            var address1 = Create2FactoryGenesisBuilder.CalculateCreate2Address(deployer, salt, initCode);
            var address2 = Create2FactoryGenesisBuilder.CalculateCreate2Address(deployer, salt, initCode);

            Assert.Equal(address1, address2);
        }

        [Fact]
        public void CalculateCreate2Address_DifferentSaltsDifferentAddresses()
        {
            var deployer = "0x4e59b44847b379578588920cA78FbF26c0B4956C";
            var salt1 = new byte[32];
            var salt2 = new byte[32];
            salt2[31] = 0x01;
            var initCode = new byte[] { 0x60, 0x80 };

            var address1 = Create2FactoryGenesisBuilder.CalculateCreate2Address(deployer, salt1, initCode);
            var address2 = Create2FactoryGenesisBuilder.CalculateCreate2Address(deployer, salt2, initCode);

            Assert.NotEqual(address1, address2);
        }
    }
}
