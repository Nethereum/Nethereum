using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip7843SlotNumTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const long G2 = 2;

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            HardforkConfig config, TransactionExecutionContext ctx)
        {
            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);
            return (ctx, result);
        }

        private static async Task<EIP7702TestNodeDataService> FundedNodeAsync(string address, BigInteger balance)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(address, balance);
            return node;
        }

        private static TransactionExecutionContext BaseCtx(ExecutionStateService executionState, BigInteger slotNumber) => new TransactionExecutionContext
        {
            Sender = SenderAddress,
            Data = null,
            IsContractCreation = false,
            GasLimit = 500_000,
            Value = 0,
            GasPrice = 1,
            Nonce = 0,
            BlockNumber = 1,
            Timestamp = 1704067200,
            BaseFee = 0,
            ChainId = 1,
            Coinbase = SenderAddress,
            ExecutionState = executionState,
            SlotNumber = EvmUInt256BigIntegerExtensions.FromBigInteger(slotNumber)
        };


        private static readonly byte[] SlotnumReturnRuntimeCode = "4B60005260206000F3".HexToByteArray();

        private const string SlotnumContractAddress = "0x2222222222222222222222222222222222222222";

        [Fact]
        public async Task Given_SLOTNUM_AtAmsterdam_When_Executed_Then_PushesSlotNumber_AndCosts2Gas()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(SlotnumContractAddress, SlotnumReturnRuntimeCode);
            var executionState = new ExecutionStateService(node);
            var ctx = BaseCtx(executionState, slotNumber: 424242);
            ctx.To = SlotnumContractAddress;

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            var pushed = new BigInteger(result.ReturnData, isUnsigned: true, isBigEndian: true);
            Assert.Equal((BigInteger)424242, pushed);

            const long nonSlotnumCost = 3 + 3 + 3 + 3 + 3;
            Assert.Equal(G2 + nonSlotnumCost, result.GasUsed - ctx.IntrinsicExecutionGas);
        }

        [Fact]
        public async Task Given_SLOTNUM_AtOsaka_Then_UnregisteredOpcode_TransactionReverts_NoCrossForkLeak()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(SlotnumContractAddress, SlotnumReturnRuntimeCode);
            var executionState = new ExecutionStateService(node);
            var ctx = BaseCtx(executionState, slotNumber: 424242);
            ctx.To = SlotnumContractAddress;

            var (_, result) = await RunAsync(config, ctx);

            Assert.False(result.Success, "0x4B must not be treated as a valid opcode pre-Amsterdam");
        }


        private const string StackOverflowContractAddress = "0x3333333333333333333333333333333333333333";

        private static byte[] BuildFillStackThenSlotnum()
        {
            var code = new List<byte>();
            for (int i = 0; i < 1024; i++)
            {
                code.Add(0x60);
                code.Add(0x00);
            }
            code.Add(0x4B);
            code.Add(0x00);
            return code.ToArray();
        }

        [Fact]
        public async Task Given_StackAt1024_AtAmsterdam_When_Slotnum_Then_StackOverflow()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(StackOverflowContractAddress, BuildFillStackThenSlotnum());
            var executionState = new ExecutionStateService(node);
            var ctx = BaseCtx(executionState, slotNumber: 1);
            ctx.To = StackOverflowContractAddress;
            ctx.GasLimit = 30_000_000;

            var (_, result) = await RunAsync(config, ctx);

            Assert.False(result.Success, "SLOTNUM onto a full stack must halt with overflow, not succeed");
        }


        private static byte[] BuildSlotnumSstoreStop(byte key)
        {
            return new byte[] { 0x4B, 0x60, key, 0x55, 0x00 };
        }

        private const string CallTargetAddress = "0x4444444444444444444444444444444444444444";
        private const string DelegatecallLogicAddress = "0x5555555555555555555555555555555555555555";
        private const string DelegatecallProxyAddress = "0x6666666666666666666666666666666666666666";
        private const string NestedCreateFactoryAddress = "0x7777777777777777777777777777777777777777";

        private static byte[] BuildDelegateCallProxyRuntimeCode(string logicAddress)
        {
            var addressBytes = logicAddress.Substring(2).HexToByteArray();
            var code = new List<byte>
            {
                0x60, 0x00,
                0x60, 0x00,
                0x60, 0x00,
                0x60, 0x00,
                0x73
            };
            code.AddRange(addressBytes);
            code.Add(0x5A);
            code.Add(0xF4);
            code.Add(0x50);
            code.Add(0x00);
            return code.ToArray();
        }

        private static byte[] BuildFactoryRuntimeCodeCreatingSlotnumChild()
        {
            var initCode = BuildSlotnumSstoreStop(0x00);
            var code = new List<byte>
            {
                0x64
            };
            code.AddRange(initCode);
            code.Add(0x60); code.Add(0x00);
            code.Add(0x52);
            code.Add(0x60); code.Add((byte)initCode.Length);
            code.Add(0x60); code.Add((byte)(32 - initCode.Length));
            code.Add(0x60); code.Add(0x00);
            code.Add(0xF0);
            code.Add(0x50);
            code.Add(0x00);
            return code.ToArray();
        }

        [Fact]
        public async Task Given_Slotnum_AtAmsterdam_When_InAnyFrameKind_Then_SameBlockSlotNumber()
        {
            const long injectedSlotNumber = 777_888_999;

            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000000"));
            await node.SetCodeAsync(CallTargetAddress, BuildSlotnumSstoreStop(0x00));
            await node.SetCodeAsync(DelegatecallLogicAddress, BuildSlotnumSstoreStop(0x01));
            await node.SetCodeAsync(DelegatecallProxyAddress, BuildDelegateCallProxyRuntimeCode(DelegatecallLogicAddress));
            await node.SetCodeAsync(NestedCreateFactoryAddress, BuildFactoryRuntimeCodeCreatingSlotnumChild());
            var executionState = new ExecutionStateService(node);
            var executor = new TransactionExecutor(config);

            var callCtx = BaseCtx(executionState, injectedSlotNumber);
            callCtx.To = CallTargetAddress;
            callCtx.Nonce = 0;
            var callResult = await executor.ExecuteAsync(callCtx);
            Assert.True(callResult.Success, callResult.Error);

            var delegateCtx = BaseCtx(executionState, injectedSlotNumber);
            delegateCtx.To = DelegatecallProxyAddress;
            delegateCtx.Nonce = 1;
            var delegateResult = await executor.ExecuteAsync(delegateCtx);
            Assert.True(delegateResult.Success, delegateResult.Error);

            var nestedCreateCtx = BaseCtx(executionState, injectedSlotNumber);
            nestedCreateCtx.To = NestedCreateFactoryAddress;
            nestedCreateCtx.Nonce = 2;
            var nestedCreateResult = await executor.ExecuteAsync(nestedCreateCtx);
            Assert.True(nestedCreateResult.Success, nestedCreateResult.Error);
            var nestedCreateChildAddress = ContractUtils.CalculateContractAddress(NestedCreateFactoryAddress, 0);

            var initcodeTxCtx = BaseCtx(executionState, injectedSlotNumber);
            initcodeTxCtx.To = "";
            initcodeTxCtx.IsContractCreation = true;
            initcodeTxCtx.Data = BuildSlotnumSstoreStop(0x00);
            initcodeTxCtx.Nonce = 3;
            var initcodeTxResult = await executor.ExecuteAsync(initcodeTxCtx);
            Assert.True(initcodeTxResult.Success, initcodeTxResult.Error);
            var initcodeTxContractAddress = initcodeTxCtx.ContractAddress;

            var expected = EvmUInt256BigIntegerExtensions.FromBigInteger(injectedSlotNumber);

            var callObserved = await executionState.GetFromStorageAsync(CallTargetAddress, EvmUInt256.Zero);
            var delegateObserved = await executionState.GetFromStorageAsync(DelegatecallProxyAddress, EvmUInt256.One);
            var nestedCreateObserved = await executionState.GetFromStorageAsync(nestedCreateChildAddress, EvmUInt256.Zero);
            var initcodeTxObserved = await executionState.GetFromStorageAsync(initcodeTxContractAddress, EvmUInt256.Zero);

            Assert.Equal(expected, EvmUInt256.FromBigEndian(callObserved));
            Assert.Equal(expected, EvmUInt256.FromBigEndian(delegateObserved));
            Assert.Equal(expected, EvmUInt256.FromBigEndian(nestedCreateObserved));
            Assert.Equal(expected, EvmUInt256.FromBigEndian(initcodeTxObserved));
        }


        [Fact]
        public async Task Given_TwoInjectedSlotNumbers_When_ExecutedAsSeparateBlocks_Then_ExecutorPropagatesEachUnchanged()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(SlotnumContractAddress, SlotnumReturnRuntimeCode);
            var executionState = new ExecutionStateService(node);
            var executor = new TransactionExecutor(config);

            var block1Ctx = BaseCtx(executionState, slotNumber: 100);
            block1Ctx.To = SlotnumContractAddress;
            block1Ctx.BlockNumber = 1;
            block1Ctx.Nonce = 0;
            var block1Result = await executor.ExecuteAsync(block1Ctx);
            Assert.True(block1Result.Success, block1Result.Error);
            var block1Observed = new BigInteger(block1Result.ReturnData, isUnsigned: true, isBigEndian: true);

            var block2Ctx = BaseCtx(executionState, slotNumber: 101);
            block2Ctx.To = SlotnumContractAddress;
            block2Ctx.BlockNumber = 2;
            block2Ctx.Nonce = 1;
            var block2Result = await executor.ExecuteAsync(block2Ctx);
            Assert.True(block2Result.Success, block2Result.Error);
            var block2Observed = new BigInteger(block2Result.ReturnData, isUnsigned: true, isBigEndian: true);

            Assert.Equal((BigInteger)100, block1Observed);
            Assert.Equal((BigInteger)101, block2Observed);
            Assert.NotEqual(block1Observed, block2Observed);
        }

        [Fact]
        public async Task Given_GenesisBlock_AtAmsterdam_When_SlotnumNotInjected_Then_DefinedZeroValue()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(SlotnumContractAddress, SlotnumReturnRuntimeCode);
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = SlotnumContractAddress,
                Data = null,
                IsContractCreation = false,
                GasLimit = 500_000,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 0,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            var observed = new BigInteger(result.ReturnData, isUnsigned: true, isBigEndian: true);
            Assert.Equal(BigInteger.Zero, observed);
        }
    }
}
