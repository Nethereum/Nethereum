using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.Witness;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class HistoryContractSeedingTests
    {
        private const string HistoryContract = "0x0000F90827F1C53a10cb7A02335B175320002935";

        private static List<WitnessAccount> Accounts() => new List<WitnessAccount>();

        private static Dictionary<long, byte[]> Hashes(params long[] numbers)
        {
            var map = new Dictionary<long, byte[]>();
            foreach (var n in numbers)
            {
                var hash = new byte[32];
                hash[31] = (byte)(n + 1);
                map[n] = hash;
            }
            return map;
        }

        private static List<WitnessStorageSlot> SeededSlots(List<WitnessAccount> accounts)
        {
            var hist = accounts.FirstOrDefault(a =>
                string.Equals(a.Address, HistoryContract, System.StringComparison.OrdinalIgnoreCase));
            return hist?.Storage ?? new List<WitnessStorageSlot>();
        }

        [Fact]
        [Trait("Category", "EIP2935")]
        public void Given_HistoryUpToTheParent_When_Seeded_Then_TheParentsSlotIsLeftForTheBlocksOwnCall()
        {
            var accounts = Accounts();

            HistoryContractHelpers.PopulateFromBlockHashes(accounts, Hashes(0, 1, 2, 3, 4), blockNumber: 5);

            var slots = SeededSlots(accounts);
            Assert.DoesNotContain(slots, s => s.Key.Equals(new EvmUInt256(4)));
            Assert.Contains(slots, s => s.Key.Equals(new EvmUInt256(3)));
            Assert.Equal(4, slots.Count);
        }

        [Fact]
        [Trait("Category", "EIP2935")]
        public void Given_OnlyGenesis_When_SeedingForBlockOne_Then_NothingIsSeeded()
        {
            var accounts = Accounts();

            HistoryContractHelpers.PopulateFromBlockHashes(accounts, Hashes(0), blockNumber: 1);

            Assert.Empty(SeededSlots(accounts));
        }

        [Fact]
        [Trait("Category", "EIP2935")]
        public void Given_HistoryWellBeforeTheParent_When_Seeded_Then_ItIsAllPresent()
        {
            var accounts = Accounts();

            HistoryContractHelpers.PopulateFromBlockHashes(accounts, Hashes(0, 1, 2), blockNumber: 9);

            var slots = SeededSlots(accounts);
            Assert.Equal(3, slots.Count);
            foreach (var n in new ulong[] { 0, 1, 2 })
                Assert.Contains(slots, s => s.Key.Equals(new EvmUInt256(n)));
        }
    }
}
