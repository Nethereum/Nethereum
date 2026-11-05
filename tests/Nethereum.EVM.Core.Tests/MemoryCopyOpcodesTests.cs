using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class MemoryCopyOpcodesTests
    {
        private const string Sender = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Contract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private static readonly byte[] Pattern = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

        private static byte[] Push32(byte[] word) => new byte[] { 0x7f }.Concat(word).ToArray();

        private static byte[] Cat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        private static byte[] ReturnWord => new byte[] { 0x60, 0x20, 0x60, 0x00, 0xf3 };

        private static byte[] Run(byte[] code, byte[] calldata = null)
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [Sender] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [Contract] = new AccountState { Code = code }
            };
            var context = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = Sender,
                To = Contract,
                Data = calldata ?? new byte[0],
                GasLimit = 1_000_000,
                ChainId = 1,
                BlockNumber = 1,
                Timestamp = 1000,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BlockGasLimit = 30_000_000,
                ExecutionState = new ExecutionStateService(new InMemoryStateReader(accounts))
            };
            var result = new TransactionExecutor(DefaultHardforkConfigs.Osaka).Execute(context);
            Assert.True(result.Success, result.Error);
            return result.ReturnData;
        }

        private static byte[] DirtyMemory => Cat(Push32(Enumerable.Repeat((byte)0xff, 32).ToArray()), new byte[] { 0x60, 0x00, 0x52 });

        [Fact]
        public void Given_a_code_range_crossing_the_end_of_the_code_When_CODECOPY_Then_the_tail_is_zero_over_dirty_memory()
        {
            var copy = new byte[] { 0x60, 0x20, 0x60, 0x08, 0x60, 0x00, 0x39 };
            var code = Cat(DirtyMemory, copy, ReturnWord);
            var expected = new byte[32];
            System.Array.Copy(code, 8, expected, 0, code.Length - 8 > 32 ? 32 : code.Length - 8);

            Assert.Equal(expected, Run(code));
        }

        [Fact]
        public void Given_a_code_offset_beyond_the_code_When_CODECOPY_Then_the_destination_is_zeroed_over_dirty_memory()
        {
            var copy = new byte[] { 0x60, 0x20, 0x61, 0x10, 0x00, 0x60, 0x00, 0x39 };
            var code = Cat(DirtyMemory, copy, ReturnWord);

            Assert.Equal(new byte[32], Run(code));
        }

        [Fact]
        public void Given_calldata_shorter_than_the_range_When_CALLDATACOPY_Then_the_tail_is_zero_over_dirty_memory()
        {
            var copy = new byte[] { 0x60, 0x20, 0x60, 0x00, 0x60, 0x00, 0x37 };
            var code = Cat(DirtyMemory, copy, ReturnWord);
            var calldata = new byte[] { 0xde, 0xad, 0xbe, 0xef };
            var expected = new byte[32];
            System.Array.Copy(calldata, expected, 4);

            Assert.Equal(expected, Run(code, calldata));
        }

        [Fact]
        public void Given_a_destination_after_an_overlapping_source_When_MCOPY_Then_the_source_bytes_are_copied_as_they_were()
        {
            var store = Cat(Push32(Pattern), new byte[] { 0x60, 0x00, 0x52 });
            var mcopy = new byte[] { 0x60, 0x1c, 0x60, 0x00, 0x60, 0x04, 0x5e };
            var expected = Pattern.Take(4).Concat(Pattern.Take(28)).ToArray();

            Assert.Equal(expected, Run(Cat(store, mcopy, ReturnWord)));
        }

        [Fact]
        public void Given_a_destination_before_an_overlapping_source_When_MCOPY_Then_the_source_bytes_are_copied_as_they_were()
        {
            var store = Cat(Push32(Pattern), new byte[] { 0x60, 0x00, 0x52 });
            var mcopy = new byte[] { 0x60, 0x1c, 0x60, 0x04, 0x60, 0x00, 0x5e };
            var expected = Pattern.Skip(4).Concat(Pattern.Skip(28)).ToArray();

            Assert.Equal(expected, Run(Cat(store, mcopy, ReturnWord)));
        }
    }
}
