using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class CodelessAccountCodeReadTests
    {
        private const string CodelessAddress = "0x0000000000000000000000000000000000000004";

        private sealed class StateReadCountingReader : IStateReader
        {
            private readonly MockNodeDataService _inner = new MockNodeDataService();

            public int CodeReads { get; private set; }

            public int Reads { get; private set; }

            public Task<byte[]> GetCodeAsync(byte[] address)
            {
                CodeReads++;
                Reads++;
                return _inner.GetCodeAsync(address);
            }

            public Task<byte[]> GetCodeAsync(string address)
            {
                CodeReads++;
                Reads++;
                return _inner.GetCodeAsync(address);
            }

            public Task<EvmUInt256> GetBalanceAsync(byte[] address)
            {
                Reads++;
                return _inner.GetBalanceAsync(address);
            }

            public Task<EvmUInt256> GetBalanceAsync(string address)
            {
                Reads++;
                return _inner.GetBalanceAsync(address);
            }

            public Task<byte[]> GetBlockHashAsync(long blockNumber) => _inner.GetBlockHashAsync(blockNumber);
            public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);
            public Task<EvmUInt256> GetTransactionCountAsync(byte[] address)
            {
                Reads++;
                return _inner.GetTransactionCountAsync(address);
            }

            public Task<EvmUInt256> GetTransactionCountAsync(string address)
            {
                Reads++;
                return _inner.GetTransactionCountAsync(address);
            }

            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
        }

        private sealed class AccountReadRecorder : IStateAccessRecorder
        {
            public System.Collections.Generic.List<string> AccountReads { get; } = new System.Collections.Generic.List<string>();

            public void RecordAccountRead(string address) => AccountReads.Add(address);

            public void RecordStorageRead(string address, EvmUInt256 key)
            {
            }
        }

        [Fact]
        public async Task Given_an_account_without_code_When_it_is_loaded_repeatedly_Then_the_state_reader_is_asked_for_its_code_once()
        {
            var reader = new StateReadCountingReader();
            var state = new ExecutionStateService(reader);

            await state.LoadBalanceNonceAndCodeFromStorageAsync(CodelessAddress);
            await state.LoadBalanceNonceAndCodeFromStorageAsync(CodelessAddress);
            await state.GetCodeAsync(CodelessAddress);

            Assert.Equal(1, reader.CodeReads);
        }

        [Fact]
        public async Task Given_an_account_without_code_whose_code_was_read_When_the_transaction_deploys_code_to_it_Then_the_new_code_is_returned()
        {
            var reader = new StateReadCountingReader();
            var state = new ExecutionStateService(reader);
            await state.GetCodeAsync(CodelessAddress);
            var deployed = new byte[] { 0x60, 0x00 };

            state.CreateOrGetAccountExecutionState(CodelessAddress).Code = deployed;

            Assert.Same(deployed, await state.GetCodeAsync(CodelessAddress));
        }

        [Fact]
        public async Task Given_an_account_whose_balance_nonce_and_code_were_loaded_When_it_is_loaded_again_Then_the_state_reader_is_not_asked_again()
        {
            var reader = new StateReadCountingReader();
            var state = new ExecutionStateService(reader);
            var first = await state.LoadBalanceNonceAndCodeFromStorageAsync(CodelessAddress);
            var readsAfterFirstLoad = reader.Reads;

            var second = await state.LoadBalanceNonceAndCodeFromStorageAsync(CodelessAddress);

            Assert.Same(first, second);
            Assert.Equal(readsAfterFirstLoad, reader.Reads);
        }

        [Fact]
        public async Task Given_an_access_recorder_When_a_loaded_account_is_loaded_again_Then_the_account_read_is_reported()
        {
            var reader = new StateReadCountingReader();
            var state = new ExecutionStateService(reader);
            await state.LoadBalanceNonceAndCodeFromStorageAsync(CodelessAddress);
            var recorder = new AccountReadRecorder();
            state.AccessRecorder = recorder;

            await state.LoadBalanceNonceAndCodeFromStorageAsync(CodelessAddress);

            Assert.Contains(CodelessAddress, recorder.AccountReads);
        }
    }
}
