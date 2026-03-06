using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.IntegrationTests.E2E.Fixtures;
using Nethereum.AppChain.IntegrationTests.E2E.Tables;
using Nethereum.AppChain.Sequencer.Builder;
using Nethereum.Mud.Contracts;
using Nethereum.Mud.Contracts.World;
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem;
using Nethereum.RPC.Eth.DTOs;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.E2E.Scenarios
{
    [Collection("Sequential")]
    [Trait("Category", "AppChainBuilder-E2E")]
    public class MudGamingChainTests : E2EScenarioFixture, IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private string? _worldAddress;
        private WorldService? _worldService;
        private WorldFactoryContractAddresses? _worldFactoryAddresses;

        public MudGamingChainTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            Chain = await new AppChainBuilder("MudGameChain", DEFAULT_CHAIN_ID)
                .WithOperator(OPERATOR_PRIVATE_KEY)
                .WithTrust(TrustModel.Open)
                .WithBaseFee(0)
                .WithPrefundedAddresses(GetTestAddresses())
                .BuildAsync();

            await DeployWorldAsync();
        }

        private async Task DeployWorldAsync()
        {
            var web3 = GetOperatorWeb3();
            var create2Address = Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS;
            var salt = "0x0000000000000000000000000000000000000000000000000000000000000001";

            var deployService = new WorldFactoryDeployService();
            _worldFactoryAddresses = await deployService.DeployWorldFactoryContractAndSystemDependenciesAsync(
                web3, create2Address, salt);

            _output.WriteLine($"MUD World Factory deployed at: {_worldFactoryAddresses.WorldFactoryAddress}");

            var worldSalt = "0x0000000000000000000000000000000000000000000000000000000000000042";
            var worldEvent = await deployService.DeployWorldAsync(web3, worldSalt, _worldFactoryAddresses);

            _worldAddress = worldEvent.NewContract;
            _worldService = new WorldService(web3, _worldAddress);

            _output.WriteLine($"MUD World deployed at: {_worldAddress}");
        }

        private async Task<TransactionReceipt> RegisterPlayerTableAsync()
        {
            var player = new PlayerTableRecord();
            var schemaEncoded = player.GetSchemaEncoded();
            var registerFunction = schemaEncoded.ToRegisterTableFunction();
            return await _worldService!.ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction);
        }

        private async Task<TransactionReceipt> RegisterGameItemTableAsync()
        {
            var item = new GameItemTableRecord();
            var schemaEncoded = item.GetSchemaEncoded();
            var registerFunction = schemaEncoded.ToRegisterTableFunction();
            return await _worldService!.ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction);
        }

        [Fact]
        public async Task Given_MudChain_When_WorldDeployed_Then_WorldAccessible()
        {
            Assert.NotNull(_worldAddress);
            Assert.NotNull(_worldService);

            var worldCode = await Chain!.AppChain.GetCodeAsync(_worldAddress!);
            Assert.NotNull(worldCode);
            Assert.True(worldCode.Length > 0, "World contract should have bytecode");

            _output.WriteLine($"World at {_worldAddress} has {worldCode.Length} bytes of code");
        }

        [Fact]
        public async Task Given_MudWorld_When_PlayerTableRegistered_Then_CanWriteAndRead()
        {
            var receipt = await RegisterPlayerTableAsync();
            Assert.True(receipt.Succeeded(), "Player table registration should succeed");
            _output.WriteLine($"Player table registered, gas used: {receipt.GasUsed}");

            var player = new PlayerTableRecord
            {
                Keys = { PlayerId = TestAccounts[0].Address },
                Values =
                {
                    Name = "TestPlayer",
                    Score = new BigInteger(1000),
                    Level = 5,
                    LastActive = new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    IsActive = true
                }
            };

            var writeReceipt = await _worldService!.SetRecordRequestAndWaitForReceiptAsync(player);
            Assert.True(writeReceipt.Succeeded(), "Writing player should succeed");
            _output.WriteLine($"Player written, gas used: {writeReceipt.GasUsed}");

            var readPlayer = new PlayerTableRecord { Keys = { PlayerId = TestAccounts[0].Address } };
            var result = await _worldService.GetRecordTableQueryAsync<
                PlayerTableRecord, PlayerTableRecord.PlayerKey, PlayerTableRecord.PlayerValue>(readPlayer);

            Assert.Equal("TestPlayer", result.Values.Name);
            Assert.Equal(new BigInteger(1000), result.Values.Score);
            Assert.Equal(5, result.Values.Level);
            Assert.True(result.Values.IsActive);

            _output.WriteLine($"Read player: Name={result.Values.Name}, Score={result.Values.Score}, Level={result.Values.Level}");
        }

        [Fact]
        public async Task Given_MudWorld_When_MultiplePlayersInteract_Then_StateUpdatesCorrectly()
        {
            await RegisterPlayerTableAsync();

            for (int i = 0; i < 3; i++)
            {
                var player = new PlayerTableRecord
                {
                    Keys = { PlayerId = TestAccounts[i].Address },
                    Values =
                    {
                        Name = $"Player{i + 1}",
                        Score = new BigInteger((i + 1) * 100),
                        Level = i + 1,
                        LastActive = new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                        IsActive = true
                    }
                };

                await _worldService!.SetRecordRequestAndWaitForReceiptAsync(player);
                _output.WriteLine($"Created Player{i + 1}");
            }

            var player1Update = new PlayerTableRecord
            {
                Keys = { PlayerId = TestAccounts[0].Address },
                Values =
                {
                    Name = "Player1",
                    Score = new BigInteger(500),
                    Level = 10,
                    LastActive = new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    IsActive = true
                }
            };

            await _worldService!.SetRecordRequestAndWaitForReceiptAsync(player1Update);
            _output.WriteLine("Updated Player1: Score=500, Level=10");

            var readPlayer1 = new PlayerTableRecord { Keys = { PlayerId = TestAccounts[0].Address } };
            var result = await _worldService.GetRecordTableQueryAsync<
                PlayerTableRecord, PlayerTableRecord.PlayerKey, PlayerTableRecord.PlayerValue>(readPlayer1);

            Assert.Equal(new BigInteger(500), result.Values.Score);
            Assert.Equal(10, result.Values.Level);

            _output.WriteLine($"Verified Player1 update: Score={result.Values.Score}, Level={result.Values.Level}");
        }

        [Fact]
        public async Task Given_MudWorld_When_ItemTableUpdated_Then_OwnershipTransfers()
        {
            await RegisterGameItemTableAsync();

            var originalOwner = TestAccounts[0].Address;
            var newOwner = TestAccounts[1].Address;

            var item = new GameItemTableRecord
            {
                Keys = { ItemId = 1 },
                Values =
                {
                    Name = "Legendary Sword",
                    ItemType = ItemType.Weapon,
                    Power = 100,
                    Rarity = Rarity.Legendary,
                    Owner = originalOwner,
                    Equipped = true
                }
            };

            await _worldService!.SetRecordRequestAndWaitForReceiptAsync(item);
            _output.WriteLine($"Created Legendary Sword owned by {originalOwner}");

            var readBefore = new GameItemTableRecord { Keys = { ItemId = 1 } };
            var beforeTransfer = await _worldService.GetRecordTableQueryAsync<
                GameItemTableRecord, GameItemTableRecord.GameItemKey, GameItemTableRecord.GameItemValue>(readBefore);

            Assert.Equal(originalOwner, beforeTransfer.Values.Owner);

            item.Values.Owner = newOwner;
            item.Values.Equipped = false;

            await _worldService.SetRecordRequestAndWaitForReceiptAsync(item);
            _output.WriteLine($"Transferred Legendary Sword to {newOwner}");

            var afterTransfer = await _worldService.GetRecordTableQueryAsync<
                GameItemTableRecord, GameItemTableRecord.GameItemKey, GameItemTableRecord.GameItemValue>(readBefore);

            Assert.Equal(newOwner, afterTransfer.Values.Owner);
            Assert.False(afterTransfer.Values.Equipped);

            _output.WriteLine($"Verified ownership transfer: {originalOwner} -> {newOwner}");
        }

        [Fact]
        public async Task Given_MudWorld_When_MultipleItemsCreated_Then_CanQueryAll()
        {
            await RegisterGameItemTableAsync();

            var items = new[]
            {
                ("Iron Sword", ItemType.Weapon, 10, Rarity.Common),
                ("Steel Shield", ItemType.Armor, 15, Rarity.Uncommon),
                ("Health Potion", ItemType.Consumable, 0, Rarity.Common),
                ("Dragon Scale", ItemType.Quest, 0, Rarity.Legendary)
            };

            for (int i = 0; i < items.Length; i++)
            {
                var (name, itemType, power, rarity) = items[i];
                var item = new GameItemTableRecord
                {
                    Keys = { ItemId = i + 1 },
                    Values =
                    {
                        Name = name,
                        ItemType = itemType,
                        Power = power,
                        Rarity = rarity,
                        Owner = OperatorAccount.Address,
                        Equipped = false
                    }
                };

                await _worldService!.SetRecordRequestAndWaitForReceiptAsync(item);
                _output.WriteLine($"Created item {i + 1}: {name}");
            }

            for (int i = 0; i < items.Length; i++)
            {
                var query = new GameItemTableRecord { Keys = { ItemId = i + 1 } };
                var result = await _worldService!.GetRecordTableQueryAsync<
                    GameItemTableRecord, GameItemTableRecord.GameItemKey, GameItemTableRecord.GameItemValue>(query);

                Assert.Equal(items[i].Item1, result.Values.Name);
                _output.WriteLine($"Verified item {i + 1}: {result.Values.Name}");
            }
        }

        [Fact]
        public async Task Given_MudWorld_When_ItemDeleted_Then_DataCleared()
        {
            await RegisterGameItemTableAsync();

            var item = new GameItemTableRecord
            {
                Keys = { ItemId = 99 },
                Values =
                {
                    Name = "Temporary Item",
                    ItemType = ItemType.Consumable,
                    Power = 5,
                    Rarity = Rarity.Common,
                    Owner = OperatorAccount.Address,
                    Equipped = false
                }
            };

            await _worldService!.SetRecordRequestAndWaitForReceiptAsync(item);
            _output.WriteLine("Created temporary item");

            var beforeDelete = await _worldService.GetRecordTableQueryAsync<
                GameItemTableRecord, GameItemTableRecord.GameItemKey, GameItemTableRecord.GameItemValue>(
                new GameItemTableRecord { Keys = { ItemId = 99 } });

            Assert.Equal("Temporary Item", beforeDelete.Values.Name);

            var deleteReceipt = await _worldService.DeleteRecordRequestAndWaitForReceiptAsync(item);
            Assert.True(deleteReceipt.Succeeded());
            _output.WriteLine($"Deleted item, gas used: {deleteReceipt.GasUsed}");

            var afterDelete = await _worldService.GetRecordTableQueryAsync<
                GameItemTableRecord, GameItemTableRecord.GameItemKey, GameItemTableRecord.GameItemValue>(
                new GameItemTableRecord { Keys = { ItemId = 99 } });

            Assert.True(string.IsNullOrEmpty(afterDelete.Values.Name));
            Assert.Equal(0, afterDelete.Values.Power);

            _output.WriteLine("Verified item deletion - data cleared");
        }
    }
}
