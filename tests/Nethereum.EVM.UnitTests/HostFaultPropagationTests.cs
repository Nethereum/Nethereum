using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class HostFaultPropagationTests
    {
        private const string Sender = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Contract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string Zero = "0x0000000000000000000000000000000000000000";

        [Fact]
        public async Task StrictWitnessMiss_DuringExecution_Propagates_NotLaunderedIntoRevert()
        {
            var bytecode = new byte[] { 0x60, 0x00, 0x54, 0x00 };

            var accounts = new Dictionary<string, AccountState>
            {
                [Sender] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000_000_000), Nonce = 0 },
                [Contract] = new AccountState { Code = bytecode },
                [Zero] = new AccountState(),
            };
            var reader = new InMemoryStateReader(accounts) { Strict = true };
            var executionState = new ExecutionStateService(reader);

            var ctx = new TransactionExecutionContext
            {
                Sender = Sender,
                To = Contract,
                Data = Array.Empty<byte>(),
                GasLimit = 1_000_000,
                Value = 0,
                GasPrice = 0,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = Zero,
                ExecutionState = executionState,
            };

            var executor = new TransactionExecutor(HardforkConfig.Prague);

            await Assert.ThrowsAsync<MissingWitnessDataException>(() => executor.ExecuteAsync(ctx));
        }
    }
}
