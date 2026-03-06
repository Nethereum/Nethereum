using System;
using System.Collections.Generic;
using System.Numerics;
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
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AppChain;

using NethereumAccountExecuteFunction = Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition.ExecuteFunction;
using Nethereum.AppChain.Sequencer;
using Nethereum.Contracts;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;
using AppChainSequencer = Nethereum.AppChain.Sequencer.Sequencer;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Sequencer
{
    public class SequencerPolicyAATests : IAsyncLifetime
    {
        private const int CHAIN_ID = 420421;

        private AppChainCore _appChain = null!;
        private AppChainSequencer _sequencer = null!;
        private AppChainNode _node = null!;
        private IWeb3 _web3 = null!;
        private BundlerService _bundlerService = null!;
        private AppChainRpcClient _rpcClient = null!;

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
            return ByteUtil.Merge(
                _ecdsaValidatorService.ContractAddress.HexToByteArray(),
                ownerAddress.HexToByteArray());
        }

        private byte[] CreateERC7579ExecuteCallData(string target, BigInteger value, byte[] data)
        {
            var mode = ERC7579ModeLib.EncodeSingleDefault();
            var executionCalldata = ERC7579ExecutionLib.EncodeSingle(target, value, data);
            var executeFunction = new NethereumAccountExecuteFunction
            {
                Mode = mode,
                ExecutionCalldata = executionCalldata
            };
            return executeFunction.GetCallData();
        }

        private byte[] PrefixSignatureWithValidator(byte[] signature)
        {
            return ByteUtil.Merge(
                _ecdsaValidatorService.ContractAddress.HexToByteArray(),
                signature);
        }

        public async Task InitializeAsync()
        {
            var operatorPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
            _operatorAccount = new Account(operatorPrivateKey, CHAIN_ID);
            _bundlerAccount = new Account("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a", CHAIN_ID);
            _userAccount = new Account("0x7c852118294e51e653712a81e05800f419141751be58f605c371e15141b007a6", CHAIN_ID);
            _unauthorizedBundlerAccount = new Account("0x47e179ec197488593b187f80a00eb0da91f1b9d0b13f8733639f19c30a34926a", CHAIN_ID);

            var blockStore = new InMemoryBlockStore();
            var transactionStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var stateStore = new InMemoryStateStore();

            var appChainConfig = AppChainConfig.CreateWithName("PolicyAATest", CHAIN_ID);
            appChainConfig.SequencerAddress = _operatorAccount.Address;
            appChainConfig.BaseFee = 1_000_000_000;
            appChainConfig.BlockGasLimit = 30_000_000;

            _appChain = new AppChainCore(
                appChainConfig,
                blockStore,
                transactionStore,
                receiptStore,
                logStore,
                stateStore);

            var prefundedAddresses = new[]
            {
                _operatorAccount.Address,
                _bundlerAccount.Address,
                _userAccount.Address,
                _unauthorizedBundlerAccount.Address
            };

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = prefundedAddresses,
                PrefundBalance = Web3.Web3.Convert.ToWei(1000),
                DeployCreate2Factory = true
            };
            await _appChain.InitializeAsync(genesisOptions);
        }

        public async Task DisposeAsync()
        {
            _bundlerService?.Dispose();
            if (_sequencer != null)
            {
                await _sequencer.StopAsync();
            }
        }

        private async Task SetupWithPolicyAsync(PolicyConfig policyConfig)
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _operatorAccount.Address,
                SequencerPrivateKey = _operatorAccount.PrivateKey,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 1000,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = policyConfig
            };

            _sequencer = new AppChainSequencer(_appChain, sequencerConfig);
            await _sequencer.StartAsync();

            _node = new AppChainNode(_appChain, _sequencer);

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
            packedUserOp.Signature = PrefixSignatureWithValidator(packedUserOp.Signature);
            return packedUserOp;
        }
    }

    internal class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
