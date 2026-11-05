using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class StateRootEmptyAccountRuleTests
    {
        private const string Address = "0x1000000000000000000000000000000000000000";

        private static ExecutionStateService StateWith(Dictionary<EvmUInt256, byte[]> storage)
        {
            var state = new ExecutionStateService(
                new InMemoryStateReader(new Dictionary<string, AccountState>()));
            if (storage == null) return state;

            var account = new AccountExecutionState { Address = Address };
            foreach (var kvp in storage) account.Storage[kvp.Key] = kvp.Value;
            state.AccountsState[EvmAddress.FromHex(Address)] = account;
            return state;
        }

        private static byte[] BinaryRoot(ExecutionStateService state)
            => new BinaryStateRootCalculator().ComputeStateRoot(state);

        private static byte[] PatriciaRoot(ExecutionStateService state)
            => new PatriciaStateRootCalculator(RlpBlockEncodingProvider.Instance).ComputeStateRoot(state);

        [Fact]
        public void Given_BinaryAndPatriciaStateRootCalculators_When_AnAccountIsEmptyOrHoldsOnlyZeroStorage_Then_BothSkipItAsBefore()
        {
            var noAccounts = StateWith(null);
            var emptyAccount = StateWith(new Dictionary<EvmUInt256, byte[]>());
            var zeroStorageOnly = StateWith(new Dictionary<EvmUInt256, byte[]>
            {
                { EvmUInt256.Zero, new byte[] { 0 } },
                { new EvmUInt256(1UL), new byte[0] },
                { new EvmUInt256(2UL), new byte[] { 0, 0, 0 } },
                { new EvmUInt256(3UL), null }
            });

            Assert.Equal(BinaryRoot(noAccounts), BinaryRoot(emptyAccount));
            Assert.Equal(BinaryRoot(noAccounts), BinaryRoot(zeroStorageOnly));
            Assert.Equal(PatriciaRoot(noAccounts), PatriciaRoot(emptyAccount));
            Assert.Equal(PatriciaRoot(noAccounts), PatriciaRoot(zeroStorageOnly));
        }

        [Fact]
        public void Given_AnAccountWithNonZeroStorageOnly_When_ComputingTheRoot_Then_TheAccountIsIncludedInBoth()
        {
            var noAccounts = StateWith(null);
            var nonZeroStorageOnly = StateWith(new Dictionary<EvmUInt256, byte[]>
            {
                { EvmUInt256.Zero, new byte[] { 0x42 } }
            });

            Assert.NotEqual(BinaryRoot(noAccounts), BinaryRoot(nonZeroStorageOnly));
            Assert.NotEqual(PatriciaRoot(noAccounts), PatriciaRoot(nonZeroStorageOnly));
        }
    }
}
