using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class Eip7610CreateCollisionTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string FactoryAddress = "0x3333333333333333333333333333333333333333";
        private const string ZeroSalt = "0000000000000000000000000000000000000000000000000000000000000000";

        private static readonly EvmUInt256 OccupiedSlot = new EvmUInt256(1);
        private static readonly byte[] PreExistingValue = new EvmUInt256(0x42).ToBigEndian();

        private static byte[] DeployEmptyCode() => new byte[]
        {
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.RETURN
        };

        private static byte[] Create2ThenKeepGoing() => new byte[]
        {
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.PUSH1, 0x00,
            (byte)Instruction.CREATE2,
            (byte)Instruction.PUSH1, 0x07,
            (byte)Instruction.SSTORE,
            (byte)Instruction.PUSH1, 0x01,
            (byte)Instruction.PUSH1, 0x08,
            (byte)Instruction.SSTORE,
            (byte)Instruction.STOP
        };

        private static string Create2TargetOf(string factory) =>
            ContractUtils.CalculateCreate2Address(factory, ZeroSalt, "").ToLower();

        private static string CreationTargetOfSender() =>
            ContractUtils.CalculateContractAddress(SenderAddress, 0).ToLower();

        private static AccountState FundedSender() =>
            new AccountState { Balance = new EvmUInt256(BigInteger.Parse("1000000000000000000000")) };

        private static AccountState WithStorage(EvmUInt256 slot, byte[] value)
        {
            var account = new AccountState();
            account.Storage[slot] = value;
            return account;
        }

        private static KeyValuePair<string, AccountState> Account(string address, AccountState state) =>
            new KeyValuePair<string, AccountState>(address, state);

        private static InMemoryStateReader ChainState(params KeyValuePair<string, AccountState>[] accounts)
        {
            var map = new Dictionary<string, AccountState>();
            foreach (var account in accounts) map[account.Key.ToLower()] = account.Value;
            return new InMemoryStateReader(map);
        }

        private static TransactionExecutionContext CreationCtx(
            ExecutionStateService executionState, byte[] initCode, long gasLimit = 2_000_000) =>
            new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = initCode,
                IsContractCreation = true,
                GasLimit = gasLimit,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

        private const long GasLeavingRoomAfterTheWithheldGrant = 10_000_000;

        private static TransactionExecutionContext CallCtx(
            ExecutionStateService executionState, string to,
            long gasLimit = GasLeavingRoomAfterTheWithheldGrant) =>
            new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = to,
                Data = new byte[0],
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

        private static EvmUInt256 StoredAt(ExecutionStateService executionState, string address, ulong slot)
        {
            var account = executionState.CreateOrGetAccountExecutionState(address);
            return account.Storage.TryGetValue(new EvmUInt256(slot), out var value)
                ? EvmUInt256.FromBigEndian(value)
                : EvmUInt256.Zero;
        }

        [Fact]
        public async Task Given_TargetWithPreExistingStorage_When_Created_Then_CollisionNotStorageWipe()
        {
            var target = CreationTargetOfSender();
            var chainState = ChainState(
                Account(SenderAddress, FundedSender()),
                Account(target, WithStorage(OccupiedSlot, PreExistingValue)));
            var executionState = new ExecutionStateService(chainState);
            var ctx = CreationCtx(executionState, DeployEmptyCode());

            var result = await new TransactionExecutor(HardforkConfig.Amsterdam).ExecuteAsync(ctx);

            Assert.False(result.Success);
            Assert.Equal("ADDRESS_COLLISION", result.Error);
            Assert.True(result.GasUsed >= ctx.GasLimit,
                "the whole gas grant must be forfeited, billed " + result.GasUsed + " of " + ctx.GasLimit);
            Assert.Equal(PreExistingValue.ToHex(), (await chainState.GetStorageAtAsync(target, OccupiedSlot)).ToHex());
            Assert.Empty(await chainState.GetCodeAsync(target));
        }

        [Fact]
        public async Task Given_TargetWithNoStorage_When_Created_Then_TheContractIsDeployed()
        {
            var target = CreationTargetOfSender();
            var chainState = ChainState(
                Account(SenderAddress, FundedSender()),
                Account(target, new AccountState()));
            var executionState = new ExecutionStateService(chainState);
            var ctx = CreationCtx(executionState, DeployEmptyCode());

            var result = await new TransactionExecutor(HardforkConfig.Amsterdam).ExecuteAsync(ctx);

            Assert.True(result.Success, result.Error);
            Assert.True(result.GasUsed < ctx.GasLimit);
        }

        [Fact]
        public async Task Given_TargetWhoseOnlyStoredValueIsZero_When_Created_Then_TheContractIsDeployed()
        {
            var target = CreationTargetOfSender();
            var chainState = ChainState(
                Account(SenderAddress, FundedSender()),
                Account(target, WithStorage(OccupiedSlot, new byte[32])));
            var executionState = new ExecutionStateService(chainState);
            var ctx = CreationCtx(executionState, DeployEmptyCode());

            var result = await new TransactionExecutor(HardforkConfig.Amsterdam).ExecuteAsync(ctx);

            Assert.True(result.Success, result.Error);
        }

        [Fact]
        public async Task Given_ACancunCreation_When_TargetHasPreExistingStorage_Then_TheSameCollisionHalts()
        {
            var target = CreationTargetOfSender();
            var chainState = ChainState(
                Account(SenderAddress, FundedSender()),
                Account(target, WithStorage(OccupiedSlot, PreExistingValue)));
            var executionState = new ExecutionStateService(chainState);
            var ctx = CreationCtx(executionState, DeployEmptyCode());

            var result = await new TransactionExecutor(HardforkConfig.Cancun).ExecuteAsync(ctx);

            Assert.False(result.Success);
            Assert.Equal("ADDRESS_COLLISION", result.Error);
            Assert.True(result.GasUsed >= ctx.GasLimit);
        }

        [Fact]
        public async Task Given_AmsterdamFork_When_CreateCollidesWithStorage_Then_NoSilentStorageDestroy()
        {
            var target = Create2TargetOf(FactoryAddress);
            var chainState = ChainState(
                Account(SenderAddress, FundedSender()),
                Account(FactoryAddress, new AccountState { Code = Create2ThenKeepGoing() }),
                Account(target, WithStorage(OccupiedSlot, PreExistingValue)));
            var executionState = new ExecutionStateService(chainState);
            var ctx = CallCtx(executionState, FactoryAddress);

            var result = await new TransactionExecutor(HardforkConfig.Amsterdam).ExecuteAsync(ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(EvmUInt256.Zero, StoredAt(executionState, FactoryAddress, 7));
            Assert.Equal(new EvmUInt256(1), StoredAt(executionState, FactoryAddress, 8));
            Assert.Equal(PreExistingValue.ToHex(), (await chainState.GetStorageAtAsync(target, OccupiedSlot)).ToHex());
            Assert.Empty(await chainState.GetCodeAsync(target));
        }

        [Fact]
        public async Task Given_AmsterdamFork_When_TheCreate2TargetIsUntouched_Then_TheChildIsDeployed()
        {
            var chainState = ChainState(
                Account(SenderAddress, FundedSender()),
                Account(FactoryAddress, new AccountState { Code = Create2ThenKeepGoing() }));
            var executionState = new ExecutionStateService(chainState);
            var ctx = CallCtx(executionState, FactoryAddress);

            var result = await new TransactionExecutor(HardforkConfig.Amsterdam).ExecuteAsync(ctx);

            Assert.True(result.Success, result.Error);
            Assert.NotEqual(EvmUInt256.Zero, StoredAt(executionState, FactoryAddress, 7));
            Assert.Equal(new EvmUInt256(1), StoredAt(executionState, FactoryAddress, 8));
        }

        [Fact]
        public async Task Given_ACancunCreate2_When_TargetHasPreExistingStorage_Then_TheSameZeroIsPushed()
        {
            var target = Create2TargetOf(FactoryAddress);
            var chainState = ChainState(
                Account(SenderAddress, FundedSender()),
                Account(FactoryAddress, new AccountState { Code = Create2ThenKeepGoing() }),
                Account(target, WithStorage(OccupiedSlot, PreExistingValue)));
            var executionState = new ExecutionStateService(chainState);
            var ctx = CallCtx(executionState, FactoryAddress);

            var result = await new TransactionExecutor(HardforkConfig.Cancun).ExecuteAsync(ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(EvmUInt256.Zero, StoredAt(executionState, FactoryAddress, 7));
            Assert.Equal(new EvmUInt256(1), StoredAt(executionState, FactoryAddress, 8));
        }
    }
}
