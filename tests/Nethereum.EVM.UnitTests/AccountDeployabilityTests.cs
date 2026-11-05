using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Create;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class AccountDeployabilityTests
    {
        private const string Address = "0xAbCdEf0123456789aBcDeF0123456789abCDEF01";

        [Fact]
        public async Task Given_an_untouched_address_When_checked_by_text_or_by_value_Then_both_answers_are_deployable()
        {
            var state = new ExecutionStateService(new MockNodeDataService());

            Assert.True(await AccountDeployability.IsDeployableAsync(state, Address));
            Assert.True(await AccountDeployability.IsDeployableAsync(state, EvmAddress.FromHex(Address)));
        }

        [Fact]
        public async Task Given_an_address_with_a_nonce_When_checked_by_text_or_by_value_Then_neither_is_deployable()
        {
            var state = new ExecutionStateService(new MockNodeDataService());
            state.SetNonce(EvmAddress.FromHex(Address), new EvmUInt256(1));

            Assert.False(await AccountDeployability.IsDeployableAsync(state, Address.ToLowerInvariant()));
            Assert.False(await AccountDeployability.IsDeployableAsync(state, EvmAddress.FromHex(Address)));
        }

        [Fact]
        public async Task Given_an_address_with_code_When_checked_by_value_Then_it_is_not_deployable()
        {
            var state = new ExecutionStateService(new MockNodeDataService());
            state.SaveCode(EvmAddress.FromHex(Address), new byte[] { 0x60, 0x00 });

            Assert.False(await AccountDeployability.IsDeployableAsync(state, EvmAddress.FromHex(Address)));
        }
    }
}
