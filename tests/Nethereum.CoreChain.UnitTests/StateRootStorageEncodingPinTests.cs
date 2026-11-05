using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class StateRootStorageEncodingPinTests
    {
        private const string Address = "0x1000000000000000000000000000000000000000";

        private static ExecutionStateService StateWith(byte[] value)
        {
            var state = new ExecutionStateService(
                new InMemoryStateReader(new Dictionary<string, AccountState>()));
            var account = new AccountExecutionState { Address = Address };
            account.Nonce = (EvmUInt256)1;
            account.Storage[EvmUInt256.Zero] = value;
            state.AccountsState[EvmAddress.FromHex(Address)] = account;
            return state;
        }

        private static byte[] PatriciaRoot(ExecutionStateService state)
            => new PatriciaStateRootCalculator(RlpBlockEncodingProvider.Instance).ComputeStateRoot(state);

        [Fact]
        public void Given_ValueWithLeadingZeros_When_EncodingStorage_Then_RootEqualsTrimmedEncoding()
        {
            var padded = PatriciaRoot(StateWith(new byte[] { 0x00, 0x00, 0x05 }));
            var trimmed = PatriciaRoot(StateWith(new byte[] { 0x05 }));
            var wide = PatriciaRoot(StateWith(new byte[32].SetLast(0x05)));

            Assert.Equal(trimmed, padded);
            Assert.Equal(trimmed, wide);
            Assert.NotEqual(trimmed, PatriciaRoot(StateWith(new byte[] { 0x06 })));
        }

        [Fact]
        public void Given_ValueWithNoLeadingZeros_When_EncodingStorage_Then_InputArrayIsNotMutated()
        {
            var value = new byte[] { 0x07, 0x00, 0x09 };
            var copy = (byte[])value.Clone();

            PatriciaRoot(StateWith(value));
            new BinaryStateRootCalculator().ComputeStateRoot(StateWith(value));

            Assert.Equal(copy, value);
        }
    }

    internal static class ByteArrayTestExtensions
    {
        public static byte[] SetLast(this byte[] bytes, byte value)
        {
            bytes[bytes.Length - 1] = value;
            return bytes;
        }
    }
}
