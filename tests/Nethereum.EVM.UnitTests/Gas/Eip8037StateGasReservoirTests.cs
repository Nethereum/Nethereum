using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.UnitTests;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8037StateGasReservoirTests
    {
        [Fact]
        public void GasConstants_ShouldMatchEip8037StateGasSpec()
        {
            Assert.Equal(1530, GasConstants.EIP8037_COST_PER_STATE_BYTE);
            Assert.Equal(183600, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
            Assert.Equal(97920, GasConstants.EIP8037_STORAGE_SET_STATE_GAS);
            Assert.Equal(35190, GasConstants.EIP8037_AUTH_BASE_STATE_GAS);
            Assert.Equal(16_777_216, GasConstants.EIP8037_TX_MAX_GAS_LIMIT);
        }

        private const string SenderAddress = "0x1111111111111111111111111111111111111111";

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


        [Fact]
        public async Task Given_LargeGasLimitAbove2Pow24_AtAmsterdam_When_Allocated_Then_ExecutionGasGrantCappedAndReservoirHoldsRemainder()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);

            const long gasLimit = 100_000_000;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "0x2222222222222222222222222222222222222222",
                Data = null,
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 1000,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

            var expectedIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                ctx.Data, isContractCreation: false, accessList: null, isSelfTransfer: false, hasValue: true);
            var expectedEvmGas = gasLimit - expectedIntrinsic;
            var expectedBudget = GasConstants.EIP8037_TX_MAX_GAS_LIMIT - expectedIntrinsic;
            var expectedGrant = System.Math.Min(expectedBudget, expectedEvmGas);
            var expectedReservoir = expectedEvmGas - expectedGrant;

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(expectedIntrinsic, resultCtx.IntrinsicExecutionGas);
            Assert.Equal(expectedGrant, resultCtx.ExecutionGasGrant);
            Assert.Equal(expectedReservoir, resultCtx.StateGasReservoir);
            Assert.True(expectedReservoir > 0, "a 100,000,000 gasLimit must overflow the 2^24 execution cap into a non-zero reservoir");
            Assert.Equal(GasConstants.EIP8037_TX_MAX_GAS_LIMIT - expectedIntrinsic, resultCtx.ExecutionGasGrant);
        }

        [Fact]
        public async Task Given_ModestGasLimit_AtAmsterdam_When_Allocated_Then_ReservoirIsZero_ExecutionGasGrantUnchangedFromPreExistingFormula()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync("0x2222222222222222222222222222222222222222", 1);
            var executionState = new ExecutionStateService(node);

            const long gasLimit = 100_000;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "0x2222222222222222222222222222222222222222",
                Data = null,
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 1000,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, resultCtx.StateGasReservoir);
            Assert.Equal(gasLimit - resultCtx.IntrinsicExecutionGas, resultCtx.ExecutionGasGrant);
        }

        [Fact]
        public async Task Given_LargeGasLimitAbove2Pow24_AtOsaka_When_Validated_Then_RejectedAtAdmission_NoCrossForkLeak()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);

            const long gasLimit = 100_000_000;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "0x2222222222222222222222222222222222222222",
                Data = null,
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 1000,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.IsValidationError);
            Assert.Equal("GAS_LIMIT_EXCEEDS_MAXIMUM", result.Error);
        }


        private static readonly byte[] InitCodeDeploying32ZeroBytes = "60206000F3".HexToByteArray();

        [Fact]
        public async Task Given_ContractDeployment_AtAmsterdam_When_CodeDepositCharged_Then_SplitsIntoKeccakExecutionPlusStateBytes()
        {
            var amsterdamConfig = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var osakaConfig = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

            async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> Deploy(HardforkConfig config)
            {
                var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
                var executionState = new ExecutionStateService(node);
                var ctx = new TransactionExecutionContext
                {
                    Sender = SenderAddress,
                    To = "",
                    Data = InitCodeDeploying32ZeroBytes,
                    IsContractCreation = true,
                    GasLimit = 500_000,
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
                return await RunAsync(config, ctx);
            }

            var (amsterdamCtx, amsterdamResult) = await Deploy(amsterdamConfig);
            var (osakaCtx, osakaResult) = await Deploy(osakaConfig);

            Assert.True(amsterdamResult.Success, amsterdamResult.Error);
            Assert.True(osakaResult.Success, osakaResult.Error);

            var amsterdamPostIntrinsic = amsterdamResult.GasUsed - amsterdamCtx.IntrinsicExecutionGas;
            var osakaPostIntrinsic = osakaResult.GasUsed - osakaCtx.IntrinsicExecutionGas;

            const int deployedLength = 32;
            const long expectedAmsterdamDeposit = 1 * GasConstants.EIP8037_CODE_HASH_PER_WORD
                + deployedLength * GasConstants.EIP8037_COST_PER_STATE_BYTE
                + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS;
            const long expectedOsakaDeposit = deployedLength * GasConstants.CREATE_DATA_GAS;

            var depositDelta = amsterdamPostIntrinsic - osakaPostIntrinsic;
            Assert.Equal(expectedAmsterdamDeposit - expectedOsakaDeposit, depositDelta);

            Assert.Equal(0, osakaResult.StateGasUsed);
            Assert.Equal((long)deployedLength * GasConstants.EIP8037_COST_PER_STATE_BYTE + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, amsterdamResult.StateGasUsed);
        }

        [Fact]
        public async Task Given_ContractDeployment_AtAmsterdam_When_ReservoirTooSmallForStateCost_Then_SpillsIntoExecutionGas_NotSilentlyDropped()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = InitCodeDeploying32ZeroBytes,
                IsContractCreation = true,
                GasLimit = 500_000,
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

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, resultCtx.StateGasReservoir);
            Assert.Equal(0, resultCtx.StateGas.FromReservoir);
            Assert.Equal(32 * GasConstants.EIP8037_COST_PER_STATE_BYTE + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, resultCtx.StateGas.SpilledIntoExecution);
        }


        private static readonly byte[] FactoryRuntimeCodeCreatingChild = new byte[]
        {
            0x64, 0x60, 0x20, 0x60, 0x00, 0xF3,
            0x60, 0x00,
            0x52,
            0x60, 0x05,
            0x60, 0x1B,
            0x60, 0x00,
            0xF0,
            0x00
        };

        private const string FactoryAddress = "0x3333333333333333333333333333333333333333";

        [Fact]
        public async Task Given_NestedCreate_AtAmsterdam_When_FactoryDeploysChild_Then_ChildCodeDepositUsesAmsterdamSplit()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(FactoryAddress, FactoryRuntimeCodeCreatingChild);
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = FactoryAddress,
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
                ExecutionState = executionState
            };

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(32 * GasConstants.EIP8037_COST_PER_STATE_BYTE + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, result.StateGasUsed);
            Assert.Equal(32 * GasConstants.EIP8037_COST_PER_STATE_BYTE + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, resultCtx.StateGas.SpilledIntoExecution);
        }

        [Fact]
        public async Task Given_NestedCreate_AtOsaka_When_FactoryDeploysChild_Then_StateGasUsedStaysZero_NoCrossForkLeak()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(FactoryAddress, FactoryRuntimeCodeCreatingChild);
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = FactoryAddress,
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
                ExecutionState = executionState
            };

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);
        }


        [Fact]
        public async Task Given_ContractDeployment_AtAmsterdam_When_StateGasHeavy_Then_ExecutionDimensionFloorsIndependently_NotByWholeTxFloor()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = InitCodeDeploying32ZeroBytes,
                IsContractCreation = true,
                GasLimit = 500_000,
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

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.GasRefund);
            Assert.True(result.StateGasUsed > 0, "code deposit must register on the state dimension at Amsterdam");

            var floorDataGas = config.IntrinsicGasRules.CalculateFloorGasLimit(
                ctx.Data, isContractCreation: true, isSelfTransfer: false, hasValue: false, accessList: null);

            Assert.True(floorDataGas < result.GasUsed, "test precondition: floor must not bind the sender bill here");

            var executionOnlyBasis = result.GasUsed - result.StateGasUsed;
            Assert.True(floorDataGas > executionOnlyBasis,
                "test precondition: the floor MUST bind the execution-only dimension for this case to be meaningful");

            Assert.Equal(floorDataGas, result.ExecutionGasUsed);
            Assert.NotEqual(executionOnlyBasis, result.ExecutionGasUsed);
        }

        [Fact]
        public async Task Given_ContractDeployment_AtOsaka_When_Settled_Then_StateGasUsedIsZero_ExecutionGasUsedEqualsGasUsed()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = InitCodeDeploying32ZeroBytes,
                IsContractCreation = true,
                GasLimit = 500_000,
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

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);
            Assert.Equal(result.GasUsed, result.ExecutionGasUsed);
        }


        private const string SstoreContractAddress = "0x6666666666666666666666666666666666666666";

        private static readonly byte[] SstoreFirstTimeSetRuntimeCode = new byte[]
        {
            0x60, 0x05,
            0x60, 0x01,
            0x55,
            0x00
        };

        private static readonly byte[] SstoreSetThenClearedBackToOriginalRuntimeCode = new byte[]
        {
            0x60, 0x05,
            0x60, 0x01,
            0x55,
            0x60, 0x00,
            0x60, 0x01,
            0x55,
            0x00
        };

        [Fact]
        public async Task Given_SstoreFirstTimeSetOfZeroSlot_AtAmsterdam_When_Charged_Then_StorageSetRegistersOnStateDimension()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(SstoreContractAddress, SstoreFirstTimeSetRuntimeCode);
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = SstoreContractAddress,
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
                ExecutionState = executionState
            };

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(GasConstants.EIP8037_STORAGE_SET_STATE_GAS, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_SstoreSetThenClearedBackToOriginal_AtAmsterdam_When_Netted_Then_UncappedCreditIsDisjointFromClassicRefund()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(SstoreContractAddress, SstoreSetThenClearedBackToOriginalRuntimeCode);
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = SstoreContractAddress,
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
                ExecutionState = executionState
            };

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);

            Assert.True(result.GasRefund > 0, "the classic restore-to-original refund must still fire on its own");
            Assert.True(result.GasRefund < GasConstants.EIP8037_STORAGE_SET_STATE_GAS,
                $"refund_counter ({result.GasRefund}) must never approach the UNCAPPED state-gas credit ({GasConstants.EIP8037_STORAGE_SET_STATE_GAS}) — contamination would inflate it far beyond the classic 1/5-capped STORAGE_WRITE refund");
        }


        private const string OuterAddress = "0x5555555555555555555555555555555555555555";
        private const string MiddleAddress = "0x4444444444444444444444444444444444444444";

        private static readonly byte[] MiddleRuntimeCodeCreatesThenReverts = new byte[]
        {
            0x64, 0x60, 0x20, 0x60, 0x00, 0xF3,
            0x60, 0x00,
            0x52,
            0x60, 0x05,
            0x60, 0x1B,
            0x60, 0x00,
            0xF0,
            0x50,
            0x60, 0x00,
            0x60, 0x00,
            0xFD
        };

        private static readonly byte[] OuterRuntimeCodeCallsMiddle = new byte[]
        {
            0x60, 0x00,
            0x60, 0x00,
            0x60, 0x00,
            0x60, 0x00,
            0x60, 0x00,
            0x73, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44,
                  0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44,
            0x5A,
            0xF1,
            0x50,
            0x00
        };

        [Fact]
        public async Task Given_NestedCreateChargesStateGas_AtAmsterdam_When_EnclosingCallFrameReverts_Then_ReservoirFullyRestored()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(OuterAddress, OuterRuntimeCodeCallsMiddle);
            await node.SetCodeAsync(MiddleAddress, MiddleRuntimeCodeCreatesThenReverts);
            var executionState = new ExecutionStateService(node);
            const long gasLimit = 100_000_000;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = OuterAddress,
                Data = null,
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

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.True(resultCtx.StateGasReservoir > 0, "test precondition: the top frame must actually receive a reservoir");

            Assert.Equal(0, result.StateGasUsed);

            Assert.Equal(resultCtx.StateGasReservoir, resultCtx.StateGas.ReservoirRemaining);
            Assert.Equal(0, resultCtx.StateGas.SpilledIntoExecution);
        }


        private const string InvalidOpcodeAddress = "0x7777777777777777777777777777777777777777";
        private static readonly byte[] InvalidOpcodeBytecode = new byte[] { 0xFE };

        [Fact]
        public async Task Given_TotalExecutionForfeit_AtAmsterdam_WithNonZeroReservoir_Then_GasUsedExcludesReservoir()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(InvalidOpcodeAddress, InvalidOpcodeBytecode);
            var executionState = new ExecutionStateService(node);
            const long gasLimit = 100_000_000;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = InvalidOpcodeAddress,
                Data = null,
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

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.False(result.Success);
            Assert.True(resultCtx.StateGasReservoir > 0, "test precondition: a reservoir must exist for this case to be meaningful");
            Assert.Equal(GasConstants.EIP8037_TX_MAX_GAS_LIMIT, result.GasUsed);
            Assert.Equal(resultCtx.IntrinsicExecutionGas + resultCtx.ExecutionGasGrant, result.GasUsed);
        }

        [Fact]
        public async Task Given_PlainRevert_AtAmsterdam_WithUnusedReservoir_Then_GasUsedReflectsOnlyExecutionSpent()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            const string revertAddress = "0x8888888888888888888888888888888888888888";
            await node.SetCodeAsync(revertAddress, "60006000FD".HexToByteArray());
            var executionState = new ExecutionStateService(node);
            const long gasLimit = 100_000_000;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = revertAddress,
                Data = null,
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

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.False(result.Success);
            Assert.True(resultCtx.StateGasReservoir > 0, "test precondition: a reservoir must exist for this case to be meaningful");
            Assert.Equal(resultCtx.IntrinsicExecutionGas + 6, result.GasUsed);
            Assert.Equal(0, resultCtx.StateGas.FromReservoir);
        }

        [Fact]
        public async Task Given_SuccessfulTransaction_AtAmsterdam_WithNonZeroReservoir_Then_GasUsedUnaffectedByForfeitBranch()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            const long gasLimit = 100_000_000;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "0x9999999999999999999999999999999999999999",
                Data = null,
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

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.True(resultCtx.StateGasReservoir > 0, "test precondition: a reservoir must exist for this case to be meaningful");
            Assert.Equal(resultCtx.IntrinsicExecutionGas, result.GasUsed);
            Assert.Equal(0, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_TotalExecutionForfeit_AtOsaka_Then_GasUsedStillBillsFullGasLimit_ByteIdentical()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(InvalidOpcodeAddress, InvalidOpcodeBytecode);
            var executionState = new ExecutionStateService(node);
            const long gasLimit = 1_000_000;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = InvalidOpcodeAddress,
                Data = null,
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

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.False(result.Success);
            Assert.False(result.IsValidationError, result.Error);
            Assert.Equal(0, resultCtx.StateGasReservoir);
            Assert.Equal(gasLimit, result.GasUsed);
        }


        [Fact]
        public async Task Given_TopLevelCreationTransaction_AtAmsterdam_OntoVirginAddress_Then_NewAccountCharged()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = new byte[] { 0x00 },
                IsContractCreation = true,
                GasLimit = 500_000,
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

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_TopLevelCreationTransaction_AtOsaka_Then_StateGasUsedStaysZero_NoCrossForkLeak()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = new byte[] { 0x00 },
                IsContractCreation = true,
                GasLimit = 500_000,
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

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);
        }

        private const string DrainedEoaFactoryAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static readonly byte[] FactoryRuntimeCodeCreatingEmptyChild = new byte[]
        {
            0x60, 0x00,
            0x60, 0x00,
            0x60, 0x00,
            0xF0,
            0x50,
            0x00
        };

        [Fact]
        public async Task Given_CreateTargetsADrainedEoa_AtAmsterdam_When_LivenessDecided_Then_NotCharged_NoSpuriousHalt()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(DrainedEoaFactoryAddress, FactoryRuntimeCodeCreatingEmptyChild);

            var drainedEoaAddress = Nethereum.Util.ContractUtils.CalculateContractAddress(DrainedEoaFactoryAddress, 0);
            await node.SetNonceAsync(drainedEoaAddress, 1);

            var executionState = new ExecutionStateService(node);
            const long gasLimit = 100_000;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = DrainedEoaFactoryAddress,
                Data = null,
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

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);
        }

        private const string SameBlockFactoryAddress = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        [Fact]
        public async Task Given_CreateTargetPreFundedByEarlierTxInSameBlock_AtAmsterdam_Then_NotCharged_ExecutionStateNotBlockStartSnapshot()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(SameBlockFactoryAddress, FactoryRuntimeCodeCreatingChild);
            var executionState = new ExecutionStateService(node);
            var createTarget = Nethereum.Util.ContractUtils.CalculateContractAddress(SameBlockFactoryAddress, 0);
            var executor = new TransactionExecutor(config);

            var fundingCtx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = createTarget,
                Data = null,
                IsContractCreation = false,
                GasLimit = 300_000,
                Value = 1,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };
            var fundingResult = await executor.ExecuteAsync(fundingCtx);
            Assert.True(fundingResult.Success, fundingResult.Error);

            var createCtx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = SameBlockFactoryAddress,
                Data = null,
                IsContractCreation = false,
                GasLimit = 500_000,
                Value = 0,
                GasPrice = 1,
                Nonce = 1,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };
            var createResult = await executor.ExecuteAsync(createCtx);

            Assert.True(createResult.Success, createResult.Error);
            Assert.Equal(32 * GasConstants.EIP8037_COST_PER_STATE_BYTE, createResult.StateGasUsed);
        }

        [Fact]
        public async Task Given_CreateTargetIsVirgin_AtAmsterdam_Then_NewAccountCharged_ControlForPreFundedCase()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(SameBlockFactoryAddress, FactoryRuntimeCodeCreatingChild);
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = SameBlockFactoryAddress,
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
                ExecutionState = executionState
            };

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(32 * GasConstants.EIP8037_COST_PER_STATE_BYTE + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, result.StateGasUsed);
        }

        private const string SelfDestructFactoryAddress = "0xcccccccccccccccccccccccccccccccccccccccc";
        private const string SelfDestructBeneficiary = "0xdddddddddddddddddddddddddddddddddddddddd";

        private static byte[] BuildInitCodeSelfDestructs()
        {
            var beneficiary = SelfDestructBeneficiary.Substring(2).HexToByteArray();
            var code = new byte[1 + 20 + 1];
            code[0] = 0x73;
            System.Buffer.BlockCopy(beneficiary, 0, code, 1, 20);
            code[21] = 0xFF;
            return code;
        }
        private static readonly byte[] InitCodeSelfDestructs = BuildInitCodeSelfDestructs();

        private static byte[] BuildFactoryRuntimeCodeCreatingSelfDestructingChild()
        {
            var initCode = InitCodeSelfDestructs;
            var bytecode = new System.Collections.Generic.List<byte>
            {
                0x75
            };
            bytecode.AddRange(initCode);
            bytecode.Add(0x60); bytecode.Add(0x00);
            bytecode.Add(0x52);
            bytecode.Add(0x60); bytecode.Add((byte)initCode.Length);
            bytecode.Add(0x60); bytecode.Add((byte)(32 - initCode.Length));
            bytecode.Add(0x60); bytecode.Add(0x00);
            bytecode.Add(0xF0);
            bytecode.Add(0x50);
            bytecode.Add(0x00);
            return bytecode.ToArray();
        }
        private static readonly byte[] FactoryRuntimeCodeCreatingSelfDestructingChild = BuildFactoryRuntimeCodeCreatingSelfDestructingChild();

        [Fact]
        public async Task Given_InitCodeSelfDestructs_AtAmsterdam_Then_TreatedAsSuccessfulHalt_NewAccountChargeStaysConsumed()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(SelfDestructFactoryAddress, FactoryRuntimeCodeCreatingSelfDestructingChild);
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = SelfDestructFactoryAddress,
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
                ExecutionState = executionState
            };

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, result.StateGasUsed);
        }

        [Fact]
        public async Task Given_CreateCollidesWithExistingContract_AtAmsterdam_Then_NewAccountChargeCreditedBack_NotConsumed()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(FactoryAddress, FactoryRuntimeCodeCreatingChild);
            var collisionTarget = Nethereum.Util.ContractUtils.CalculateContractAddress(FactoryAddress, 0);
            await node.SetCodeAsync(collisionTarget, new byte[] { 0x00 });
            var executionState = new ExecutionStateService(node);
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = FactoryAddress,
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
                ExecutionState = executionState
            };

            var (_, result) = await RunAsync(config, ctx);

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, result.StateGasUsed);
        }


        [Fact]
        public async Task Given_TopLevelCreationCollides_AtAmsterdam_WithNonZeroReservoir_Then_ForfeitExcludesReservoir_NotFullGasLimit()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var collisionTarget = Nethereum.Util.ContractUtils.CalculateContractAddress(SenderAddress, 0);
            await node.SetCodeAsync(collisionTarget, new byte[] { 0x00 });
            var executionState = new ExecutionStateService(node);

            const long reservoirGrant = 12_345;
            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = "",
                Data = new byte[] { 0x00 },
                IsContractCreation = true,
                GasLimit = GasConstants.EIP8037_TX_MAX_GAS_LIMIT + reservoirGrant,
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

            var (resultCtx, result) = await RunAsync(config, ctx);

            Assert.False(result.Success);
            Assert.Equal("ADDRESS_COLLISION", result.Error);
            Assert.True(resultCtx.StateGasReservoir > 0, "test setup must actually produce a nonzero reservoir");
            Assert.Equal(resultCtx.IntrinsicExecutionGas + resultCtx.ExecutionGasGrant, result.GasUsed);
            Assert.Equal(resultCtx.StateGasReservoir, ctx.GasLimit.ToLongSafe() - result.GasUsed);
        }


        private const string ClearCreditGrandparentAddress = "0x1212121212121212121212121212121212121212";
        private const string ClearCreditParentAddress = "0x1313131313131313131313131313131313131313";
        private const string ClearCreditGrandchildAddress = "0x1414141414141414141414141414141414141414";

        private const long BelowTxMaxGasLimit = 2_000_000;

        private static byte[] Concat(params byte[][] parts)
        {
            var bytes = new System.Collections.Generic.List<byte>();
            foreach (var part in parts) bytes.AddRange(part);
            return bytes.ToArray();
        }

        private static byte[] Push20(string address)
        {
            var code = new System.Collections.Generic.List<byte> { 0x73 };
            code.AddRange(address.HexToByteArray());
            return code.ToArray();
        }

        private static byte[] CallForwardingAllGas(string target) => Concat(
            new byte[] { 0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00 },
            Push20(target),
            new byte[] { 0x5A, 0xF1, 0x50 });

        private static byte[] DelegateCallForwardingAllGas(string target) => Concat(
            new byte[] { 0x60, 0x00, 0x60, 0x00, 0x60, 0x00, 0x60, 0x00 },
            Push20(target),
            new byte[] { 0x5A, 0xF4, 0x50 });

        private static readonly byte[] SstoreSetSlotOne = { 0x60, 0x05, 0x60, 0x01, 0x55 };
        private static readonly byte[] SstoreClearSlotOne = { 0x60, 0x00, 0x60, 0x01, 0x55 };
        private static readonly byte[] Revert = { 0x60, 0x00, 0x60, 0x00, 0xFD };
        private static readonly byte[] Stop = { 0x00 };

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunClearCreditAsync(
            byte[] parentCode)
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var node = await FundedNodeAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetCodeAsync(ClearCreditGrandparentAddress, Concat(CallForwardingAllGas(ClearCreditParentAddress), Stop));
            await node.SetCodeAsync(ClearCreditParentAddress, parentCode);
            await node.SetCodeAsync(ClearCreditGrandchildAddress, Concat(SstoreClearSlotOne, Stop));

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = ClearCreditGrandparentAddress,
                Data = null,
                IsContractCreation = false,
                GasLimit = BelowTxMaxGasLimit,
                Value = 0,
                GasPrice = 1,
                Nonce = 0,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 0,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = new ExecutionStateService(node)
            };

            return await RunAsync(config, ctx);
        }

        [Fact]
        public async Task Given_AGrandchildEarnedAStorageClearCredit_When_TheIntermediateChildReverts_Then_TheCreditIsDiscarded()
        {
            var (ctx, result) = await RunClearCreditAsync(Concat(
                SstoreSetSlotOne,
                DelegateCallForwardingAllGas(ClearCreditGrandchildAddress),
                Revert));

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, ctx.StateGasReservoir);
            Assert.Equal(0, ctx.StateGas.ReservoirRemaining);
            Assert.Equal(0, ctx.StateGasUsed);
        }

        [Fact]
        public async Task Given_AGrandchildEarnedAStorageClearCredit_When_TheIntermediateChildReturns_Then_TheCreditNetsTheChargeToZero()
        {
            var (ctx, result) = await RunClearCreditAsync(Concat(
                SstoreSetSlotOne,
                DelegateCallForwardingAllGas(ClearCreditGrandchildAddress),
                Stop));

            Assert.True(result.Success, result.Error);
            Assert.Equal(0, ctx.StateGasUsed);
        }

        [Fact]
        public async Task Given_NoGrandchildToClearTheSlot_When_TheIntermediateChildReturns_Then_TheStorageSetChargeStands()
        {
            var (ctx, result) = await RunClearCreditAsync(Concat(SstoreSetSlotOne, Stop));

            Assert.True(result.Success, result.Error);
            Assert.Equal(GasConstants.EIP8037_STORAGE_SET_STATE_GAS, ctx.StateGasUsed);
        }
    }
}
