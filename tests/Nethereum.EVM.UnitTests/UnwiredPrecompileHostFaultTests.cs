using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Precompiles.Bls;
using Nethereum.Signer.Bls.Herumi;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class UnwiredPrecompileHostFaultTests
    {
        private const string Sender = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Contract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string Zero = "0x0000000000000000000000000000000000000000";

        private const string BlsG1AddPrecompile = "0x000000000000000000000000000000000000000b";

        private static readonly byte[] CallBlsG1AddBytecode = new byte[]
        {
            0x60, 0x80,
            0x61, 0x01, 0xa0,
            0x61, 0x01, 0x00,
            0x60, 0x00,
            0x60, 0x00,
            0x60, 0x0b,
            0x5a,
            0xf1,
            0x61, 0x01, 0x80,
            0x52,
            0x60, 0xa0,
            0x61, 0x01, 0x80,
            0xf3
        };

        private static ExecutionStateService StateWithCallerContract()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [Sender] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000_000_000), Nonce = 0 },
                [Contract] = new AccountState { Code = CallBlsG1AddBytecode },
                [Zero] = new AccountState(),
            };
            return new ExecutionStateService(new InMemoryStateReader(accounts));
        }

        private static ExecutionStateService StateWithSenderOnly()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [Sender] = new AccountState { Balance = new EvmUInt256(1_000_000_000_000_000_000), Nonce = 0 },
                [Zero] = new AccountState(),
            };
            return new ExecutionStateService(new InMemoryStateReader(accounts));
        }

        private static TransactionExecutionContext TxTo(string to, byte[] data, ExecutionStateService state)
            => new TransactionExecutionContext
            {
                Sender = Sender,
                To = to,
                Data = data,
                GasLimit = 5_000_000,
                Value = 0,
                GasPrice = 0,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1000,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = Zero,
                ExecutionState = state,
            };

        private static HardforkConfig PragueWithNoBlsBackend()
            => HardforkConfig.Prague.WithPrecompiles(DefaultPrecompileRegistries.PragueBase());

        private static HardforkConfig PragueWithBlsBackend()
            => HardforkConfig.Prague.WithPrecompiles(
                DefaultPrecompileRegistries.PragueBase().WithBlsBackend(new Bls12381Operations()));

        [Fact]
        public async Task Given_APrecompileWithNoBackend_When_Called_Then_ItFailsAsAHostFaultNotAConsumedCall()
        {
            var executor = new TransactionExecutor(PragueWithNoBlsBackend());

            var fault = await Assert.ThrowsAsync<UnwiredPrecompileException>(
                () => executor.ExecuteAsync(TxTo(Contract, Array.Empty<byte>(), StateWithCallerContract())));

            Assert.True(EvmHostException.IsHostOrSystemFault(fault));
            Assert.Contains("0xb", fault.Message);
            Assert.Contains("WithBlsBackend", fault.Message);
        }

        [Fact]
        public async Task Given_APrecompileWithNoBackend_When_CalledDirectlyByATransaction_Then_ItFailsAsAHostFaultNotAConsumedCall()
        {
            var executor = new TransactionExecutor(PragueWithNoBlsBackend());

            var fault = await Assert.ThrowsAsync<UnwiredPrecompileException>(
                () => executor.ExecuteAsync(TxTo(BlsG1AddPrecompile, new byte[256], StateWithSenderOnly())));

            Assert.True(EvmHostException.IsHostOrSystemFault(fault));
            Assert.Equal(0x0b, fault.AddressNumeric);
        }

        [Fact]
        public async Task Given_APrecompileWithABackendWired_When_Called_Then_ItExecutes()
        {
            var executor = new TransactionExecutor(PragueWithBlsBackend());

            var result = await executor.ExecuteAsync(
                TxTo(Contract, Array.Empty<byte>(), StateWithCallerContract()));

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.ReturnData);
            Assert.Equal(160, result.ReturnData.Length);

            Assert.Equal(1, result.ReturnData[31]);
            for (int i = 32; i < 160; i++)
                Assert.Equal(0, result.ReturnData[i]);
        }
    }
}
