using System;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.Factory;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount
{
    public class AccountCreationAcrossForksTests
    {
        private const int ChainId = 31337;
        private const string OwnerPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string BundlerPrivateKey = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        [Theory]
        [InlineData("prague")]
        [InlineData("osaka")]
        [InlineData("amsterdam")]
        public async Task Given_TheAccountFactory_When_ItCreatesAnAccountDirectly_Then_TheAccountHasCodeAtEveryFork(string hardfork)
        {
            var owner = new Web3Account(OwnerPrivateKey, ChainId);

            await using var host = await InProcessBundlerHost.StartAsync(
                owner,
                ChainId,
                new[] { owner.Address },
                Nethereum.Web3.Web3.Convert.ToWei(10000),
                new DevChainConfig { ChainId = ChainId, Hardfork = hardfork, AutoMine = true });

            var entryPoint = await EntryPointService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3, new EntryPointDeployment());

            var validator = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3, new ECDSAValidatorDeployment());

            var factory = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3,
                new NethereumAccountFactoryDeployment { EntryPoint = entryPoint.ContractAddress });

            var initData = AccountInitDataBuilder.BuildEcdsa(validator.ContractAddress, owner.Address);
            var salt = ModularAccountBundlerFixture.CreateSalt(1);

            var expected = await factory.GetAddressQueryAsync(salt, initData);

            var receipt = await factory.CreateAccountRequestAndWaitForReceiptAsync(
                new CreateAccountFunction { Salt = salt, InitData = initData });

            Assert.True(receipt.Status.Value == 1,
                $"{hardfork}: createAccount reverted, so an account cannot be created at this fork at all");

            var code = await host.OperatorWeb3.Eth.GetCode.SendRequestAsync(expected);

            Assert.False(string.IsNullOrEmpty(code) || code == "0x",
                $"{hardfork}: createAccount succeeded but {expected} has no code");
        }

        [Theory]
        [InlineData("prague")]
        [InlineData("osaka")]
        [InlineData("amsterdam")]
        public async Task Given_AUserOperationCarryingInitCode_When_ItIsBundled_Then_TheAccountIsCreatedAtEveryFork(string hardfork)
        {
            var owner = new Web3Account(OwnerPrivateKey, ChainId);
            var bundlerAccount = new Web3Account(BundlerPrivateKey, ChainId);

            await using var host = await InProcessBundlerHost.StartAsync(
                owner,
                ChainId,
                new[] { owner.Address, bundlerAccount.Address },
                Nethereum.Web3.Web3.Convert.ToWei(10000),
                new DevChainConfig { ChainId = ChainId, Hardfork = hardfork, AutoMine = true });

            var entryPoint = await EntryPointService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3, new EntryPointDeployment());

            var validatorService = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3, new ECDSAValidatorDeployment());

            var factory = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3,
                new NethereumAccountFactoryDeployment { EntryPoint = entryPoint.ContractAddress });

            var bundler = host.StartBundler(
                entryPoint.ContractAddress, bundlerAccount, enableErc7562Validation: false,
                configureOverrides: c => c.MaxVerificationGas = 10_000_000);

            var ownerKey = EthECKey.GenerateKey();
            var salt = ModularAccountBundlerFixture.CreateSalt((ulong)Random.Shared.NextInt64());
            var initData = AccountInitDataBuilder.BuildEcdsa(
                validatorService.ContractAddress, ownerKey.GetPublicAddress());

            var accountAddress = await factory.GetAddressQueryAsync(salt, initData);
            await host.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(1));

            var initCodeBuilder = new NethereumAccountInitCodeBuilder(factory.ContractAddress, salt, initData);
            var initCode = Nethereum.Util.ByteUtil.Merge(
                initCodeBuilder.FactoryAddress.HexToByteArray(),
                initCodeBuilder.BuildFactoryData());

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                InitCode = initCode,
                CallData = new ExecuteFunction
                {
                    Mode = ERC7579ModeLib.EncodeSingleDefault(),
                    ExecutionCalldata = ERC7579ExecutionLib.EncodeSingle(
                        accountAddress, 0, new AddDepositFunction().GetCallData())
                }.GetCallData(),
                PreVerificationGas = 100_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await entryPoint.SignAndInitialiseUserOperationAsync(
                userOp,
                new AccountSigningOfflineService(ownerKey),
                new EcdsaValidatorModule(validatorService.ContractAddress));

            var userOpHash = await bundler.SendUserOperation.SendRequestAsync(
                UserOperationConverter.ToRpcFormat(packedOp), entryPoint.ContractAddress);

            Assert.False(string.IsNullOrEmpty(userOpHash), $"{hardfork}: the bundler refused the operation");

            var code = await host.OperatorWeb3.Eth.GetCode.SendRequestAsync(accountAddress);
            Assert.False(string.IsNullOrEmpty(code) || code == "0x",
                $"{hardfork}: the operation was accepted but {accountAddress} has no code");
        }

        [Theory]
        [InlineData("prague")]
        [InlineData("osaka")]
        [InlineData("amsterdam")]
        public async Task Given_AnAccountCreatingOperation_When_TheBundlerEstimatesItsGas_Then_ThatEstimateIsEnoughToRunIt(string hardfork)
        {
            var owner = new Web3Account(OwnerPrivateKey, ChainId);
            var bundlerAccount = new Web3Account(BundlerPrivateKey, ChainId);

            await using var host = await InProcessBundlerHost.StartAsync(
                owner,
                ChainId,
                new[] { owner.Address, bundlerAccount.Address },
                Nethereum.Web3.Web3.Convert.ToWei(10000),
                new DevChainConfig { ChainId = ChainId, Hardfork = hardfork, AutoMine = true });

            var entryPoint = await EntryPointService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3, new EntryPointDeployment());

            var validatorService = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3, new ECDSAValidatorDeployment());

            var factory = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                host.OperatorWeb3,
                new NethereumAccountFactoryDeployment { EntryPoint = entryPoint.ContractAddress });

            var bundler = host.StartBundler(
                entryPoint.ContractAddress, bundlerAccount, enableErc7562Validation: false,
                configureOverrides: c => c.MaxVerificationGas = 10_000_000);

            var ownerKey = EthECKey.GenerateKey();
            var salt = ModularAccountBundlerFixture.CreateSalt((ulong)Random.Shared.NextInt64());
            var initData = AccountInitDataBuilder.BuildEcdsa(
                validatorService.ContractAddress, ownerKey.GetPublicAddress());

            var accountAddress = await factory.GetAddressQueryAsync(salt, initData);
            await host.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(1));

            var initCodeBuilder = new NethereumAccountInitCodeBuilder(factory.ContractAddress, salt, initData);
            var initCode = Nethereum.Util.ByteUtil.Merge(
                initCodeBuilder.FactoryAddress.HexToByteArray(),
                initCodeBuilder.BuildFactoryData());

            var callData = new ExecuteFunction
            {
                Mode = ERC7579ModeLib.EncodeSingleDefault(),
                ExecutionCalldata = ERC7579ExecutionLib.EncodeSingle(
                    accountAddress, 0, new AddDepositFunction().GetCallData())
            }.GetCallData();

            var toEstimate = new UserOperation
            {
                Sender = accountAddress,
                InitCode = initCode,
                CallData = callData,
                PreVerificationGas = 100_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedForEstimate = await entryPoint.SignAndInitialiseUserOperationAsync(
                toEstimate,
                new AccountSigningOfflineService(ownerKey),
                new EcdsaValidatorModule(validatorService.ContractAddress));

            var estimate = await bundler.EstimateUserOperationGas.SendRequestAsync(
                UserOperationConverter.ToRpcFormat(packedForEstimate), entryPoint.ContractAddress);

            Assert.NotNull(estimate?.VerificationGasLimit);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                InitCode = initCode,
                CallData = callData,
                CallGasLimit = estimate.CallGasLimit.Value,
                VerificationGasLimit = estimate.VerificationGasLimit.Value,
                PreVerificationGas = estimate.PreVerificationGas.Value,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await entryPoint.SignAndInitialiseUserOperationAsync(
                userOp,
                new AccountSigningOfflineService(ownerKey),
                new EcdsaValidatorModule(validatorService.ContractAddress));

            var userOpHash = await bundler.SendUserOperation.SendRequestAsync(
                UserOperationConverter.ToRpcFormat(packedOp), entryPoint.ContractAddress);

            Assert.False(string.IsNullOrEmpty(userOpHash),
                $"{hardfork}: the bundler estimated verificationGasLimit " +
                $"{estimate.VerificationGasLimit.Value} and then refused the operation it sized itself");

            var code = await host.OperatorWeb3.Eth.GetCode.SendRequestAsync(accountAddress);
            Assert.False(string.IsNullOrEmpty(code) || code == "0x",
                $"{hardfork}: the bundler's own estimate was not enough to deploy {accountAddress}");
        }
    }
}
