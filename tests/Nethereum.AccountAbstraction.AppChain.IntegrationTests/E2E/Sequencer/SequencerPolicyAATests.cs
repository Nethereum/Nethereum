using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry.ContractDefinition;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AppChain;

using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Contracts;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Sequencer
{
    public class SequencerPolicyAATests : IAsyncLifetime
    {
        private const int CHAIN_ID = 420421;

        private AppChainCore _appChain = null!;
        private ISequencer _sequencer = null!;
        private AppChainNode _node = null!;
        private IWeb3 _web3 = null!;
        private BundlerService _bundlerService = null!;
        private AppChainRpcClient _rpcClient = null!;
        private AppChainComposedNode? _composed;
        private bool _signRecoverableBeforeCompose;

        private EntryPointService _entryPointService = null!;
        private NethereumAccountFactoryService _accountFactoryService = null!;
        private ECDSAValidatorService _ecdsaValidatorService = null!;
        private AccountRegistryService _accountRegistryService = null!;

        private Account _operatorAccount = null!;
        private Account _bundlerAccount = null!;
        private Account _userAccount = null!;
        private Account _unauthorizedBundlerAccount = null!;

        private byte[] EncodeInitData(string ownerAddress)
        {
            return Nethereum.AccountAbstraction.ERC7579.Modules.AccountInitDataBuilder.BuildEcdsa(
                _ecdsaValidatorService.ContractAddress, ownerAddress);
        }

        private byte[] CreateERC7579ExecuteCallData(string target, BigInteger value, byte[] data)
        {
            return new Nethereum.AccountAbstraction.Execution.Erc7579ExecuteEncoder().EncodeExecute(target, value, data);
        }

        private string _operatorPrivateKey = null!;

        public Task InitializeAsync()
        {
            _operatorPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
            _operatorAccount = new Account(_operatorPrivateKey, CHAIN_ID);
            _bundlerAccount = new Account("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a", CHAIN_ID);
            _userAccount = new Account("0x7c852118294e51e653712a81e05800f419141751be58f605c371e15141b007a6", CHAIN_ID);
            _unauthorizedBundlerAccount = new Account("0x47e179ec197488593b187f80a00eb0da91f1b9d0b13f8733639f19c30a34926a", CHAIN_ID);
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            _bundlerService?.Dispose();
            if (_composed != null)
            {
                await _composed.DisposeAsync();
                EthECKey.SignRecoverable = _signRecoverableBeforeCompose;
            }
        }

        private async Task SetupWithPolicyAsync(PolicyConfig policyConfig)
        {
            var config = new AppChainServerConfig
            {
                ChainId = CHAIN_ID,
                ChainName = "PolicyAATest",
                BaseFee = 1_000_000_000
            };
            config.Genesis.Owner.PrivateKey = _operatorPrivateKey;
            config.Consensus.Sequencer.PrivateKey = _operatorPrivateKey;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            config.Consensus.BlockTimeMs = 0;
            config.Consensus.Policy = policyConfig;
            config.Node.Storage.InMemory = true;
            config.Node.Network.Serve = false;
            config.Node.Sync.Mode = SyncMode.None;
            config.Mud.DeployWorld = false;

            _signRecoverableBeforeCompose = EthECKey.SignRecoverable;
            _composed = await AppChainComposition.ComposeAsync(config, NullLoggerFactory.Instance, CancellationToken.None);
            _appChain = _composed.AppChain;
            _sequencer = _composed.Sequencer!;
            _node = _composed.Node;

            var prefundBalance = Web3.Web3.Convert.ToWei(1000);
            foreach (var address in new[] { _bundlerAccount.Address, _userAccount.Address, _unauthorizedBundlerAccount.Address })
            {
                await _appChain.State.SaveAccountAsync(address, new Nethereum.Model.Account
                {
                    Balance = prefundBalance,
                    Nonce = 0
                });
            }

            _rpcClient = new AppChainRpcClient(_node, CHAIN_ID);
            _web3 = new Web3.Web3(_operatorAccount, _rpcClient);
            _web3.TransactionManager.UseLegacyAsDefault = true;

            await DeployAAContractsAsync();
            SetupBundlerService(_bundlerAccount);
        }

        private async Task DeployAAContractsAsync()
        {
            _entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                _web3, new EntryPointDeployment());

            var accountRegistryDeployment = new AccountRegistryDeployment
            {
                InitialAdmin = _operatorAccount.Address
            };
            _accountRegistryService = await AccountRegistryService.DeployContractAndGetServiceAsync(
                _web3, accountRegistryDeployment);

            var accountFactoryDeployment = new NethereumAccountFactoryDeployment
            {
                EntryPoint = _entryPointService.ContractAddress
            };
            _accountFactoryService = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                _web3, accountFactoryDeployment);

            _ecdsaValidatorService = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                _web3, new ECDSAValidatorDeployment());
        }

        private void SetupBundlerService(Account bundlerAccount)
        {
            var bundlerWeb3 = new Web3.Web3(bundlerAccount, _rpcClient);
            bundlerWeb3.TransactionManager.UseLegacyAsDefault = true;

            var bundlerConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _entryPointService.ContractAddress },
                BeneficiaryAddress = bundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                UnsafeMode = true,
                ChainId = CHAIN_ID
            };

            _bundlerService?.Dispose();
            _bundlerService = new BundlerService(bundlerWeb3, bundlerConfig);
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Policy")]
        public async Task Given_OpenAccessPolicy_When_AnyBundlerSubmits_Then_TransactionAccepted()
        {
            await SetupWithPolicyAsync(PolicyConfig.OpenAccess);

            var smartAccountAddress = await CreateAndSetupSmartAccountAsync(_userAccount);

            var callData = CreateERC7579ExecuteCallData(
                _operatorAccount.Address,
                Web3.Web3.Convert.ToWei(0.01m),
                Array.Empty<byte>());

            var nonce = await _entryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);

            var userOp = CreateUserOp(smartAccountAddress, nonce, callData);

            var packedUserOp = SignUserOperation(userOp, _userAccount);

            await _bundlerService.SendUserOperationAsync(packedUserOp, _entryPointService.ContractAddress);

            var bundleResult = await _bundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Open access policy should allow any bundler: {bundleResult.Error}");
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Policy")]
        public async Task Given_RestrictedPolicy_When_AuthorizedBundlerSubmits_Then_TransactionAccepted()
        {
            var allowedWriters = new List<string>
            {
                _operatorAccount.Address,
                _bundlerAccount.Address,
                _userAccount.Address
            };
            await SetupWithPolicyAsync(PolicyConfig.RestrictedAccess(allowedWriters));

            var smartAccountAddress = await CreateAndSetupSmartAccountAsync(_userAccount);

            var callData = CreateERC7579ExecuteCallData(
                _operatorAccount.Address,
                Web3.Web3.Convert.ToWei(0.01m),
                Array.Empty<byte>());

            var nonce = await _entryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);

            var userOp = CreateUserOp(smartAccountAddress, nonce, callData);

            var packedUserOp = SignUserOperation(userOp, _userAccount);

            await _bundlerService.SendUserOperationAsync(packedUserOp, _entryPointService.ContractAddress);

            var bundleResult = await _bundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Authorized bundler should be allowed: {bundleResult.Error}");
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Policy")]
        public async Task Given_RestrictedPolicy_When_UnauthorizedBundlerSubmits_Then_TransactionRejected()
        {
            var allowedWriters = new List<string>
            {
                _operatorAccount.Address,
                _userAccount.Address
            };
            await SetupWithPolicyAsync(PolicyConfig.RestrictedAccess(allowedWriters));

            SetupBundlerService(_unauthorizedBundlerAccount);

            var smartAccountAddress = await CreateAndSetupSmartAccountAsync(_userAccount);

            var callData = CreateERC7579ExecuteCallData(
                _operatorAccount.Address,
                Web3.Web3.Convert.ToWei(0.01m),
                Array.Empty<byte>());

            var nonce = await _entryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);

            var userOp = CreateUserOp(smartAccountAddress, nonce, callData);

            var packedUserOp = SignUserOperation(userOp, _userAccount);

            await _bundlerService.SendUserOperationAsync(packedUserOp, _entryPointService.ContractAddress);

            var bundleResult = await _bundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.False(bundleResult.Success, "Unauthorized bundler should be rejected by policy");
            Assert.Contains("not in the allowed writers", bundleResult.Error ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "AppChain-AA-Policy")]
        public async Task Given_CalldataSizeLimit_When_LargeUserOpSubmitted_Then_TransactionRejected()
        {
            var policyConfig = new PolicyConfig
            {
                Enabled = true,
                MaxCalldataBytes = 50000,
                AllowedWriters = null
            };
            await SetupWithPolicyAsync(policyConfig);

            var smartAccountAddress = await CreateAndSetupSmartAccountAsync(_userAccount);

            var largeData = new byte[100000];
            Array.Fill(largeData, (byte)0xAB);

            var callData = CreateERC7579ExecuteCallData(
                _operatorAccount.Address,
                BigInteger.Zero,
                largeData);

            var nonce = await _entryPointService.GetNonceQueryAsync(smartAccountAddress, BigInteger.Zero);

            var userOp = CreateUserOp(smartAccountAddress, nonce, callData);
            // The 100KB calldata pushes the EIP-7623 preVerificationGas floor to ~3.5M; fund above it
            userOp.PreVerificationGas = 4_000_000;

            var packedUserOp = SignUserOperation(userOp, _userAccount);

            await _bundlerService.SendUserOperationAsync(packedUserOp, _entryPointService.ContractAddress);

            var bundleResult = await _bundlerService.ExecuteBundleAsync();

            Assert.NotNull(bundleResult);
            Assert.False(bundleResult.Success, "Large calldata should be rejected by policy");
            Assert.Contains("exceeds maximum", bundleResult.Error ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> CreateAndSetupSmartAccountAsync(Account userAccount)
        {
            var salt = new byte[32];
            new Random().NextBytes(salt);
            var initData = EncodeInitData(userAccount.Address);

            var smartAccountAddress = await _accountFactoryService.GetAddressQueryAsync(
                new GetAddressFunction
                {
                    Salt = salt,
                    InitData = initData
                });

            await _appChain.State.SaveAccountAsync(smartAccountAddress, new Nethereum.Model.Account
            {
                Balance = Web3.Web3.Convert.ToWei(10),
                Nonce = 0
            });

            var createAccountFunction = new CreateAccountFunction
            {
                Salt = salt,
                InitData = initData
            };
            await _accountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(createAccountFunction);

            await _entryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = smartAccountAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(1)
                });

            await _accountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(smartAccountAddress);

            return smartAccountAddress;
        }

        private static UserOperation CreateUserOp(string sender, BigInteger nonce, byte[] callData)
        {
            return new UserOperation
            {
                Sender = sender,
                Nonce = nonce,
                InitCode = Array.Empty<byte>(),
                CallData = callData,
                CallGasLimit = 100000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000,
                Paymaster = AddressUtil.ZERO_ADDRESS,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 0,
                PaymasterPostOpGasLimit = 0,
                Signature = Array.Empty<byte>()
            };
        }

        private PackedUserOperation SignUserOperation(UserOperation userOp, Account signerAccount)
        {
            var signerKey = new EthECKey(signerAccount.PrivateKey);
            var packedUserOp = UserOperationBuilder.PackAndSignEIP712UserOperation(
                userOp,
                _entryPointService.ContractAddress,
                CHAIN_ID,
                signerKey);
            packedUserOp.Signature = EcdsaValidatorModule.ApplySignaturePrefix(_ecdsaValidatorService.ContractAddress, packedUserOp.Signature);
            return packedUserOp;
        }
    }

    internal class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
