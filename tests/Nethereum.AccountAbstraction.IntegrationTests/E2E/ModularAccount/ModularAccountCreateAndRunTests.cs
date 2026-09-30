using System;
using System.Threading.Tasks;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.Factory;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    [Collection(ModularAccountBundlerCollection.COLLECTION_NAME)]
    public class ModularAccountCreateAndRunTests
    {
        private readonly ModularAccountBundlerFixture _fixture;

        public ModularAccountCreateAndRunTests(ModularAccountBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [Trait("UseCase", "CreateAccount")]
        [Trait("Doc", "docs/aa/accounts.md#create")]
        [Trait("Spec", "§5.Tab1")]
        public async Task Given_owner_and_salt_When_create_modular_account_and_send_first_op_Then_deployed_and_op_lands()
        {
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            var salt = ModularAccountBundlerFixture.CreateSalt((ulong)Random.Shared.NextInt64());

            var initData = AccountInitDataBuilder.BuildEcdsa(_fixture.EcdsaValidatorService.ContractAddress, ownerAddress);

            var accountAddress = await _fixture.FactoryService.GetAddressQueryAsync(salt, initData);

            var codeBeforeDeploy = await _fixture.Bootstrap.Node.GetCodeAsync(accountAddress);
            Assert.True(codeBeforeDeploy == null || codeBeforeDeploy.Length == 0);

            await _fixture.Bootstrap.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(1));

            var initCodeBuilder = new NethereumAccountInitCodeBuilder(_fixture.FactoryService.ContractAddress, salt, initData);
            var initCode = Nethereum.Util.ByteUtil.Merge(
                initCodeBuilder.FactoryAddress.HexToByteArray(),
                initCodeBuilder.BuildFactoryData());

            var addDepositCallData = new AddDepositFunction().GetCallData();
            var executeCallData = new ExecuteFunction
            {
                Mode = ERC7579ModeLib.EncodeSingleDefault(),
                ExecutionCalldata = ERC7579ExecutionLib.EncodeSingle(accountAddress, 0, addDepositCallData)
            }.GetCallData();

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                InitCode = initCode,
                CallData = executeCallData,
                CallGasLimit = 300_000,
                VerificationGasLimit = 500_000,
                PreVerificationGas = 100_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var signingService = new AccountSigningOfflineService(ownerKey);
            var validator = new EcdsaValidatorModule(_fixture.EcdsaValidatorService.ContractAddress);

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, signingService, validator);
            var rpcOp = UserOperationConverter.ToRpcFormat(packedOp);

            var userOpHash = await _fixture.Bundler.SendUserOperation.SendRequestAsync(rpcOp, _fixture.EntryPointService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(userOpHash));

            var receipt = await _fixture.Bundler.GetUserOperationReceipt.SendRequestAsync(userOpHash);
            Assert.NotNull(receipt);
            Assert.True(receipt.Success, receipt.Reason);

            var codeAfterDeploy = await _fixture.Bootstrap.Node.GetCodeAsync(accountAddress);
            Assert.NotNull(codeAfterDeploy);
            Assert.True(codeAfterDeploy.Length > 0);
        }

        [Fact]
        [Trait("UseCase", "CreateAccount")]
        [Trait("Doc", "docs/aa/accounts.md#create")]
        [Trait("Spec", "§5.Tab1")]
        public async Task Given_unfunded_counterfactual_When_first_op_Then_AA21_minus32500()
        {
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            var salt = ModularAccountBundlerFixture.CreateSalt((ulong)Random.Shared.NextInt64());

            var initData = AccountInitDataBuilder.BuildEcdsa(_fixture.EcdsaValidatorService.ContractAddress, ownerAddress);
            var accountAddress = await _fixture.FactoryService.GetAddressQueryAsync(salt, initData);

            var initCodeBuilder = new NethereumAccountInitCodeBuilder(_fixture.FactoryService.ContractAddress, salt, initData);
            var initCode = Nethereum.Util.ByteUtil.Merge(
                initCodeBuilder.FactoryAddress.HexToByteArray(),
                initCodeBuilder.BuildFactoryData());

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 500_000,
                PreVerificationGas = 100_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var signingService = new AccountSigningOfflineService(ownerKey);
            var validator = new EcdsaValidatorModule(_fixture.EcdsaValidatorService.ContractAddress);

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, signingService, validator);
            var rpcOp = UserOperationConverter.ToRpcFormat(packedOp);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                _fixture.Bundler.SendUserOperation.SendRequestAsync(rpcOp, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.SimulateValidation, ex.Code);
            Assert.Contains("AA21", ex.Message);
        }
    }
}
