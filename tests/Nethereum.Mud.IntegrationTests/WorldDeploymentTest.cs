using Nethereum.Contracts;
using Nethereum.Mud.Contracts;
using Nethereum.Mud.Contracts.RegistrationSystem;
using Nethereum.Mud.Contracts.RegistrationSystem.ContractDefinition;
using Nethereum.Mud.Contracts.StoreEvents;
using Nethereum.Mud.Contracts.Tables.World;
using Nethereum.Mud.Contracts.World;
using Nethereum.Mud.EncodingDecoding;
using Nethereum.Mud.TableRepository;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Diagnostics;
using static Nethereum.Mud.IntegrationTests.WorldServiceTests;
using Mud.IntegrationTests.IncrementSystem.ContractDefinition;
using Mud.IntegrationTests.IncrementSystem;
using static Nethereum.Mud.IntegrationTests.WorldServiceTests.CounterTable;

namespace Nethereum.Mud.IntegrationTests
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class WorldDeploymentTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public WorldDeploymentTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async Task ShouldDeployWorldContractRegisterTablesSystemAndInteract()
        {
            
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            web3.TransactionManager.UseLegacyAsDefault = true;
            var random = new Random();
            //random salt in case we have an existing contract deployed
            var salt = Nethereum.Util.Sha3Keccack.Current.CalculateHash(random.Next(0, 1000000).ToString());
            
            var create2DeterministicDeploymentProxyService = web3.Eth.Create2DeterministicDeploymentProxyService;

            var proxyCreate2Deployment = await create2DeterministicDeploymentProxyService.GenerateEIP155DeterministicDeploymentUsingPreconfiguredSignatureAsync();
            var addressDeployer = await create2DeterministicDeploymentProxyService.DeployProxyAndGetContractAddressAsync(proxyCreate2Deployment);

            var worldFactoryDeployerService = new WorldFactoryDeployService();
            var worldFactoryAddresses = await worldFactoryDeployerService.DeployWorldFactoryContractAndSystemDependenciesAsync(web3, addressDeployer, salt);
            var worldEvent = await worldFactoryDeployerService.DeployWorldAsync(web3, salt, worldFactoryAddresses);
            var worldAddress = worldEvent.NewContract;
            var worldService = new WorldService(web3, worldEvent.NewContract);
            var version = await worldService.StoreVersionQueryAsStringAsync();
            Assert.Equal("2.0.0", version);

            var storeLogProcessingService = new StoreEventsLogProcessingService(web3);
            var inMemoryStore = new InMemoryTableRepository();
            await storeLogProcessingService.ProcessAllStoreChangesAsync(inMemoryStore, worldAddress, null, null, CancellationToken.None);

            var resultsSystems = await inMemoryStore.GetTableRecordsAsync<SystemsTableRecord>(new SystemsTableRecord().ResourceId);
            Assert.True(resultsSystems.ToList().Count > 0);

            var registrationSystemService = new RegistrationSystemService(web3, worldAddress);
            var nameSpaceReceipt = registrationSystemService.RegisterNamespaceRequestAndWaitForReceiptAsync(
                ResourceEncoder.EncodeNamesapce(String.Empty));


            var counterSchemaEncoded = new CounterTable().GetSchemaEncoded();
            var registerTableFunction = counterSchemaEncoded.ToRegisterTableFunction();

            TransactionReceipt receipt = null;

            receipt = await registrationSystemService.RegisterTableRequestAndWaitForReceiptAsync(registerTableFunction);
           
            var itemsTableSchemaEncoded = new ItemTable().GetSchemaEncoded();
            var registerItemsTableFunction = itemsTableSchemaEncoded.ToRegisterTableFunction();
            try
            {
                receipt = await registrationSystemService.RegisterTableRequestAndWaitForReceiptAsync(registerItemsTableFunction);
                var events = receipt.DecodeAllEvents<StoreSetrecordEventDTO>();
            }
            catch (SmartContractCustomErrorRevertException e)
            {
                var errorTypes = registrationSystemService.GetAllErrorTypes();
                foreach (var errorType in errorTypes)
                {
                    if (e.IsCustomErrorFor(errorType))
                    {
                        Debug.WriteLine(errorType.ToString());
                        throw;
                    }
                }

            }

            var storeLogProcessor = new StoreEventsLogProcessingService(web3);

            var tables = await storeLogProcessingService.GetTablesTableRecordsFromLogsAsync(worldAddress, null, null, CancellationToken.None);
            var counterTableSchema = tables.Where(x => x.Keys.GetTableIdResource().Name == "Counter").FirstOrDefault();
            var itemTableSchema = tables.Where(x => x.Keys.GetTableIdResource().Name == "Item").FirstOrDefault();

            var counterTableResourceId = counterTableSchema.Keys.GetTableIdResource();
            Assert.True(counterTableResourceId.IsRoot);
            var field = counterTableSchema.Values.GetValueSchemaFields().ToList()[0];
            Assert.Equal("uint32", field.Type);
            Assert.Equal(1, field.Order);
            Assert.Equal("value", field.Name);

            var itemTableResource = itemTableSchema.Keys.GetTableIdResource();
            Assert.True(itemTableResource.IsRoot);
            var itemFields = itemTableSchema.Values.GetValueSchemaFields().ToList();
            var itemKeys = itemTableSchema.Values.GetKeySchemaFields().ToList();

            Assert.Equal("uint32", itemFields[0].Type);
            Assert.Equal(1, itemFields[0].Order);
            Assert.Equal("price", itemFields[0].Name);
            Assert.Equal("string", itemFields[1].Type);
            Assert.Equal(2, itemFields[1].Order);
            Assert.Equal("name", itemFields[1].Name);
            Assert.Equal("string", itemFields[2].Type);
            Assert.Equal(3, itemFields[2].Order);
            Assert.Equal("description", itemFields[2].Name);
            Assert.Equal("string", itemFields[3].Type);
            Assert.Equal(4, itemFields[3].Order);
            Assert.Equal("owner", itemFields[3].Name);


            Assert.Equal("uint32", itemKeys[0].Type);
            Assert.Equal(1, itemKeys[0].Order);
            Assert.Equal("id", itemKeys[0].Name);


            var incrementSystemDeployment = new IncrementSystemDeployment();
            var incrementSystemAddress = create2DeterministicDeploymentProxyService.CalculateCreate2Address(incrementSystemDeployment, addressDeployer, salt);
            var receiptIncrementSystemDeployment = await create2DeterministicDeploymentProxyService.DeployContractRequestAndWaitForReceiptAsync(incrementSystemDeployment, addressDeployer, salt);

            var incrementSystemId = ResourceEncoder.EncodeRootSystem("Increment");
            var registrationIncrementSystemReceipt = await registrationSystemService.RegisterSystemRequestAndWaitForReceiptAsync(incrementSystemId, incrementSystemAddress, true);
            

            var registeredFunctions = await storeLogProcessingService.GetFunctionSelectorsTableRecordsFromLogsAsync(worldAddress, null, null, CancellationToken.None);
            var registeredSelectors = registeredFunctions.Select(x => x.Values.SystemFunctionSelector.ToString()).ToList();
            registeredSelectors.AddRange(new SystemDefaultFunctions().GetAllFunctionSignatures().ToList());
            var incrementSystemService = new IncrementSystemService(web3, worldAddress);
            var functionAbis = incrementSystemService.GetAllFunctionABIs();
            var functionSelectorsToRegister = functionAbis.Where(x => !registeredSelectors.Any(y => y.IsTheSameHex(x.Sha3Signature))).ToList();
           
            foreach (var functionSelectorToRegister in functionSelectorsToRegister)
            {
                var registerFunction = new RegisterRootFunctionSelectorFunction();
                registerFunction.SystemFunctionSignature = functionSelectorToRegister.Signature;
                registerFunction.WorldFunctionSignature = functionSelectorToRegister.Signature;
                registerFunction.SystemId = incrementSystemId;
                
                await registrationSystemService.RegisterRootFunctionSelectorRequestAndWaitForReceiptAsync(registerFunction);
                
            }

            await incrementSystemService.IncrementRequestAndWaitForReceiptAsync();
            
            var counterRecord = await worldService.GetRecordTableQueryAsync<CounterTable, CounterValue>(new CounterTable());
            Assert.Equal(1, counterRecord.Values.Value);

            await incrementSystemService.IncrementRequestAndWaitForReceiptAsync();

            counterRecord = await worldService.GetRecordTableQueryAsync<CounterTable, CounterValue>(new CounterTable());
            Assert.Equal(2, counterRecord.Values.Value);

        }
    }
}
   
