using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.AppChain;
using Nethereum.AppChain.Sequencer;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;
using AppChainSequencer = Nethereum.AppChain.Sequencer.Sequencer;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Sequencer
{
    public class SequencerSimpleAccountTests : IAsyncLifetime
    {
        private const int CHAIN_ID = 420422;

        private AppChainCore _appChain = null!;
        private AppChainSequencer _sequencer = null!;
        private AppChainNode _node = null!;
        private IWeb3 _web3 = null!;
        private BundlerService _bundlerService = null!;
        private AppChainRpcClient _rpcClient = null!;

        private EntryPointService _entryPointService = null!;
        private SimpleAccountFactoryService _accountFactoryService = null!;

        private Account _operatorAccount = null!;
        private Account _bundlerAccount = null!;
        private Account _userAccount = null!;

        public async Task InitializeAsync()
        {
            var operatorPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
            _operatorAccount = new Account(operatorPrivateKey, CHAIN_ID);
            _bundlerAccount = new Account("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a", CHAIN_ID);
            _userAccount = new Account("0x7c852118294e51e653712a81e05800f419141751be58f605c371e15141b007a6", CHAIN_ID);

            var blockStore = new InMemoryBlockStore();
            var transactionStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var stateStore = new InMemoryStateStore();

            var appChainConfig = AppChainConfig.CreateWithName("SimpleAccountTest", CHAIN_ID);
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
                _userAccount.Address
            };

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = prefundedAddresses,
                PrefundBalance = Web3.Web3.Convert.ToWei(1000),
                DeployCreate2Factory = true
            };
            await _appChain.InitializeAsync(genesisOptions);

            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _operatorAccount.Address,
                SequencerPrivateKey = _operatorAccount.PrivateKey,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 1000,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = PolicyConfig.OpenAccess
            };

            _sequencer = new AppChainSequencer(_appChain, sequencerConfig);
            await _sequencer.StartAsync();

            _node = new AppChainNode(_appChain, _sequencer);

            _rpcClient = new AppChainRpcClient(_node, CHAIN_ID);
            _web3 = new Web3.Web3(_operatorAccount, _rpcClient);
            _web3.TransactionManager.UseLegacyAsDefault = true;

            await DeployAAContractsAsync();
            SetupBundlerService();
        }

        public async Task DisposeAsync()
        {
            _bundlerService?.Dispose();
            if (_sequencer != null)
            {
                await _sequencer.StopAsync();
            }
        }

        private async Task DeployAAContractsAsync()
        {
            _entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                _web3, new EntryPointDeployment());

            var factoryDeployment = new SimpleAccountFactoryDeployment
            {
                EntryPoint = _entryPointService.ContractAddress
            };

            _accountFactoryService = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(
                _web3, factoryDeployment);
        }

        private void SetupBundlerService()
        {
            var bundlerWeb3 = new Web3.Web3(_bundlerAccount, _rpcClient);
            bundlerWeb3.TransactionManager.UseLegacyAsDefault = true;

            var bundlerConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _entryPointService.ContractAddress },
                BeneficiaryAddress = _bundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                SimulateValidation = false,
                UnsafeMode = true,
                ChainId = CHAIN_ID
            };

            _bundlerService = new BundlerService(bundlerWeb3, bundlerConfig);
        }

        [Fact]
        [Trait("Category", "AppChain-AA-SimpleAccount")]
        public async Task Given_SequencerWithSimpleAccount_When_SenderCreatorDeployed_Then_HasCode()
        {
            // Verify SenderCreator is deployed (critical for ERC-4337 v0.7 account creation)
            var epSenderCreator = await _entryPointService.SenderCreatorQueryAsync();
            Assert.NotNull(epSenderCreator);
            Assert.NotEqual(AddressUtil.ZERO_ADDRESS, epSenderCreator);

            var senderCreatorCode = await _web3.Eth.GetCode.SendRequestAsync(epSenderCreator);
            Assert.True(!string.IsNullOrEmpty(senderCreatorCode) && senderCreatorCode.Length > 2,
                $"SenderCreator at {epSenderCreator} should have code, got: {senderCreatorCode}");

            // Verify factory's expected SenderCreator matches EntryPoint's
            var factorySenderCreator = await _accountFactoryService.SenderCreatorQueryAsync();
            Assert.Equal(epSenderCreator.ToLower(), factorySenderCreator.ToLower());
        }

        [Fact]
        [Trait("Category", "AppChain-AA-SimpleAccount")]
        public async Task Given_Sequencer_When_QueryChainId_Then_MatchesExpected()
        {
            // Verify chain ID is correct for signature verification
            var reportedChainId = await _web3.Eth.ChainId.SendRequestAsync();
            Assert.Equal(CHAIN_ID, (int)reportedChainId.Value);
        }

        [Fact]
        [Trait("Category", "AppChain-AA-SimpleAccount")]
        public async Task Given_SequencerWithSimpleAccount_When_FactoryCalledDirectly_Then_RejectedBySenderCreatorProtection()
        {
            // ERC-4337 v0.7 SimpleAccountFactory only allows createAccount to be called from SenderCreator
            // Direct calls should be rejected - this tests the security protection is working
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            ulong salt = 99999;

            // Get counterfactual address
            var smartAccountAddress = await _accountFactoryService.GetAddressQueryAsync(ownerAddress, salt);

            // Verify no code at address yet
            var codeBefore = await _web3.Eth.GetCode.SendRequestAsync(smartAccountAddress);
            Assert.True(string.IsNullOrEmpty(codeBefore) || codeBefore == "0x", "Account should not exist yet");

            // Attempt to call factory directly - this should fail
            var createAccountFunction = new CreateAccountFunction
            {
                Owner = ownerAddress,
                Salt = salt
            };

            var exception = await Assert.ThrowsAsync<Nethereum.ABI.FunctionEncoding.SmartContractRevertException>(
                async () => await _accountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(createAccountFunction));

            // Verify the correct protection error
            Assert.Contains("only callable from SenderCreator", exception.Message);

            // Verify account was NOT deployed (protection worked)
            var codeAfter = await _web3.Eth.GetCode.SendRequestAsync(smartAccountAddress);
            Assert.True(string.IsNullOrEmpty(codeAfter) || codeAfter == "0x",
                "Account should NOT be deployed when calling factory directly");
        }

        [Fact]
        [Trait("Category", "AppChain-AA-SimpleAccount")]
        public async Task Given_SequencerWithSimpleAccount_When_HandleOpsCalled_Then_TransactionSucceeds()
        {
            var recipient = _userAccount;
            var transferAmount = Web3.Web3.Convert.ToWei(0.01m);

            // First verify SenderCreator is deployed
            var epSenderCreator = await _entryPointService.SenderCreatorQueryAsync();
            var senderCreatorCode = await _web3.Eth.GetCode.SendRequestAsync(epSenderCreator);
            Assert.True(!string.IsNullOrEmpty(senderCreatorCode) && senderCreatorCode.Length > 2,
                $"SenderCreator at {epSenderCreator} should have code");

            // Create SimpleAccount using the same approach as working tests
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            ulong salt = 12345;

            // Get counterfactual address
            var smartAccountAddress = await _accountFactoryService.GetAddressQueryAsync(ownerAddress, salt);

            // Fund the smart account address via ETH transfer (same as working tests)
            await _web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(smartAccountAddress, 1m);

            // Build initCode: factory address + createAccount calldata
            var initCode = _accountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            // Create UserOperation with explicitly set values (same approach as working DevChain tests)
            var userOp = new UserOperation
            {
                Sender = smartAccountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedDeployOp = await _entryPointService.SignAndInitialiseUserOperationAsync(userOp, ownerKey);

            // Deploy account via handleOps directly (simpler than bundler for debugging)
            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = new List<Nethereum.AccountAbstraction.Structs.PackedUserOperation> { packedDeployOp },
                Beneficiary = _bundlerAccount.Address,
                Gas = 5000000
            };

            var deployReceipt = await _entryPointService.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

            Assert.NotNull(deployReceipt);
            Assert.True(deployReceipt.Status?.Value == 1,
                $"Account deployment failed with status: {deployReceipt.Status?.Value}, tx: {deployReceipt.TransactionHash}");

            // Verify account is deployed
            var code = await _web3.Eth.GetCode.SendRequestAsync(smartAccountAddress);
            Assert.True(!string.IsNullOrEmpty(code) && code.Length > 2, "Account should have code deployed");

            // Deposit to EntryPoint for gas sponsorship
            await _entryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = smartAccountAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(1)
                });

            var recipientBalanceBefore = await _web3.Eth.GetBalance.SendRequestAsync(recipient.Address);

            // Create UserOperation for transfer
            var executeFunction = new ExecuteFunction
            {
                Target = recipient.Address,
                Value = transferAmount,
                Data = Array.Empty<byte>()
            };

            var packedUserOp = await _entryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = smartAccountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 200000,
                    VerificationGasLimit = 200000
                },
                ownerKey);

            // Execute via handleOps
            var executeOpsFunction = new HandleOpsFunction
            {
                Ops = new List<Nethereum.AccountAbstraction.Structs.PackedUserOperation> { packedUserOp },
                Beneficiary = _bundlerAccount.Address,
                Gas = 5000000
            };

            var executeReceipt = await _entryPointService.HandleOpsRequestAndWaitForReceiptAsync(executeOpsFunction);

            Assert.NotNull(executeReceipt);
            Assert.True(executeReceipt.Status?.Value == 1,
                $"Bundle execution failed with status: {executeReceipt.Status?.Value}, tx: {executeReceipt.TransactionHash}");

            var recipientBalanceAfter = await _web3.Eth.GetBalance.SendRequestAsync(recipient.Address);
            Assert.Equal(recipientBalanceBefore.Value + transferAmount, recipientBalanceAfter.Value);
        }
    }
}
