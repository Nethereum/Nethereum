using Nethereum.Mud.Contracts.World;
using System.Diagnostics;
using Nethereum.ABI.Decoders;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.ABI;
using Nethereum.Mud.Contracts.AccessManagementSystem;
using static Nethereum.Mud.IntegrationTests.WorldServiceTests.CounterTable;
using static Nethereum.Mud.IntegrationTests.WorldServiceTests.ItemTable;
using Nethereum.Mud.Contracts.World.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Contracts.Tables.World;
using static Nethereum.Mud.Contracts.Tables.World.ResourceAccessTableRecord;

namespace Nethereum.Mud.IntegrationTests
{
    //This tests are using a vanilla world contract (using vanilla template) deployed to anvil
    //and this configuration
    //is used for the tests
       /*
         export default defineWorld({
              tables: {
                Counter: {
                  schema: {
                    value: "uint32",
                  },
                  key: [],
                },
                Item:{
                  schema:{
                    id:"uint32",
                    price:"uint32",
                    name:"string",
                    description:"string",
                    owner:"string",
                  },
                  key:["id"]
                }
              },
            });
        */
    public class WorldServiceTests
    {
        public const string WorldAddress = "0x29d8d3f29d98ff51bba2ad34f6ab888303783a36";
        public const string WorldUrl = "http://localhost:8545";
        //using default anvil private key
        public const string OwnerPK = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public const string UserPK = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
        public const string UserAccount = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

        public WorldService GetWorldService()
        {
            var web3 = new Nethereum.Web3.Web3(new Nethereum.Web3.Accounts.Account(OwnerPK), WorldUrl);
            return new WorldService(web3, WorldAddress);
        }

        public WorldService GetUserWorldService()
        {
            var web3 = new Nethereum.Web3.Web3(new Nethereum.Web3.Accounts.Account(UserPK), WorldUrl);
            return new WorldService(web3, WorldAddress);
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
            var counterTableResourceId = new CounterTable().ResourceId;
            var counterTableResourceId2 =  CounterTable.TableResourceId;
            Assert.Equal(counterTableResourceId.ToHex(), counterTableResourceId2.ToHex());

            var itemTableResourceId = new ItemTable().ResourceId;
            var itemTableResourceId2 = ItemTable.TableResourceId;
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
            var record = await worldService.GetRecordTableQueryAsync<CounterTable, CounterValue>(new CounterTable());
            Assert.True(record.Values.Value > 0);
        }

      

        [Fact]
        public async Task ShouldSetAndGetAccessTable()
        {
            var worldService = GetWorldService();

            //first we are going to try to set the record on the resource table without direct table access
            var resourceAccess = new ResourceAccessTableRecord();
            resourceAccess.Keys.ResourceId = new CounterTable().ResourceId;
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
            var receipt2 = await accessSystem.GrantAccessRequestAndWaitForReceiptAsync(CounterTable.TableResourceId, worldService.Web3.TransactionManager.Account.Address);
            var resourceAccessTable = new ResourceAccessTableRecord();
            resourceAccessTable.Keys.ResourceId = CounterTable.TableResourceId;
            resourceAccessTable.Keys.Caller = worldService.Web3.TransactionManager.Account.Address;
            var record = await worldService.GetRecordTableQueryAsync<ResourceAccessTableRecord, ResourceAccessKey, ResourceAccessValue>(resourceAccessTable);
            Assert.True(record.Values.Access);

            //then another account
            var receipt3 = await accessSystem.GrantAccessRequestAndWaitForReceiptAsync(CounterTable.TableResourceId, UserAccount);
            resourceAccessTable.Keys.Caller = UserAccount;
            record = await worldService.GetRecordTableQueryAsync<ResourceAccessTableRecord, ResourceAccessKey, ResourceAccessValue>(resourceAccessTable);
            Assert.True(record.Values.Access);

            //we can now set the value using the user account directly in the table counter
            var counterTable = new CounterTable();
            counterTable.Values.Value = 2000;
            var worldServiceUser = GetUserWorldService();
           
            receipt3 = await worldServiceUser.SetRecordRequestAndWaitForReceiptAsync(counterTable); 
            var recordCounter = await worldService.GetRecordTableQueryAsync<CounterTable, CounterValue>(new CounterTable());
            Assert.True(recordCounter.Values.Value == 2000);

            //revoke access
            var receipt4 = await accessSystem.RevokeAccessRequestAndWaitForReceiptAsync(CounterTable.TableResourceId, UserAccount);

            //we cannot set the value anymore
            var counterTable2 = new CounterTable();
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
            recordCounter = await worldService.GetRecordTableQueryAsync<CounterTable, CounterValue>(new CounterTable());
            //the value should have been incremented by 1 of the previous value we set 2000
            Assert.True(recordCounter.Values.Value == 2001);

        }

      

        public class CounterTable : TableRecordSingleton<CounterValue>
        {

            public CounterTable() : base("Counter")
            {
            }
            public class CounterValue
            {
                [Parameter("int32", 1)]
                public int Value { get; set; }
            }

        }

       

        public class ItemTable:TableRecord<ItemKey, ItemValue>
        {
            public ItemTable() : base("Item")
            {
                
            }

            public class ItemKey
            {
                [Parameter("uint32", 1)]
                public int Id { get; set; }
            }

            public class ItemValue
            {
                [Parameter("uint32", 1)]
                public int Price { get; set; }
                [Parameter("string", 2)]
                public string Name { get; set; }
                [Parameter("string", 3)]
                public string Description { get; set; }
                [Parameter("string", 4)]
                public string Owner { get; set; }
            }
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
            var itemTable = new ItemTable();
            itemTable.Values.Name = "Item1";
            itemTable.Values.Price = 100;
            itemTable.Values.Description = "Description";
            itemTable.Values.Owner = "Owner";
            itemTable.Keys.Id = 1;
            var receipt = await worldService.SetRecordRequestAndWaitForReceiptAsync(itemTable);
            var returnedItem = new ItemTable();
            returnedItem.Keys.Id = 1;
            returnedItem = await worldService.GetRecordTableQueryAsync<ItemTable, ItemKey, ItemValue>(returnedItem);
            Assert.Equal("Item1", returnedItem.Values.Name);
            Assert.Equal(100, returnedItem.Values.Price);
            Assert.Equal("Description", returnedItem.Values.Description);
            Assert.Equal("Owner", returnedItem.Values.Owner);
            Assert.Equal(1, returnedItem.Keys.Id);
        }

        [Fact]
        public async Task ShouldSetRecordTable()
        {
            var worldService = GetWorldService();
            var counterTable = new CounterTable();
            counterTable.Values.Value = 10;
            var receipt = await worldService.SetRecordRequestAndWaitForReceiptAsync(counterTable);
            var record = await worldService.GetRecordQueryAsync("Counter");
        }

      





    }
}