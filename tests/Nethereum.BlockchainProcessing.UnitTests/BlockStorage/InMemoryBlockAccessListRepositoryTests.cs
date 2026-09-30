using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.BlockchainProcessing.UnitTests.BlockStorage
{
    public class InMemoryBlockAccessListRepositoryTests
    {
        [Fact]
        public async Task Upsert_Then_GetForBlock_Returns_The_Accounts()
        {
            var records = new List<BlockAccessListAccount>();
            var repository = new InMemoryBlockAccessListRepository(records);

            var accountOne = new AccountAccess { Address = "0x1111111111111111111111111111111111111111" };
            var accountTwo = new AccountAccess { Address = "0x2222222222222222222222222222222222222222" };

            await repository.UpsertAsync(accountOne, 100, "0xblockhash");
            await repository.UpsertAsync(accountTwo, 100, "0xblockhash");
            await repository.UpsertAsync(new AccountAccess { Address = accountOne.Address }, 101, "0xotherhash");

            var forBlock100 = await repository.GetForBlockAsync(100);

            Assert.Equal(2, forBlock100.Count);
            Assert.Contains(forBlock100, a => a.Address == accountOne.Address);
            Assert.Contains(forBlock100, a => a.Address == accountTwo.Address);
        }

        [Fact]
        public async Task Upsert_Replaces_Existing_Record_For_Same_Block_And_Address()
        {
            var records = new List<BlockAccessListAccount>();
            var repository = new InMemoryBlockAccessListRepository(records);

            var account = new AccountAccess
            {
                Address = "0x1111111111111111111111111111111111111111",
                NonceChanges = new List<NonceChange> { new NonceChange { Index = "0x0", Value = "0x1" } }
            };

            await repository.UpsertAsync(account, 100, "0xblockhash");

            account.NonceChanges = new List<NonceChange> { new NonceChange { Index = "0x0", Value = "0x2" } };
            await repository.UpsertAsync(account, 100, "0xblockhash");

            var forBlock100 = await repository.GetForBlockAsync(100);

            Assert.Single(forBlock100);
            Assert.Equal("0x2", forBlock100.Single().NonceChanges.Single().Value);
        }

        [Fact]
        public async Task MarkNonCanonicalAsync_Excludes_The_Block_From_GetForBlock()
        {
            var records = new List<BlockAccessListAccount>();
            var repository = new InMemoryBlockAccessListRepository(records);

            var account = new AccountAccess { Address = "0x1111111111111111111111111111111111111111" };
            await repository.UpsertAsync(account, 100, "0xblockhash");

            await repository.MarkNonCanonicalAsync(100);

            var forBlock100 = await repository.GetForBlockAsync(100);

            Assert.Empty(forBlock100);
        }
    }
}
