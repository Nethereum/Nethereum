using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Sequencer
{
    public class SequencerSimpleAccountTests : IAsyncLifetime
    {
        private const int CHAIN_ID = 420422;

        private AppChainCore _appChain = null!;
        private ISequencer _sequencer = null!;
        private AppChainNode _node = null!;
        private IWeb3 _web3 = null!;
        private BundlerService _bundlerService = null!;
        private AppChainRpcClient _rpcClient = null!;
        private AppChainComposedNode? _composed;
        private bool _signRecoverableBeforeCompose;

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

            var config = new AppChainServerConfig
            {
                ChainId = CHAIN_ID,
                ChainName = "SimpleAccountTest",
                BaseFee = 1_000_000_000
            };
            config.Genesis.Owner.PrivateKey = operatorPrivateKey;
            config.Consensus.Sequencer.PrivateKey = operatorPrivateKey;
            config.Consensus.BlockProductionMode = BlockProductionMode.OnDemand;
            config.Consensus.BlockTimeMs = 0;
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
            foreach (var address in new[] { _bundlerAccount.Address, _userAccount.Address })
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
            SetupBundlerService();
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

            var factorySenderCreator = await _accountFactoryService.SenderCreatorQueryAsync();
            Assert.Equal(epSenderCreator.ToLower(), factorySenderCreator.ToLower());
        }

        [Fact]
        [Trait("Category", "AppChain-AA-SimpleAccount")]
        public async Task Given_Sequencer_When_QueryChainId_Then_MatchesExpected()
        {
            var reportedChainId = await _web3.Eth.ChainId.SendRequestAsync();
            Assert.Equal(CHAIN_ID, (int)reportedChainId.Value);
        }

        [Fact]
        [Trait("Category", "AppChain-AA-SimpleAccount")]
        public async Task Given_SequencerWithSimpleAccount_When_FactoryCalledDirectly_Then_RejectedBySenderCreatorProtection()
        {
            // ERC-4337 v0.7 SimpleAccountFactory only allows createAccount to be called from SenderCreator
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            ulong salt = 99999;

            var smartAccountAddress = await _accountFactoryService.GetAddressQueryAsync(ownerAddress, salt);

            var codeBefore = await _web3.Eth.GetCode.SendRequestAsync(smartAccountAddress);
            Assert.True(string.IsNullOrEmpty(codeBefore) || codeBefore == "0x", "Account should not exist yet");

            var createAccountFunction = new CreateAccountFunction
            {
                Owner = ownerAddress,
                Salt = salt
            };

            var exception = await Assert.ThrowsAsync<Nethereum.Contracts.SmartContractCustomErrorRevertException>(
                async () => await _accountFactoryService.CreateAccountRequestAndWaitForReceiptAsync(createAccountFunction));

            Assert.True(exception.IsCustomErrorFor<Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition.NotSenderCreatorError>());

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

            var epSenderCreator = await _entryPointService.SenderCreatorQueryAsync();
            var senderCreatorCode = await _web3.Eth.GetCode.SendRequestAsync(epSenderCreator);
            Assert.True(!string.IsNullOrEmpty(senderCreatorCode) && senderCreatorCode.Length > 2,
                $"SenderCreator at {epSenderCreator} should have code");

            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            ulong salt = 12345;

            var smartAccountAddress = await _accountFactoryService.GetAddressQueryAsync(ownerAddress, salt);

            await _web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(smartAccountAddress, 1m);

            var initCode = _accountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

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

            var code = await _web3.Eth.GetCode.SendRequestAsync(smartAccountAddress);
            Assert.True(!string.IsNullOrEmpty(code) && code.Length > 2, "Account should have code deployed");

            await _entryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = smartAccountAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(1)
                });

            var recipientBalanceBefore = await _web3.Eth.GetBalance.SendRequestAsync(recipient.Address);

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
