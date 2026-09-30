using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8037PrecompileDispatchGasTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string DelegateAddress = "0x3333333333333333333333333333333333333333";

        private const string IdentityPrecompile = "0x0000000000000000000000000000000000000004";
        private const long IdentityBaseCost = 15;
        private const long AccountWriteCost = GasConstants.EIP8038_ACCOUNT_WRITE;

        private const string PairingPrecompile = "0x0000000000000000000000000000000000000008";
        private const long PairingBaseCost = 45_000;
        private static readonly byte[] MalformedPairingInput = new byte[] { 0x00 };

        private const string PrivateKey1 = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private static readonly string Authority1 = EthECKey.GetPublicAddress(PrivateKey1);

        private const string PrivateKey2 = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690";
        private static readonly string Authority2 = EthECKey.GetPublicAddress(PrivateKey2);

        private static Authorisation7702Signed SignAuthorization(string privateKeyHex, string delegateAddress, BigInteger nonce)
        {
            var key = new EthECKey(privateKeyHex);
            var auth = new Authorisation7702 { ChainId = 1, Address = delegateAddress, Nonce = nonce };
            return new Authorisation7702Signer().SignAuthorisation(key, auth);
        }

        private static async Task<ExecutionStateService> StateWithAlreadyDelegatedAuthorityAsync()
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            await node.SetBalanceAsync(Authority1, BigInteger.Parse("1000000000000000000"));
            await node.SetCodeAsync(Authority1, Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress));
            return new ExecutionStateService(node);
        }

        private static async Task<ExecutionStateService> StateWithFreshAuthorityAsync()
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            return new ExecutionStateService(node);
        }

        private static TransactionExecutionContext PrecompileTxCtx(
            ExecutionStateService executionState, List<Authorisation7702Signed> authList, long gasLimit)
            => new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = IdentityPrecompile,
                Data = Array.Empty<byte>(),
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 0,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

        private static TransactionExecutionContext PairingValueTransferTxCtx(
            ExecutionStateService executionState, List<Authorisation7702Signed> authList, long gasLimit)
            => new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = PairingPrecompile,
                Data = MalformedPairingInput,
                IsContractCreation = false,
                GasLimit = gasLimit,
                Value = 1,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

        private static HardforkConfig AmsterdamWithPrecompiles()
            => HardforkConfig.Amsterdam.WithPrecompiles(
                Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());

        [Fact]
        [Trait("Category", "EIP8037")]
        public async Task Given_AnAuthChargeLeavesTooLittle_When_APrecompileIsCalled_Then_ItRunsOutOfGas()
        {
            var executionState = await StateWithAlreadyDelegatedAuthorityAsync();
            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };

            var ctx = PrecompileTxCtx(executionState, authList, gasLimit: 31_830);
            var result = await new TransactionExecutor(AmsterdamWithPrecompiles()).ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);

            var grant = ctx.ExecutionGasGrant;
            var consumed = ctx.PreDispatchExecutionGasConsumed;

            Assert.True(consumed >= AccountWriteCost,
                $"expected the auth list to charge ACCOUNT_WRITE before dispatch; consumed {consumed}");
            Assert.True(grant >= IdentityBaseCost && grant - consumed < IdentityBaseCost,
                $"test window not straddled: grant {grant}, consumed {consumed}, precompile {IdentityBaseCost}");

            Assert.False(result.Success);
            Assert.Equal("PRECOMPILE_OUT_OF_GAS", result.Error);
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public async Task Given_APrecompileCallThatSucceeds_When_AnAuthChargePreceded_It_Then_ThatChargeIsStillBilled()
        {
            var executionState = await StateWithAlreadyDelegatedAuthorityAsync();
            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey1, DelegateAddress, 0) };

            var ctx = PrecompileTxCtx(executionState, authList, gasLimit: 500_000);
            var result = await new TransactionExecutor(AmsterdamWithPrecompiles()).ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(result.Success, result.Error);

            var consumed = ctx.PreDispatchExecutionGasConsumed;
            Assert.True(consumed >= AccountWriteCost,
                $"expected the auth list to charge ACCOUNT_WRITE before dispatch; consumed {consumed}");

            Assert.Equal(ctx.IntrinsicExecutionGas + IdentityBaseCost + consumed, result.GasUsed);
        }


        [Fact]
        [Trait("Category", "EIP8037")]
        public async Task Given_ValueTransferToEmptyPrecompile_When_PrecompileExceptionallyHalts_Then_NewAccountStateGasRefills()
        {
            var executionState = await StateWithFreshAuthorityAsync();
            var ctx = PairingValueTransferTxCtx(executionState, authList: null, gasLimit: 20_000_000);
            var result = await new TransactionExecutor(AmsterdamWithPrecompiles()).ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.True(ctx.StateGasReservoir > GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS,
                $"test window not straddled: reservoir {ctx.StateGasReservoir} must exceed the {GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS} NEW_ACCOUNT charge");

            Assert.False(result.Success);
            Assert.Equal("PRECOMPILE_FAILED", result.Error);

            Assert.Equal(0, ctx.StateGas.FromReservoir);
            Assert.Equal(ctx.IntrinsicExecutionGas + ctx.ExecutionGasGrant, result.GasUsed);
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public async Task Given_AuthorizationsCommittedThenPrecompileHalts_When_Settled_Then_OnlyRecipientNewAccountRefillsAuthStateStaysConsumed()
        {
            var executionState = await StateWithFreshAuthorityAsync();
            var authList = new List<Authorisation7702Signed> { SignAuthorization(PrivateKey2, DelegateAddress, 0) };
            var ctx = PairingValueTransferTxCtx(executionState, authList, gasLimit: 20_000_000);
            var result = await new TransactionExecutor(AmsterdamWithPrecompiles()).ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            var committedAuthStateGas = GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS + GasConstants.EIP8037_AUTH_BASE_STATE_GAS;
            Assert.True(ctx.StateGasReservoir > committedAuthStateGas + GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS,
                $"test window not straddled: reservoir {ctx.StateGasReservoir} must exceed the authorization's committed {committedAuthStateGas} plus the recipient's own {GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS}");

            Assert.False(result.Success);
            Assert.Equal("PRECOMPILE_FAILED", result.Error);

            Assert.Equal(committedAuthStateGas, ctx.StateGas.FromReservoir);
            Assert.Equal(ctx.IntrinsicExecutionGas + ctx.ExecutionGasGrant + committedAuthStateGas, result.GasUsed);
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public async Task Given_ValueTransferToEmptyPrecompile_When_PrecompileRunsOutOfForwardedGas_Then_NewAccountStateGasRefills()
        {
            var config = AmsterdamWithPrecompiles();
            var baseIntrinsic = config.IntrinsicGasRules.CalculateIntrinsicGas(
                MalformedPairingInput, isContractCreation: false, accessList: null,
                isSelfTransfer: false, hasValue: true);

            const long perTupleWeight = 7_816 + AccountWriteCost;
            var capacity = GasConstants.EIP8037_TX_MAX_GAS_LIMIT - baseIntrinsic;
            var authCount = (int)((capacity - PairingBaseCost) / perTupleWeight) + 1;

            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var authList = new List<Authorisation7702Signed>(authCount);
            for (var i = 0; i < authCount; i++)
            {
                var authorityKey = EthECKey.GenerateKey();
                await node.SetBalanceAsync(authorityKey.GetPublicAddress(), BigInteger.One);
                await node.SetCodeAsync(authorityKey.GetPublicAddress(), Eip7702DelegationUtils.CreateDelegationCode(DelegateAddress));
                var auth = new Authorisation7702 { ChainId = 1, Address = DelegateAddress, Nonce = 0 };
                authList.Add(new Authorisation7702Signer().SignAuthorisation(authorityKey, auth));
            }
            var executionState = new ExecutionStateService(node);

            var ctx = PairingValueTransferTxCtx(executionState, authList, gasLimit: GasConstants.EIP8037_TX_MAX_GAS_LIMIT + 1_000_000);
            var result = await new TransactionExecutor(config).ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);

            var available = ctx.PreDispatchExecutionGasAvailable;
            Assert.True(ctx.StateGasReservoir > GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS,
                $"test window not straddled: reservoir {ctx.StateGasReservoir} must exceed the {GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS} NEW_ACCOUNT charge");
            Assert.True(available > 0 && available < PairingBaseCost,
                $"test window not straddled: available {available} must sit in (0, {PairingBaseCost}) — authCount {authCount}, grant {ctx.ExecutionGasGrant}, consumed {ctx.PreDispatchExecutionGasConsumed}");

            Assert.False(result.Success);
            Assert.Equal("PRECOMPILE_OUT_OF_GAS", result.Error);

            Assert.Equal(0, ctx.StateGas.FromReservoir);
            Assert.Equal(ctx.IntrinsicExecutionGas + ctx.ExecutionGasGrant, result.GasUsed);
        }

        [Fact]
        [Trait("Category", "EIP8037")]
        public async Task Given_ValueTransferToEmptyPrecompile_AtOsaka_When_PrecompileExceptionallyHalts_Then_GasUsedUnchanged_NoCrossForkLeak()
        {
            var executionState = await StateWithFreshAuthorityAsync();
            var osakaConfig = HardforkConfig.Osaka.WithPrecompiles(
                Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            Assert.False(osakaConfig.IntrinsicGasRules.StateGasActive,
                "Osaka must be pre-Amsterdam for this cross-fork check to mean anything");

            var ctx = PairingValueTransferTxCtx(executionState, authList: null, gasLimit: 200_000);
            var result = await new TransactionExecutor(osakaConfig).ExecuteAsync(ctx);

            Assert.False(result.IsValidationError, result.Error);
            Assert.False(result.Success);
            Assert.Equal("PRECOMPILE_FAILED", result.Error);

            Assert.Equal(0, ctx.StateGasReservoir);
            Assert.Equal(0, ctx.StateGas.FromReservoir);
            Assert.Equal(ctx.GasLimit.ToLongSafe(), result.GasUsed);
            Assert.Equal(ctx.IntrinsicExecutionGas + ctx.ExecutionGasGrant, result.GasUsed);
        }
    }
}
