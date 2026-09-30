using System;
using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class SyncHostFaultPropagationTests
    {
        private const string Sender = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Contract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string Zero = "0x0000000000000000000000000000000000000000";

        private static TransactionExecutionContext ContextFor(byte[] bytecode, bool strict)
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [Sender] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000_000_000), Nonce = 0 },
                [Contract] = new AccountState { Code = bytecode },
                [Zero] = new AccountState(),
            };

            var reader = new InMemoryStateReader(accounts) { Strict = strict };

            return new TransactionExecutionContext
            {
                Sender = Sender,
                To = Contract,
                Data = Array.Empty<byte>(),
                GasLimit = 1_000_000,
                Value = EvmUInt256.Zero,
                GasPrice = 0,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = Zero,
                ExecutionState = new ExecutionStateService(reader),
            };
        }

        [Fact]
        public void Given_AStrictWitnessMiss_When_TheSyncSimulatorExecutes_Then_ItPropagatesRatherThanReverting()
        {
            var ctx = ContextFor(new byte[] { 0x60, 0x00, 0x54, 0x00 }, strict: true);

            var executor = new TransactionExecutor(HardforkConfig.Prague);

            Assert.Throws<MissingWitnessDataException>(() => executor.Execute(ctx));
        }

        [Fact]
        public void Given_AnOrdinaryEvmHalt_When_TheSyncSimulatorExecutes_Then_ItRevertsRatherThanPropagating()
        {
            var ctx = ContextFor(new byte[] { 0x0c }, strict: false);

            var executor = new TransactionExecutor(HardforkConfig.Prague);

            var result = executor.Execute(ctx);

            Assert.False(result.Success);
        }
    }
}
