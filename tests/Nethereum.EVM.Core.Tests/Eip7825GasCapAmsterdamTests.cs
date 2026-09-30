using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Precompiles;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class Eip7825GasCapAmsterdamTests
    {
        private const string SenderAddress = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string RecipientAddress = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private const long DeclaredGasLimitAboveCap = 120_000_000;

        private static readonly byte[] NoCalldata = new byte[0];

        private static readonly HardforkConfig OsakaWithPrecompiles =
            HardforkConfig.Osaka.WithPrecompiles(DefaultPrecompileRegistries.OsakaBase());
        private static readonly HardforkConfig AmsterdamWithPrecompiles =
            HardforkConfig.Amsterdam.WithPrecompiles(DefaultPrecompileRegistries.OsakaBase());
        private static readonly HardforkConfig PragueWithPrecompiles =
            HardforkConfig.Prague.WithPrecompiles(DefaultPrecompileRegistries.PragueBase());

        private static TransactionExecutionContext CreatePlainCallContext(long gasLimit, byte[] data = null)
        {
            var accounts = new Dictionary<string, AccountState>
            {
                [SenderAddress] = new AccountState { Balance = new EvmUInt256(1_000_000_000), Nonce = 0 },
                [RecipientAddress] = new AccountState { Code = new byte[0] }
            };
            var stateReader = new InMemoryStateReader(accounts);
            var executionState = new ExecutionStateService(stateReader);

            return new TransactionExecutionContext
            {
                Mode = ExecutionMode.Transaction,
                Sender = SenderAddress,
                To = RecipientAddress,
                Data = data ?? new byte[0],
                GasLimit = new EvmUInt256(gasLimit),
                Value = EvmUInt256.Zero,
                GasPrice = 0,
                ChainId = 1,
                BlockNumber = 1,
                Timestamp = 1000,
                Coinbase = "0x0000000000000000000000000000000000000000",
                BaseFee = 0,
                BlockGasLimit = 200_000_000,
                ExecutionState = executionState
            };
        }

        [Fact]
        [Trait("Rule", "AMS-8037-03")]
        public void Given_DeclaredGasLimitAbove2Pow24_When_ValidatedAtOsaka_Then_RejectedByRawGasLimitCap()
        {
            var ctx = CreatePlainCallContext(DeclaredGasLimitAboveCap);
            var executor = new TransactionExecutor(OsakaWithPrecompiles);

            var result = executor.Execute(ctx);

            Assert.True(result.IsValidationError);
            Assert.Equal(TransactionError.GasLimitExceedsMaximum, result.ErrorCode);
        }

        [Fact]
        [Trait("Rule", "AMS-8037-03")]
        public void Given_DeclaredGasLimitAbove2Pow24_ButIntrinsicExecutionGasFits_When_ValidatedAtAmsterdam_Then_AdmittedByIntrinsicExecutionCap()
        {
            var ctx = CreatePlainCallContext(DeclaredGasLimitAboveCap);
            var executor = new TransactionExecutor(AmsterdamWithPrecompiles);

            var result = executor.Execute(ctx);

            Assert.False(result.IsValidationError, $"Unexpected validation error: {result.Error}");
            Assert.True(result.Success, $"Execution failed: {result.Error}");
        }

        [Fact]
        [Trait("Rule", "AMS-8037-08")]
        public void Given_IntrinsicExecutionGasAboveCapAndFloorBelowIt_When_ValidatedAtAmsterdam_Then_RejectedAsIntrinsicGasTooLow()
        {
            var accessList = AccessListOverExecutionCap(AmsterdamWithPrecompiles);
            var rules = AmsterdamWithPrecompiles.IntrinsicGasRules;
            var intrinsicExecutionGas = rules.CalculateIntrinsicGas(NoCalldata, false, accessList, false, false);
            var floorGas = rules.CalculateFloorGasLimit(NoCalldata, false, false, false, accessList);

            Assert.True(intrinsicExecutionGas > GasConstants.EIP8037_TX_MAX_GAS_LIMIT, "the execution operand must exceed the cap");
            Assert.True(floorGas < GasConstants.EIP8037_TX_MAX_GAS_LIMIT, "the calldata floor must stay below the cap");

            var ctx = CreatePlainCallContext(intrinsicExecutionGas + 1_000_000, NoCalldata);
            ctx.AccessList = accessList;
            var executor = new TransactionExecutor(AmsterdamWithPrecompiles);

            var result = executor.Execute(ctx);

            Assert.True(result.IsValidationError);
            Assert.Equal(TransactionError.IntrinsicGasTooLow, result.ErrorCode);
        }

        [Fact]
        [Trait("Rule", "AMS-8037-08")]
        public void Given_IntrinsicExecutionGasJustBelowCap_When_ValidatedAtAmsterdam_Then_Admitted()
        {
            var accessList = AccessListOverExecutionCap(AmsterdamWithPrecompiles);
            accessList.RemoveAt(accessList.Count - 1);
            var rules = AmsterdamWithPrecompiles.IntrinsicGasRules;
            var intrinsicExecutionGas = rules.CalculateIntrinsicGas(NoCalldata, false, accessList, false, false);

            Assert.True(intrinsicExecutionGas <= GasConstants.EIP8037_TX_MAX_GAS_LIMIT, "the execution operand must stay within the cap");

            var ctx = CreatePlainCallContext(intrinsicExecutionGas + 1_000_000, NoCalldata);
            ctx.AccessList = accessList;
            var executor = new TransactionExecutor(AmsterdamWithPrecompiles);

            var result = executor.Execute(ctx);

            Assert.False(result.IsValidationError, result.Error);
        }

        private static List<AccessListEntry> AccessListOverExecutionCap(HardforkConfig config)
        {
            var rules = config.IntrinsicGasRules;
            var withoutAccessList = rules.CalculateIntrinsicGas(NoCalldata, false, null, false, false);
            var withOneEntry = rules.CalculateIntrinsicGas(NoCalldata, false, AccessListOf(1), false, false);
            var perEntry = withOneEntry - withoutAccessList;

            var entries = (int)((GasConstants.EIP8037_TX_MAX_GAS_LIMIT - withoutAccessList) / perEntry) + 1;
            return AccessListOf(entries);
        }

        private static List<AccessListEntry> AccessListOf(int entries)
        {
            var accessList = new List<AccessListEntry>(entries);
            for (var i = 0; i < entries; i++)
                accessList.Add(new AccessListEntry { Address = "0x" + (0x10000 + i).ToString("x40") });
            return accessList;
        }

        [Fact]
        [Trait("Rule", "AMS-8037-03")]
        public void Given_DeclaredGasLimitAbove2Pow24_When_ValidatedAtPreOsakaFork_Then_CapDoesNotApply()
        {
            var ctx = CreatePlainCallContext(DeclaredGasLimitAboveCap);
            var executor = new TransactionExecutor(PragueWithPrecompiles);

            var result = executor.Execute(ctx);

            Assert.False(result.IsValidationError, $"Unexpected validation error: {result.Error}");
            Assert.True(result.Success, $"Execution failed: {result.Error}");
        }
    }
}
