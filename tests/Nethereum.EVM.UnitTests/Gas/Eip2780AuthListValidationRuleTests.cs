using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.UnitTests;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip2780AuthListValidationRuleTests
    {
        private const string SenderAddress = "0x1111111111111111111111111111111111111111";
        private const string TargetAddress = "0x2222222222222222222222222222222222222222";
        private const string DelegateAddress = "0x3333333333333333333333333333333333333333";
        private const long DerivedAmsterdamAuthCost = 7816;
        private const long FlatPreAmsterdamAuthCost = 25000;

        private static Authorisation7702Signed SignAuthorization(string privateKeyHex)
        {
            var key = new EthECKey(privateKeyHex);
            var auth = new Authorisation7702 { ChainId = 1, Address = DelegateAddress, Nonce = 0 };
            var signer = new Authorisation7702Signer();
            return signer.SignAuthorisation(key, auth);
        }

        private static async Task<(TransactionExecutionContext ctx, TransactionExecutionResult result)> RunAsync(
            HardforkConfig config, List<Authorisation7702Signed> authorisationList)
        {
            var node = new EIP7702TestNodeDataService();
            await node.SetBalanceAsync(SenderAddress, BigInteger.Parse("1000000000000000000000"));
            var executionState = new ExecutionStateService(node);

            var ctx = new TransactionExecutionContext
            {
                Sender = SenderAddress,
                To = TargetAddress,
                Data = Array.Empty<byte>(),
                IsContractCreation = false,
                GasLimit = 5_000_000,
                Value = 0,
                MaxFeePerGas = 100,
                MaxPriorityFeePerGas = 10,
                GasPrice = 100,
                Nonce = 0,
                AuthorisationList = authorisationList,
                BlockNumber = 1,
                Timestamp = 1704067200,
                BaseFee = 7,
                ChainId = 1,
                Coinbase = SenderAddress,
                ExecutionState = executionState
            };

            var executor = new TransactionExecutor(config);
            var result = await executor.ExecuteAsync(ctx);
            return (ctx, result);
        }

        [Fact]
        public async Task Given_SingleAuthorization_AtAmsterdam_When_IntrinsicComputed_Then_ChargesDerived7816()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var withAuth = new List<Authorisation7702Signed>
            {
                SignAuthorization("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80")
            };

            var (ctxWith, resultWith) = await RunAsync(config, withAuth);
            var (ctxWithout, resultWithout) = await RunAsync(config, null);

            Assert.False(resultWith.IsValidationError, resultWith.Error);
            Assert.False(resultWithout.IsValidationError, resultWithout.Error);
            Assert.Equal(DerivedAmsterdamAuthCost, ctxWith.IntrinsicExecutionGas - ctxWithout.IntrinsicExecutionGas);
        }

        [Fact]
        public async Task Given_ThreeAuthorizations_AtAmsterdam_When_IntrinsicComputed_Then_ScalesLinearlyAt7816PerTuple()
        {
            var config = HardforkConfig.Amsterdam.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var authList = new List<Authorisation7702Signed>
            {
                SignAuthorization("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80"),
                SignAuthorization("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d"),
                SignAuthorization("0x9895d1edcf187d1fa60122ca9b1ae14cda00407ff333b81d7c620f7b113b2663"),
            };

            var (ctxWith, resultWith) = await RunAsync(config, authList);
            var (ctxWithout, resultWithout) = await RunAsync(config, null);

            Assert.False(resultWith.IsValidationError, resultWith.Error);
            Assert.Equal(3 * DerivedAmsterdamAuthCost, ctxWith.IntrinsicExecutionGas - ctxWithout.IntrinsicExecutionGas);
        }

        [Fact]
        public async Task Given_SingleAuthorization_AtPrague_When_IntrinsicComputed_Then_StillCharges25000_NoCrossForkLeak()
        {
            var config = HardforkConfig.Prague.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.PragueBase());
            var withAuth = new List<Authorisation7702Signed>
            {
                SignAuthorization("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80")
            };

            var (ctxWith, resultWith) = await RunAsync(config, withAuth);
            var (ctxWithout, resultWithout) = await RunAsync(config, null);

            Assert.False(resultWith.IsValidationError, resultWith.Error);
            Assert.Equal(FlatPreAmsterdamAuthCost, ctxWith.IntrinsicExecutionGas - ctxWithout.IntrinsicExecutionGas);
        }

        [Fact]
        public async Task Given_SingleAuthorization_AtOsaka_When_IntrinsicComputed_Then_StillCharges25000_NoCrossForkLeak()
        {
            var config = HardforkConfig.Osaka.WithPrecompiles(Nethereum.EVM.Precompiles.DefaultPrecompileRegistries.OsakaBase());
            var withAuth = new List<Authorisation7702Signed>
            {
                SignAuthorization("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80")
            };

            var (ctxWith, resultWith) = await RunAsync(config, withAuth);
            var (ctxWithout, resultWithout) = await RunAsync(config, null);

            Assert.False(resultWith.IsValidationError, resultWith.Error);
            Assert.Equal(FlatPreAmsterdamAuthCost, ctxWith.IntrinsicExecutionGas - ctxWithout.IntrinsicExecutionGas);
        }
    }
}
