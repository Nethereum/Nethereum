using System.Collections.Generic;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Entities.Mapping;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.BlockchainProcessing.UnitTests.BlockStorage.Entities.Mapping
{
    public class BlockAccessListAccountMappingTests
    {
        private static AccountAccess CreateAccountAccess()
        {
            return new AccountAccess
            {
                Address = "0x5f236f062f16a9b19819c535127398df9a01d762",
                StorageReads = new List<string> { "0x01", "0x02" },
                StorageChanges = new List<SlotChanges>
                {
                    new SlotChanges
                    {
                        Key = "0x03",
                        Changes = new List<StorageChange>
                        {
                            new StorageChange { Index = "0x0", Value = "0x64" }
                        }
                    }
                },
                BalanceChanges = new List<BalanceChange>
                {
                    new BalanceChange { Index = "0x0", Value = "0x1bc16d674ec80000" }
                },
                NonceChanges = new List<NonceChange>
                {
                    new NonceChange { Index = "0x0", Value = "0x1" }
                },
                CodeChanges = new List<CodeChange>
                {
                    new CodeChange { Index = "0x0", Code = "0x6001600101" }
                }
            };
        }

        [Fact]
        public void Maps_AccountAccess_To_Entity()
        {
            var source = CreateAccountAccess();

            var entity = source.MapToStorageEntityForUpsert(100, "0xabc");

            Assert.Equal(100, entity.BlockNumber);
            Assert.Equal("0xabc", entity.BlockHash);
            Assert.Equal(source.Address, entity.Address);
            Assert.True(entity.IsCanonical);
            Assert.Contains("0x01", entity.StorageReads);
            Assert.Contains("0x03", entity.StorageChanges);
            Assert.Contains("0x1bc16d674ec80000", entity.BalanceChanges);
            Assert.Contains("0x1", entity.NonceChanges);
            Assert.Contains("0x6001600101", entity.CodeChanges);
        }

        [Fact]
        public void RoundTrips_Entity_Back_To_AccountAccess()
        {
            var source = CreateAccountAccess();
            var entity = source.MapToStorageEntityForUpsert(100, "0xabc");

            var rehydrated = entity.ToAccountAccess();

            Assert.Equal(source.Address, rehydrated.Address);
            Assert.Equal(source.StorageReads, rehydrated.StorageReads);
            Assert.Equal(source.StorageChanges[0].Key, rehydrated.StorageChanges[0].Key);
            Assert.Equal(source.StorageChanges[0].Changes[0].Value, rehydrated.StorageChanges[0].Changes[0].Value);
            Assert.Equal(source.BalanceChanges[0].Value, rehydrated.BalanceChanges[0].Value);
            Assert.Equal(source.NonceChanges[0].Value, rehydrated.NonceChanges[0].Value);
            Assert.Equal(source.CodeChanges[0].Code, rehydrated.CodeChanges[0].Code);
        }
    }
}
