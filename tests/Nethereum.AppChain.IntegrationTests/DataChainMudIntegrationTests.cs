using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Sequencer;
using Nethereum.Mud;
using Nethereum.Mud.Contracts;
using Nethereum.Mud.Contracts.World;
using Nethereum.Mud.Contracts.World.Systems.AccessManagementSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Mud.TableRepository;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Xunit;
using Xunit.Abstractions;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class AppChainMudIntegrationTests : IAsyncLifetime, IDisposable
    {
        private string _databasePath = "";
        private RocksDbManager? _dbManager;
        private AppChainCore? _appChain;
        private Sequencer.Sequencer? _sequencer;
        private Web3.Web3? _web3;
        private AppChainNode? _appChainNode;

        private readonly ITestOutputHelper _output;

        private const string DeployerPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private readonly string _deployerAddress;
        private static readonly BigInteger AppChainId = new BigInteger(420420);

        public AppChainMudIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            var deployerKey = new EthECKey(DeployerPrivateKey);
            _deployerAddress = deployerKey.GetPublicAddress();
        }

        public async Task InitializeAsync()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"appchain_mud_{Guid.NewGuid():N}");
            var options = new RocksDbStorageOptions { DatabasePath = _databasePath };
            _dbManager = new RocksDbManager(options);

            var blockStore = new RocksDbBlockStore(_dbManager);
            var transactionStore = new RocksDbTransactionStore(_dbManager, blockStore);
            var receiptStore = new RocksDbReceiptStore(_dbManager, blockStore);
            var logStore = new RocksDbLogStore(_dbManager);
            var stateStore = new RocksDbStateStore(_dbManager);

            var appChainConfig = AppChainConfig.CreateWithName("MudTestAppChain", (int)AppChainId);
            appChainConfig.SequencerAddress = _deployerAddress;

            _appChain = new AppChainCore(appChainConfig, blockStore, transactionStore, receiptStore, logStore, stateStore);

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = new[] { _deployerAddress },
                PrefundBalance = BigInteger.Parse("10000000000000000000000"),
                DeployCreate2Factory = true
            };
            await _appChain.InitializeAsync(genesisOptions);

            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _deployerAddress,
                SequencerPrivateKey = DeployerPrivateKey,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };
            _sequencer = new Sequencer.Sequencer(_appChain, sequencerConfig);
            await _sequencer.StartAsync();

            _appChainNode = new AppChainNode(_appChain, _sequencer);

            var account = new Account(DeployerPrivateKey, (int)AppChainId);
            var rpcClient = new AppChainRpcClient(_appChainNode, (long)AppChainId);
            _web3 = new Web3.Web3(account, rpcClient);
            _web3.TransactionManager.UseLegacyAsDefault = true;
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _sequencer = null;
            _appChain = null;
            _dbManager?.Dispose();

            if (!string.IsNullOrEmpty(_databasePath) && Directory.Exists(_databasePath))
            {
                try { Directory.Delete(_databasePath, true); } catch { }
            }
        }

        [Fact]
        public async Task AppChain_HasCreate2FactoryDeployed()
        {
            var create2Address = Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;
            var code = await _appChain!.GetCodeAsync(create2Address);

            Assert.NotNull(code);
            Assert.True(code.Length > 0);
            _output.WriteLine($"Create2Factory deployed at: {create2Address}");
            _output.WriteLine($"Create2Factory code size: {code.Length} bytes");
        }

        [Fact]
        public async Task AppChain_CanDeployFullMudStack()
        {
            var create2Address = Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;
            var salt = "0x0000000000000000000000000000000000000000000000000000000000000001";

            var deployService = new WorldFactoryDeployService();
            var addresses = await deployService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                _web3!, create2Address, salt);

            _output.WriteLine($"AccessManagementSystem: {addresses.AccessManagementSystemAddress}");
            _output.WriteLine($"BalanceTransferSystem: {addresses.BalanceTransferSystemAddress}");
            _output.WriteLine($"BatchCallSystem: {addresses.BatchCallSystemAddress}");
            _output.WriteLine($"RegistrationSystem: {addresses.RegistrationSystemAddress}");
            _output.WriteLine($"InitModule: {addresses.InitModuleAddress}");
            _output.WriteLine($"WorldFactory: {addresses.WorldFactoryAddress}");

            var factoryCode = await _appChain!.GetCodeAsync(addresses.WorldFactoryAddress);
            Assert.NotNull(factoryCode);
            Assert.True(factoryCode.Length > 0);
        }

        [Fact]
        public async Task AppChain_CanDeployWorldFromFactory()
        {
            var create2Address = Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;
            var salt = "0x0000000000000000000000000000000000000000000000000000000000000002";

            var deployService = new WorldFactoryDeployService();
            var addresses = await deployService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                _web3!, create2Address, salt);

            var worldSalt = "0x0000000000000000000000000000000000000000000000000000000000000042";
            var worldDeployedEvent = await deployService.DeployWorldAsync(_web3!, worldSalt, addresses);

            Assert.NotNull(worldDeployedEvent);
            var worldAddress = worldDeployedEvent.NewContract;
            Assert.NotNull(worldAddress);
            Assert.NotEqual("0x0000000000000000000000000000000000000000", worldAddress);

            var worldCode = await _appChain!.GetCodeAsync(worldAddress);
            Assert.NotNull(worldCode);
            Assert.True(worldCode.Length > 0);

            _output.WriteLine($"World deployed at: {worldAddress}");
            _output.WriteLine($"World code size: {worldCode.Length} bytes");
        }

        private string? _worldAddress;
        private WorldFactoryContractAddresses? _worldFactoryAddresses;

        private async Task<string> DeployWorldAsync()
        {
            if (_worldAddress != null) return _worldAddress;

            var create2Address = Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;
            var salt = "0x0000000000000000000000000000000000000000000000000000000000000099";

            var deployService = new WorldFactoryDeployService();
            _worldFactoryAddresses = await deployService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                _web3!, create2Address, salt);

            var worldSalt = "0x00000000000000000000000000000000000000000000000000000000000000AA";
            var worldEvent = await deployService.DeployWorldAsync(_web3!, worldSalt, _worldFactoryAddresses);

            _worldAddress = worldEvent.NewContract;
            _output.WriteLine($"Test World deployed at: {_worldAddress}");
            return _worldAddress;
        }

        private async Task<TransactionReceipt> RegisterItemTableAsync(WorldService worldService, ItemTableRecord tableRecord)
        {
            var schemaEncoded = tableRecord.GetSchemaEncoded();
            var registerFunction = schemaEncoded.ToRegisterTableFunction();
            return await worldService.ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction);
        }

        private async Task<TransactionReceipt> RegisterCounterTableAsync(WorldService worldService, CounterTableRecord tableRecord)
        {
            var schemaEncoded = tableRecord.GetSchemaEncoded();
            var registerFunction = schemaEncoded.ToRegisterTableFunction();
            return await worldService.ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction);
        }

        [Fact]
        public async Task AppChain_CanWriteAndReadItemTableRecord()
        {
            var worldAddress = await DeployWorldAsync();
            var worldService = new WorldService(_web3!, worldAddress);

            var item = new ItemTableRecord();
            var registerReceipt = await RegisterItemTableAsync(worldService, item);
            Assert.True(registerReceipt.Succeeded());
            _output.WriteLine($"Item table registered, gas used: {registerReceipt.GasUsed}");

            item.Keys.Id = 1;
            item.Values.Name = "TestItem";
            item.Values.Price = 100;
            item.Values.Description = "A test item";
            item.Values.Owner = _deployerAddress;

            var writeReceipt = await worldService.SetRecordRequestAndWaitForReceiptAsync(item);
            Assert.NotNull(writeReceipt);
            Assert.True(writeReceipt.Succeeded());
            _output.WriteLine($"Item written, gas used: {writeReceipt.GasUsed}");

            var readItem = new ItemTableRecord { Keys = { Id = 1 } };
            var result = await worldService.GetRecordTableQueryAsync<
                ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>(readItem);

            Assert.Equal("TestItem", result.Values.Name);
            Assert.Equal(100, result.Values.Price);
            Assert.Equal("A test item", result.Values.Description);
            Assert.Equal(_deployerAddress, result.Values.Owner);

            _output.WriteLine($"Read back: Name={result.Values.Name}, Price={result.Values.Price}");
        }

        [Fact]
        public async Task AppChain_CanDeleteItemTableRecord()
        {
            var worldAddress = await DeployWorldAsync();
            var worldService = new WorldService(_web3!, worldAddress);

            var item = new ItemTableRecord();
            await RegisterItemTableAsync(worldService, item);

            item.Keys.Id = 2;
            item.Values.Name = "ToDelete";
            item.Values.Price = 50;
            item.Values.Description = "Will be deleted";
            item.Values.Owner = _deployerAddress;

            await worldService.SetRecordRequestAndWaitForReceiptAsync(item);

            var readItem = new ItemTableRecord { Keys = { Id = 2 } };
            var beforeDelete = await worldService.GetRecordTableQueryAsync<
                ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>(readItem);
            Assert.Equal("ToDelete", beforeDelete.Values.Name);

            var deleteReceipt = await worldService.DeleteRecordRequestAndWaitForReceiptAsync(item);
            Assert.True(deleteReceipt.Succeeded());
            _output.WriteLine($"Item deleted, gas used: {deleteReceipt.GasUsed}");

            var afterDelete = await worldService.GetRecordTableQueryAsync<
                ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>(new ItemTableRecord { Keys = { Id = 2 } });
            Assert.True(string.IsNullOrEmpty(afterDelete.Values.Name));
            Assert.Equal(0, afterDelete.Values.Price);
        }

        [Fact]
        public async Task AppChain_CanReadMultipleItemRecords()
        {
            var worldAddress = await DeployWorldAsync();
            var worldService = new WorldService(_web3!, worldAddress);

            var templateItem = new ItemTableRecord();
            await RegisterItemTableAsync(worldService, templateItem);

            for (int i = 1; i <= 3; i++)
            {
                var item = new ItemTableRecord
                {
                    Keys = { Id = i },
                    Values = { Name = $"Item{i}", Price = i * 10, Description = $"Description {i}", Owner = _deployerAddress }
                };
                await worldService.SetRecordRequestAndWaitForReceiptAsync(item);
                _output.WriteLine($"Created Item{i}");
            }

            var items = new List<ItemTableRecord>
            {
                new ItemTableRecord { Keys = { Id = 1 } },
                new ItemTableRecord { Keys = { Id = 2 } },
                new ItemTableRecord { Keys = { Id = 3 } }
            };

            var results = await worldService.GetRecordTableMultiQueryRpcAsync<
                ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>(items);

            Assert.Equal(3, results.Count);
            Assert.Equal("Item1", results[0].Values.Name);
            Assert.Equal(10, results[0].Values.Price);
            Assert.Equal("Item2", results[1].Values.Name);
            Assert.Equal(20, results[1].Values.Price);
            Assert.Equal("Item3", results[2].Values.Name);
            Assert.Equal(30, results[2].Values.Price);

            _output.WriteLine("Successfully read all 3 items via multi-query");
        }

        [Fact]
        public async Task AppChain_CanWriteAndReadCounterSingleton()
        {
            var worldAddress = await DeployWorldAsync();
            var worldService = new WorldService(_web3!, worldAddress);

            var counter = new CounterTableRecord();
            await RegisterCounterTableAsync(worldService, counter);

            counter.Values.Value = 42;
            var writeReceipt = await worldService.SetRecordRequestAndWaitForReceiptAsync(counter);
            Assert.True(writeReceipt.Succeeded());
            _output.WriteLine($"Counter set to 42, gas used: {writeReceipt.GasUsed}");

            var readCounter = await worldService.GetRecordTableQueryAsync<
                CounterTableRecord, CounterTableRecord.CounterValue>(new CounterTableRecord());
            Assert.Equal(42, readCounter.Values.Value);
            _output.WriteLine($"Read counter value: {readCounter.Values.Value}");
        }
    }

    public class CounterTableRecord : TableRecordSingleton<CounterTableRecord.CounterValue>
    {
        public CounterTableRecord() : base("Counter")
        {
        }

        public class CounterValue
        {
            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint32", "value", 1)]
            public int Value { get; set; }
        }
    }

    public class ItemTableRecord : TableRecord<ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>
    {
        public ItemTableRecord() : base("Item")
        {
        }

        public class ItemKey
        {
            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint32", "id", 1)]
            public int Id { get; set; }
        }

        public class ItemValue
        {
            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint32", "price", 1)]
            public int Price { get; set; }

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("string", "name", 2)]
            public string Name { get; set; } = "";

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("string", "description", 3)]
            public string Description { get; set; } = "";

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("string", "owner", 4)]
            public string Owner { get; set; } = "";
        }
    }
}
