using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.IntegrationTests.E2E;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Xunit;
using RpcUserOperationGasEstimate = Nethereum.RPC.AccountAbstraction.DTOs.UserOperationGasEstimate;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(ModularAccountBundlerCollection.COLLECTION_NAME)]
    public class ModularAccountExecuteEncoderTests
    {
        private readonly ModularAccountBundlerFixture _fixture;

        public ModularAccountExecuteEncoderTests(ModularAccountBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<(string accountAddress, IAccountSigningService signingService, IErc7579ValidatorModule validator)> DeployModularAccountAsync(ulong saltSeed)
        {
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            var salt = ModularAccountBundlerFixture.CreateSalt(saltSeed);

            var initData = AccountInitDataBuilder.BuildEcdsa(_fixture.EcdsaValidatorService.ContractAddress, ownerAddress);
            var accountAddress = await _fixture.FactoryService.GetAddressQueryAsync(salt, initData);

            await _fixture.FactoryService.CreateAccountRequestAndWaitForReceiptAsync(salt, initData);
            await _fixture.Bootstrap.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var signingService = new AccountSigningOfflineService(ownerKey);
            var validator = new EcdsaValidatorModule(_fixture.EcdsaValidatorService.ContractAddress);

            return (accountAddress, signingService, validator);
        }

        [Fact]
        [Trait("UseCase", "CustomContract")]
        [Trait("Doc", "docs/aa/custom.md#idiomatic-send")]
        [Trait("Spec", "§5.Tab6")]
        public async Task Given_modular_account_with_erc7579_encoder_When_send_typed_function_via_handler_Then_lands()
        {
            var (accountAddress, signingService, validator) = await DeployModularAccountAsync((ulong)System.Random.Shared.NextInt64());

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());

            var initialCount = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, initialCount);

            testCounter.ChangeContractHandlerToAA(
                    accountAddress,
                    signingService,
                    validator,
                    _fixture.Bundler,
                    _fixture.EntryPointService.ContractAddress)
                .WithErc7579Execution();

            var receipt = (AATransactionReceipt)await testCounter.CountRequestAndWaitForReceiptAsync();

            Assert.True(receipt.UserOpSuccess, receipt.RevertReason);
            Assert.Equal(accountAddress.ToLower(), receipt.Sender?.ToLower());

            var finalCount = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, finalCount);
        }

        [Fact]
        [Trait("UseCase", "CustomContract")]
        [Trait("Doc", "docs/aa/custom.md#idiomatic-send")]
        [Trait("Spec", "§5.Tab6")]
        public async Task Given_modular_account_When_BatchExecute_Then_both_calls_applied()
        {
            var (accountAddress, signingService, validator) = await DeployModularAccountAsync((ulong)System.Random.Shared.NextInt64());

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());

            var handler = testCounter.ChangeContractHandlerToAA(
                    accountAddress,
                    signingService,
                    validator,
                    _fixture.Bundler,
                    _fixture.EntryPointService.ContractAddress)
                .WithErc7579Execution();

            var receipt = await handler.BatchExecuteAsync(
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall());

            Assert.True(receipt.UserOpSuccess, receipt.RevertReason);

            var finalCount = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(2), finalCount);
        }

        [Fact]
        [Trait("UseCase", "CustomContract")]
        [Trait("Doc", "docs/aa/custom.md#idiomatic-send")]
        [Trait("Spec", "§5.Tab6")]
        public async Task Given_modular_call_reverts_When_send_Then_revert_decoded()
        {
            var (accountAddress, signingService, validator) = await DeployModularAccountAsync((ulong)System.Random.Shared.NextInt64());

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());

            var bundlerWithFixedEstimate = new FixedEstimateBundlerService(_fixture.Bundler, new RpcUserOperationGasEstimate
            {
                CallGasLimit = new HexBigInteger(300_000),
                VerificationGasLimit = new HexBigInteger(500_000),
                PreVerificationGas = new HexBigInteger(100_000)
            });

            testCounter.ChangeContractHandlerToAA(
                    accountAddress,
                    signingService,
                    validator,
                    bundlerWithFixedEstimate,
                    _fixture.EntryPointService.ContractAddress)
                .WithErc7579Execution();

            var receipt = (AATransactionReceipt)await testCounter.ContractHandler
                .SendRequestAndWaitForReceiptAsync(new CountFailFunction());

            Assert.False(receipt.UserOpSuccess);
            Assert.Equal(UserOperationFailureKind.RevertedWithReason, receipt.FailureKind);
            Assert.Contains("count failed", receipt.RevertReason);
        }
    }
}
