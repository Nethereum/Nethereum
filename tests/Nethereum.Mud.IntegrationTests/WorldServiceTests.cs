using Nethereum.Contracts;
using Nethereum.Mud.Contracts.World;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Mud.TableRepository;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Diagnostics;
using Nethereum.Mud.IntegrationTests.MudTest.Tables;
using Nethereum.Mud.IntegrationTests.MudTest.Systems;
using Nethereum.Mud.IntegrationTests.MudTest.Systems.IncrementSystem;
using Nethereum.Mud.IntegrationTests.MudTest.Systems.IncrementSystem.ContractDefinition;
using Nethereum.Mud.Contracts;
using Nethereum.Mud.Contracts.Core.StoreEvents;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using Nethereum.Mud.Contracts.World.Tables;
using Nethereum.Mud.Contracts.Store.Tables;
using Nethereum.Mud.Contracts.Core.Systems;
using Nethereum.Mud.Contracts.Core.Namespaces;
using Nethereum.Web3;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Util;
using Nethereum.Mud.Contracts.World.Systems.AccessManagementSystem;
using Nethereum.ABI;
using Nethereum.RLP;
using MyProject.Contracts.MyWorld.Tables;


namespace Nethereum.Mud.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class WorldServiceTests : IAsyncLifetime
    {
        private static readonly string FactorySalt = Sha3Keccack.Current.CalculateHash(nameof(WorldServiceTests));

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;
        private readonly Nethereum.Web3.Accounts.Account _user = new Nethereum.Web3.Accounts.Account(
            Nethereum.Signer.EthECKey.GenerateKey().GetPrivateKey(), EthereumClientIntegrationFixture.ChainId);

        private string WorldAddress { get; set; }
        private string UserAccount => _user.Address;

        public WorldServiceTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public async Task InitializeAsync()
        {
            var web3 = GetWeb3();
            var create2Service = web3.Eth.Create2DeterministicDeploymentProxyService;
            var proxyDeployment = await create2Service.GenerateEIP155DeterministicDeploymentUsingPreconfiguredSignatureAsync();
            var deployerProxy = await create2Service.DeployProxyAndGetContractAddressAsync(proxyDeployment);

            var worldFactoryDeployService = new WorldFactoryDeployService();
            var factoryAddresses = await worldFactoryDeployService.DeployWorldFactoryContractAndSystemDependenciesAsync(web3, deployerProxy, FactorySalt);
            var worldSalt = Sha3Keccack.Current.CalculateHash(Guid.NewGuid().ToString());
            WorldAddress = (await worldFactoryDeployService.DeployWorldAsync(web3, worldSalt, factoryAddresses)).NewContract;

            var registration = new RegistrationSystemService(web3, WorldAddress);
            await registration.RegisterNamespaceRequestAndWaitForReceiptAsync(
                ResourceEncoder.EncodeNamespace(new ItemTableRecord().Namespace));
            await registration.RegisterTableRequestAndWaitForReceiptAsync(new CounterTableRecord().GetSchemaEncoded().ToRegisterTableFunction());
            await registration.RegisterTableRequestAndWaitForReceiptAsync(new ItemTableRecord().GetSchemaEncoded().ToRegisterTableFunction());

            var incrementSystemDeployment = new IncrementSystemDeployment();
            var incrementSystemAddress = create2Service.CalculateCreate2Address(incrementSystemDeployment, deployerProxy, FactorySalt);
            await create2Service.DeployContractRequestAndWaitForReceiptAsync(incrementSystemDeployment, deployerProxy, FactorySalt);
            var incrementSystemId = ResourceEncoder.EncodeRootSystem("Increment");
            await registration.RegisterSystemRequestAndWaitForReceiptAsync(incrementSystemId, incrementSystemAddress, true);
            foreach (var function in new IncrementSystemService(web3, WorldAddress).GetAllFunctionABIs())
            {
                await registration.RegisterRootFunctionSelectorRequestAndWaitForReceiptAsync(new RegisterRootFunctionSelectorFunction
                {
                    SystemId = incrementSystemId,
                    SystemFunctionSignature = function.Signature,
                    WorldFunctionSignature = function.Signature
                });
            }

            var world = GetWorldService();
            await world.ContractHandler.SendRequestAndWaitForReceiptAsync(new IncrementFunction());
            await world.ContractHandler.SendRequestAndWaitForReceiptAsync(new IncrementFunction());

            await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(UserAccount, 1m);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        public WorldService GetWorldService()
        {
            return new WorldService(GetWeb3(), WorldAddress);
        }

        public IWeb3 GetWeb3()
        {
            return _ethereumClientIntegrationFixture.GetWeb3();
        }

        public WorldService GetUserWorldService()
        {
            return new WorldService(new Nethereum.Web3.Web3(_user, _ethereumClientIntegrationFixture.GetClient()), WorldAddress);
        }

        [Fact]
        public async Task ShouldReturnRightVersion()
        {
            var worldService = GetWorldService();
            var storeVersion = await worldService.StoreVersionQueryAsStringAsync();
            Assert.Equal("2.0.0", storeVersion);
        }


        [Function("increment")]
        public class IncrementFunction : FunctionMessage
        {
        }

        [Fact]
        public void ShouldGenerateResourceIdsOnDifferentAbstractImplementations()
        {
            var counterTableResourceId = new CounterTableRecord().ResourceIdEncoded;
            var counterTableResourceId2 = ResourceRegistry.GetResourceEncoded<CounterTableRecord>();
            Assert.Equal(counterTableResourceId.ToHex(), counterTableResourceId2.ToHex());

            var itemTableResourceId = new ItemTableRecord().ResourceIdEncoded;
            var itemTableResourceId2 = ResourceRegistry.GetResourceEncoded<ItemTableRecord>();
            Assert.Equal(itemTableResourceId.ToHex(), itemTableResourceId2.ToHex());

            Assert.NotEqual(counterTableResourceId.ToHex(), itemTableResourceId.ToHex());

        }


        [Fact]
        public async Task ShouldGetRecord()
        {
            var worldService = GetWorldService();
            var receipt = await worldService.ContractHandler.SendRequestAndWaitForReceiptAsync(new IncrementFunction());
            var record = await worldService.GetRecordQueryAsync("Counter");
        }

        [Fact]
        public async Task ShouldGetRecordUsingTable()
        {
            var worldService = GetWorldService();
            var receipt = await worldService.ContractHandler.SendRequestAndWaitForReceiptAsync(new IncrementFunction());
            var record = await worldService.GetRecordTableQueryAsync<CounterTableRecord, CounterTableRecord.CounterValue>(new CounterTableRecord());
            Assert.True(record.Values.Value > 0);
        }

      

        [Fact]
        public async Task ShouldSetAndGetAccessTable()
        {
            var worldService = GetWorldService();

            //first we are going to try to set the record on the resource table without direct table access
            var resourceAccess = new ResourceAccessTableRecord();
            resourceAccess.Keys.ResourceId = new CounterTableRecord().ResourceIdEncoded;
            resourceAccess.Keys.Caller = UserAccount;
            resourceAccess.Values.Access = true;
            try
            {
                var receipt = await worldService.SetRecordRequestAndWaitForReceiptAsync(resourceAccess);
            } 
            catch (SmartContractCustomErrorRevertException e)
            {
                Assert.True(e.IsCustomErrorFor<WorldAccessdeniedError>());  
                var error = e.DecodeError<WorldAccessdeniedError>();
                Assert.Equal("tb:world:ResourceAccess", error.Resource);
                Assert.True(error.Caller.IsTheSameAddress(worldService.Web3.TransactionManager.Account.Address));
                
                
            }

            //using the access management system
            //first owner account 
            var accessSystem = new AccessManagementSystemService(worldService.Web3, worldService.ContractAddress);
            var receipt2 = await accessSystem.GrantAccessRequestAndWaitForReceiptAsync(ResourceRegistry.GetResourceEncoded<CounterTableRecord>(), worldService.Web3.TransactionManager.Account.Address);
            var resourceAccessTable = new ResourceAccessTableRecord();
            resourceAccessTable.Keys.ResourceId = ResourceRegistry.GetResourceEncoded<CounterTableRecord>();
            resourceAccessTable.Keys.Caller = worldService.Web3.TransactionManager.Account.Address;
            var record = await worldService.GetRecordTableQueryAsync<ResourceAccessTableRecord, ResourceAccessTableRecord.ResourceAccessKey, ResourceAccessTableRecord.ResourceAccessValue>(resourceAccessTable);
            Assert.True(record.Values.Access);

            //then another account
            var receipt3 = await accessSystem.GrantAccessRequestAndWaitForReceiptAsync(ResourceRegistry.GetResourceEncoded<CounterTableRecord>(), UserAccount);
            resourceAccessTable.Keys.Caller = UserAccount;
            record = await worldService.GetRecordTableQueryAsync<ResourceAccessTableRecord, ResourceAccessTableRecord.ResourceAccessKey, ResourceAccessTableRecord.ResourceAccessValue>(resourceAccessTable);
            Assert.True(record.Values.Access);

            //we can now set the value using the user account directly in the table counter
            var counterTable = new CounterTableRecord();
            counterTable.Values.Value = 2500;
            var worldServiceUser = GetUserWorldService();
           
            receipt3 = await worldServiceUser.SetRecordRequestAndWaitForReceiptAsync(counterTable); 
            var recordCounter = await worldService.GetRecordTableQueryAsync<CounterTableRecord, CounterTableRecord.CounterValue>(new CounterTableRecord());
            Assert.True(recordCounter.Values.Value == 2500);

            //revoke access
            var receipt4 = await accessSystem.RevokeAccessRequestAndWaitForReceiptAsync(ResourceRegistry.GetResourceEncoded<CounterTableRecord>(), UserAccount);

            //we cannot set the value anymore
            var counterTable2 = new CounterTableRecord();
            counterTable2.Values.Value = 3000;
            try
            {
                var receipt5 = await worldServiceUser.SetRecordRequestAndWaitForReceiptAsync(counterTable2);
            }
            catch (SmartContractCustomErrorRevertException e)
            {
                Assert.True(e.IsCustomErrorFor<WorldAccessdeniedError>());
                var error = e.DecodeError<WorldAccessdeniedError>();
                Assert.Equal("tb:<root>:Counter", error.Resource);
                Assert.True(error.Caller.IsTheSameAddress(UserAccount));
            }

            //but we can still set the value using the system as it is open
            var receipt6 = await worldServiceUser.ContractHandler.SendRequestAndWaitForReceiptAsync(new IncrementFunction());
            recordCounter = await worldService.GetRecordTableQueryAsync<CounterTableRecord, CounterTableRecord.CounterValue>(new CounterTableRecord());
            //the value should have been incremented by 1 of the previous value we set 2000
            Assert.True(recordCounter.Values.Value == 2501);

        }

        [Fact]
        public async Task ShouldSetRecord()
        {
            var worldService = GetWorldService();
            var receipt = await worldService.SetRecordRequestAndWaitForReceiptAsync("Counter", ABIType.CreateABIType("int32").EncodePacked(8));
            var record = await worldService.GetRecordQueryAsync("Counter");
            Assert.True(record.StaticData.ToBigIntegerFromRLPDecoded() == 8);
        }



        [Fact]
        public async Task ShouldSetAndGetRecordTableItem()
        {
            var worldService = GetWorldService();
            var itemTable = new ItemTableRecord();
            itemTable.Values.Name = "Item1";
            itemTable.Values.Price = 100;
            itemTable.Values.Description = "Description";
            itemTable.Values.Owner = "Owner";
            itemTable.Keys.Id = 1;
            var receipt = await worldService.SetRecordRequestAndWaitForReceiptAsync(itemTable);
            var returnedItem = new ItemTableRecord();
            returnedItem.Keys.Id = 1;
            returnedItem = await worldService.GetRecordTableQueryAsync<ItemTableRecord, ItemTableRecord.ItemKey, ItemTableRecord.ItemValue>(returnedItem);
            Assert.Equal("Item1", returnedItem.Values.Name);
            Assert.Equal((uint)100, returnedItem.Values.Price);
            Assert.Equal("Description", returnedItem.Values.Description);
            Assert.Equal("Owner", returnedItem.Values.Owner);
            Assert.Equal((uint)1, returnedItem.Keys.Id);
        }

        [Fact]
        public async Task ShouldSetRecordTable()
        {
            var worldService = GetWorldService();
            var counterTable = new CounterTableRecord();
            counterTable.Values.Value = 10;
            var receipt = await worldService.SetRecordRequestAndWaitForReceiptAsync(counterTable);
            var record = await worldService.GetRecordQueryAsync("Counter");
        }

        [Fact]
        public async Task ShouldGetSetRecordsFromLogs()
        {
            var worldService = GetWorldService();
            foreach (var value in new[] { 10, 20 })
            {
                var counter = new CounterTableRecord();
                counter.Values.Value = value;
                await worldService.SetRecordRequestAndWaitForReceiptAsync(counter);
            }

            var web3 = GetWeb3();
            var storeLogProcessingService = new StoreEventsLogProcessingService(web3, WorldAddress);
            var setRecords = await storeLogProcessingService.GetAllSetRecordForTable( "Counter", null, null, CancellationToken.None);

            var counterTable0 = new CounterTableRecord();
            counterTable0.DecodeValues(setRecords[0].Event.StaticData, setRecords[0].Event.EncodedLengths, setRecords[0].Event.DynamicData);
            Assert.True(counterTable0.Values.Value > 0);

            var counterTable1 = new CounterTableRecord();
            counterTable1.DecodeValues(setRecords[1].Event.StaticData, setRecords[1].Event.EncodedLengths, setRecords[1].Event.DynamicData);
            Assert.True(counterTable1.Values.Value > 0);

            foreach (var setRecord in setRecords)
            {
                var counterTable = new CounterTableRecord(); 
                counterTable.DecodeValues(setRecord.Event.StaticData, setRecord.Event.EncodedLengths, setRecord.Event.DynamicData);
                Debug.WriteLine(counterTable.Values.Value);
                Assert.True(counterTable.Values.Value > 0);
            }

        }


        [Fact]
        public async Task ShouldGetAllChanges()
        {
            var web3 = GetWeb3();
            var storeLogProcessingService = new StoreEventsLogProcessingService(web3, WorldAddress);
            var inMemoryStore = new InMemoryTableRepository();
            var tableId = new CounterTableRecord().ResourceIdEncoded;
            await storeLogProcessingService.ProcessAllStoreChangesAsync(inMemoryStore, null, null, CancellationToken.None);
            var results = await inMemoryStore.GetTableRecordsAsync<CounterTableRecord>(tableId);

            Assert.True(results.ToList()[0].Values.Value> 0);

            var resultsSystems = await inMemoryStore.GetTableRecordsAsync<SystemsTableRecord>(new SystemsTableRecord().ResourceIdEncoded);
            Assert.True(resultsSystems.ToList().Count > 0);
            foreach (var result in resultsSystems)
            {
                Debug.WriteLine(ResourceEncoder.Decode(result.Keys.SystemId).Name);
                Debug.WriteLine(ResourceEncoder.Decode(result.Keys.SystemId).Namespace);
            }

            var resultsAccess = await inMemoryStore.GetTableRecordsAsync<ResourceAccessTableRecord>(new ResourceAccessTableRecord().ResourceIdEncoded);
            Assert.True(resultsAccess.ToList().Count > 0);
            foreach (var result in resultsAccess)
            {
                Debug.WriteLine(ResourceEncoder.Decode(result.Keys.ResourceId).Name);
                Debug.WriteLine(result.Keys.Caller);
                Debug.WriteLine(result.Values.Access);
            }


            //the world factory is the owner of the store and world namespaces
            var namespaceOwner = await inMemoryStore.GetTableRecordsAsync<NamespaceOwnerTableRecord>(new NamespaceOwnerTableRecord().ResourceIdEncoded);
            Assert.True(namespaceOwner.ToList().Count > 0);
            foreach (var result in namespaceOwner)
            {
                Debug.WriteLine(ResourceEncoder.Decode(result.Keys.NamespaceId).Name);
                Debug.WriteLine(ResourceEncoder.Decode(result.Keys.NamespaceId).Namespace);
                Debug.WriteLine(result.Values.Owner);
            }

            var itemTableResults = await inMemoryStore.GetTableRecordsAsync<ItemTableRecord>(new ItemTableRecord().ResourceIdEncoded);
            Assert.True(itemTableResults.ToList().Count >= 0); // we many not have set a record yet
            foreach (var result in itemTableResults)
            {
                Debug.WriteLine(result.Keys.Id);
                Debug.WriteLine(result.Values.Name);
                Debug.WriteLine(result.Values.Price);
                Debug.WriteLine(result.Values.Description);
                Debug.WriteLine(result.Values.Owner);
            }

        }

        [Fact]
        public async Task ShouldGetAllChangesForASingleTable()
        {
            var web3 = GetWeb3();
            var storeLogProcessingService = new StoreEventsLogProcessingService(web3, WorldAddress);
            var inMemoryStore = new InMemoryTableRepository();
            var tableId = new CounterTableRecord().ResourceIdEncoded;
            await storeLogProcessingService.ProcessAllStoreChangesAsync(inMemoryStore, tableId, null, null, CancellationToken.None);
            var results = await inMemoryStore.GetTableRecordsAsync<CounterTableRecord>(tableId);

            Assert.True(results.ToList()[0].Values.Value > 0);

        }

        [Fact]
        public async Task ShouldGetAllTablesRegistered()
        {
            var web3 = GetWeb3();
            var storeLogProcessingService = new StoreEventsLogProcessingService(web3, WorldAddress);
            var inMemoryStore = new InMemoryTableRepository();
            var tableId = new TablesTableRecord().ResourceIdEncoded;
            await storeLogProcessingService.ProcessAllStoreChangesAsync(inMemoryStore, tableId, null, null, CancellationToken.None);
            var results = await inMemoryStore.GetTableRecordsAsync<TablesTableRecord>(tableId);

            Assert.True(results.ToList().Count > 0);
            
            foreach (var result in results)
            {
                var recordTableId = result.Keys.GetTableIdResource();

                if(recordTableId.Name == "Counter")
                {
                    Assert.True(recordTableId.IsRoot());
                    var field = result.Values.GetValueSchemaFields().ToList()[0];
                    Assert.Equal("uint32", field.Type);
                    Assert.Equal(1, field.Order);
                    Assert.Equal("value", field.Name);
                }

                if(recordTableId.Name == "Item")
                {
                    Assert.Equal(new ItemTableRecord().Namespace, recordTableId.Namespace);
                    var fields = result.Values.GetValueSchemaFields().ToList();
                   
                    Assert.Equal("uint32", fields[0].Type);
                    Assert.Equal(1, fields[0].Order);
                    Assert.Equal("price", fields[0].Name);
                    Assert.Equal("string", fields[1].Type);
                    Assert.Equal(2, fields[1].Order);
                    Assert.Equal("name", fields[1].Name);
                    Assert.Equal("string", fields[2].Type);
                    Assert.Equal(3, fields[2].Order);
                    Assert.Equal("description", fields[2].Name);
                    Assert.Equal("string", fields[3].Type);
                    Assert.Equal(4, fields[3].Order);
                    Assert.Equal("owner", fields[3].Name);

                    var keys = result.Values.GetKeySchemaFields().ToList();
                    Assert.Equal("uint32", keys[0].Type);
                    Assert.Equal(1, keys[0].Order);
                    Assert.Equal("id", keys[0].Name);
                     
                }

                Debug.WriteLine(recordTableId.Name);
                Debug.WriteLine(recordTableId.Namespace);
                Debug.WriteLine("Field schema");
                foreach (var field in result.Values.GetValueSchemaFields())
                {
                    Debug.WriteLine(field.Name);
                    Debug.WriteLine(field.Type);
                    Debug.WriteLine(field.Order);
                    
                }
                Debug.WriteLine("Key Schema");
                foreach (var field in result.Values.GetKeySchemaFields())
                {
                    Debug.WriteLine(field.Name);
                    Debug.WriteLine(field.Type);
                    Debug.WriteLine(field.Order);
                }
                
            }
        }


    }
}