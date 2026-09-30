using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class SyncExecutionTests
    {
        private const string SenderAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string ContractAddress = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private TransactionExecutionContext CreateCallContext(
            Dictionary<string, AccountState> accounts,
            byte[] data = null)
        {
            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);

            return new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = SenderAddress,
                To = ContractAddress,
                Data = data ?? new byte[0],
                GasLimit = 1_000_000,
                Value = EvmUInt256.Zero,
                GasPrice = 0,
                ChainId = 1,
                BlockNumber = 1,
                Timestamp = 1000,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                ExecutionState = executionState
            };
        }

        [Fact]
        public void Execute_Push1AndReturn_ReturnsExpectedValue()
        {
            var bytecode = new byte[] { 0x60, 0x42, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xF3 };

            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [ContractAddress] = new AccountState { Code = bytecode }
            };

            var ctx = CreateCallContext(accounts);
            var executor = new TransactionExecutor(DefaultHardforkConfigs.Osaka);
            var result = executor.Execute(ctx);

            Assert.True(result.Success, $"Execution failed: {result.Error}");
            Assert.NotNull(result.ReturnData);
            Assert.Equal(32, result.ReturnData.Length);
            Assert.Equal(0x42, result.ReturnData[31]);
        }

        [Fact]
        public void Execute_AddTwoNumbers_ReturnsSum()
        {
            var bytecode = new byte[] { 0x60, 0x03, 0x60, 0x04, 0x01, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xF3 };

            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [ContractAddress] = new AccountState { Code = bytecode }
            };

            var ctx = CreateCallContext(accounts);
            var executor = new TransactionExecutor(DefaultHardforkConfigs.Osaka);
            var result = executor.Execute(ctx);

            Assert.True(result.Success, $"Execution failed: {result.Error}");
            Assert.NotNull(result.ReturnData);
            Assert.Equal(32, result.ReturnData.Length);
            Assert.Equal(7, result.ReturnData[31]);
        }

        [Fact]
        public void Execute_SstoreAndSload_ReturnsStoredValue()
        {
            var bytecode = new byte[]
            {
                0x60, 0xFF, 0x60, 0x00, 0x55,
                0x60, 0x00, 0x54,
                0x60, 0x00, 0x52,
                0x60, 0x20, 0x60, 0x00, 0xF3
            };

            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [ContractAddress] = new AccountState { Code = bytecode }
            };

            var ctx = CreateCallContext(accounts);
            var executor = new TransactionExecutor(DefaultHardforkConfigs.Osaka);
            var result = executor.Execute(ctx);

            Assert.True(result.Success, $"Execution failed: {result.Error}");
            Assert.NotNull(result.ReturnData);
            Assert.Equal(32, result.ReturnData.Length);
            Assert.Equal(0xFF, result.ReturnData[31]);
        }

        [Fact]
        public void Execute_Caller_ReturnsSenderAddress()
        {
            var bytecode = new byte[] { 0x33, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xF3 };

            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [ContractAddress] = new AccountState { Code = bytecode }
            };

            var ctx = CreateCallContext(accounts);
            var executor = new TransactionExecutor(DefaultHardforkConfigs.Osaka);
            var result = executor.Execute(ctx);

            Assert.True(result.Success, $"Execution failed: {result.Error}");
            Assert.NotNull(result.ReturnData);
            Assert.Equal(32, result.ReturnData.Length);
            Assert.Equal(0xaa, result.ReturnData[12]);
            Assert.Equal(0xaa, result.ReturnData[31]);
        }

        [Fact]
        public void Execute_Revert_FailsWithRevert()
        {
            var bytecode = new byte[] { 0x60, 0x00, 0x60, 0x00, 0xFD };

            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [ContractAddress] = new AccountState { Code = bytecode }
            };

            var ctx = CreateCallContext(accounts);
            var executor = new TransactionExecutor(DefaultHardforkConfigs.Osaka);
            var result = executor.Execute(ctx);

            Assert.False(result.Success);
        }

        [Fact]
        public void Execute_EmptyCode_SucceedsWithNoReturnData()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [ContractAddress] = new AccountState { Code = new byte[0] }
            };

            var ctx = CreateCallContext(accounts);
            var executor = new TransactionExecutor(DefaultHardforkConfigs.Osaka);
            var result = executor.Execute(ctx);

            Assert.True(result.Success, $"Execution failed: {result.Error}");
        }

        [Fact]
        public void Execute_CalldataLoad_ReturnsInputData()
        {
            var bytecode = new byte[] { 0x60, 0x00, 0x35, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xF3 };

            var calldata = new byte[32];
            calldata[31] = 0xBE;
            calldata[30] = 0xEF;

            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [ContractAddress] = new AccountState { Code = bytecode }
            };

            var ctx = CreateCallContext(accounts, calldata);
            var executor = new TransactionExecutor(DefaultHardforkConfigs.Osaka);
            var result = executor.Execute(ctx);

            Assert.True(result.Success, $"Execution failed: {result.Error}");
            Assert.NotNull(result.ReturnData);
            Assert.Equal(32, result.ReturnData.Length);
            Assert.Equal(0xBE, result.ReturnData[31]);
            Assert.Equal(0xEF, result.ReturnData[30]);
        }

        [Fact]
        public void ExecutionStateService_LoadsBalanceFromStateReader()
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(5_000_000) }
            };
            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);

            var balance = executionState.GetTotalBalance(SenderAddress);

            Assert.Equal(new EvmUInt256(5_000_000), balance);
        }

        [Fact]
        public void ExecutionStateService_LoadsCodeFromStateReader()
        {
            var code = new byte[] { 0x60, 0x00 };
            var accounts = new Dictionary<string, AccountState>
            {
                [ContractAddress] = new AccountState { Code = code }
            };
            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);

            var loaded = executionState.GetCode(ContractAddress);

            Assert.Equal(code, loaded);
        }

        [Fact]
        public void ExecutionStateService_LoadsStorageFromStateReader()
        {
            var storageValue = new byte[32];
            storageValue[31] = 0xAA;
            var accounts = new Dictionary<string, AccountState>
            {
                [ContractAddress] = new AccountState
                {
                    Storage = new Dictionary<EvmUInt256, byte[]>
                    {
                        [EvmUInt256.Zero] = storageValue
                    }
                }
            };
            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);

            var loaded = executionState.GetFromStorage(ContractAddress, EvmUInt256.Zero);

            Assert.Equal(storageValue, loaded);
        }
    }
}
