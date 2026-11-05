using System;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Exceptions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public partial class HaltReasonParityTests
    {
        private static Program Execute(EVMSimulator simulator, Program program)
            => simulator.ExecuteWithCallStackAsync(program, traceEnabled: false).GetAwaiter().GetResult();

        [Fact]
        public void Given_the_async_engine_When_a_frame_halts_Then_the_only_exception_type_the_loop_catches_is_EvmHaltException()
        {
            var program = new Program(new byte[0]) { GasRemaining = 1 };
            var thrown = Assert.Throws<OutOfGasException>(() => program.UpdateGasUsed(5));
            Assert.IsAssignableFrom<EvmHaltException>(thrown);
            Assert.Equal(EvmHaltReason.OutOfGas, ((EvmHaltException)thrown).Reason);
        }

        [Fact]
        public void Given_a_stack_underflow_on_the_async_engine_When_popped_directly_Then_an_EvmHaltException_with_StackUnderflow_is_thrown()
        {
            var program = new Program(new byte[0]);
            var thrown = Assert.Throws<EvmHaltException>(() => program.StackPop());
            Assert.Equal(EvmHaltReason.StackUnderflow, thrown.Reason);
        }

        private sealed class ThrowingStateReader : IStateReader
        {
            private readonly Exception _exception;
            public ThrowingStateReader(Exception exception) => _exception = exception;

            public Task<EvmUInt256> GetBalanceAsync(byte[] address) => Task.FromResult(EvmUInt256.Zero);
            public Task<EvmUInt256> GetBalanceAsync(string address) => Task.FromResult(EvmUInt256.Zero);
            public Task<byte[]> GetCodeAsync(byte[] address) => Task.FromResult(new byte[0]);
            public Task<byte[]> GetCodeAsync(string address) => Task.FromResult(new byte[0]);
            public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => throw _exception;
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position) => throw _exception;
            public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => Task.FromResult(EvmUInt256.Zero);
            public Task<EvmUInt256> GetTransactionCountAsync(string address) => Task.FromResult(EvmUInt256.Zero);
            public Task<bool> AccountExistsAsync(string address) => Task.FromResult(true);
            public Task<byte[]> GetBlockHashAsync(long blockNumber) => Task.FromResult(new byte[32]);
        }
    }
}
