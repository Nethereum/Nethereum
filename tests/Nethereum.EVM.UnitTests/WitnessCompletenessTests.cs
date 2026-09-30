using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Witness;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class WitnessCompletenessTests
    {
        [Fact]
        public async Task Strict_ThrowsOnMissingAccount()
        {
            var reader = new InMemoryStateReader(new Dictionary<string, AccountState>())
            {
                Strict = true
            };

            await Assert.ThrowsAsync<MissingWitnessDataException>(
                () => reader.GetBalanceAsync("0x1111111111111111111111111111111111111111"));
        }

        [Fact]
        public async Task Strict_ThrowsOnMissingStorageSlot()
        {
            var addr = "0x1111111111111111111111111111111111111111";
            var accounts = new Dictionary<string, AccountState>
            {
                { addr, new AccountState { Balance = EvmUInt256.Zero } }
            };
            var reader = new InMemoryStateReader(accounts) { Strict = true };

            await Assert.ThrowsAsync<MissingWitnessDataException>(
                () => reader.GetStorageAtAsync(addr, (EvmUInt256)7));
        }

        [Fact]
        public async Task NonStrict_ReturnsDefaultsForMissingData()
        {
            var reader = new InMemoryStateReader(new Dictionary<string, AccountState>());

            Assert.Equal(EvmUInt256.Zero, await reader.GetBalanceAsync("0xabc"));
            Assert.Empty(await reader.GetCodeAsync("0xabc"));
        }
    }
}
