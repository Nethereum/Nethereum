using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures
{
    [CollectionDefinition(DevChainBundlerFixture.COLLECTION_NAME)]
    public class DevChainBundlerCollection : ICollectionFixture<DevChainBundlerFixture> { }

    /// <summary>
    /// Test fixture that provides a DevChain node with 4337 infrastructure (EntryPoint, AccountFactory, Bundler).
    /// Uses in-memory storage by default. Can be extended for RocksDB testing.
    /// </summary>
    public class DevChainBundlerFixture : IAsyncLifetime
    {
        public const string COLLECTION_NAME = "DevChainBundler";
        public const int CHAIN_ID = 31337;

        public DevChainNode Node { get; private set; } = null!;
        public IWeb3 Web3 { get; private set; } = null!;
        public BundlerService BundlerService { get; private set; } = null!;

        public EntryPointService EntryPointService { get; private set; } = null!;
        public SimpleAccountFactoryService AccountFactoryService { get; private set; } = null!;

        public Web3Account OperatorAccount { get; private set; } = null!;
        public Web3Account BundlerAccount { get; private set; } = null!;

        public string OperatorPrivateKey => "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        public string BundlerPrivateKey => "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";

        private readonly Transaction7702Signer _signer7702 = new();
        private int _bundlerResetCount = 0;

        public async Task InitializeAsync()
        {
            OperatorAccount = new Web3Account(OperatorPrivateKey, CHAIN_ID);
            BundlerAccount = new Web3Account(BundlerPrivateKey, CHAIN_ID);

            var config = new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            Node = new DevChainNode(config);

            var prefundedAddresses = new[] { OperatorAccount.Address, BundlerAccount.Address };
            await Node.StartAsync(prefundedAddresses, Nethereum.Web3.Web3.Convert.ToWei(10000));

            Web3 = Node.CreateWeb3(OperatorAccount);

            await DeployContractsAsync();
            SetupBundlerService();
        }

        private async Task DeployContractsAsync()
        {
            EntryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                Web3, new EntryPointDeployment());

            var factoryDeployment = new SimpleAccountFactoryDeployment
            {
                EntryPoint = EntryPointService.ContractAddress
            };

            AccountFactoryService = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(
                Web3, factoryDeployment);
        }

        private void SetupBundlerService()
        {
            var bundlerWeb3 = Node.CreateWeb3(BundlerAccount);

            var bundlerConfig = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPointService.ContractAddress },
                BeneficiaryAddress = BundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                SimulateValidation = false,
                UnsafeMode = true,
                ChainId = CHAIN_ID
            };

            BundlerService = new BundlerService(bundlerWeb3, bundlerConfig);
        }

        public Task DisposeAsync()
        {
            BundlerService?.Dispose();
            Node?.Dispose();
            return Task.CompletedTask;
        }

        #region Account Creation Helpers

        public async Task<(string accountAddress, EthECKey accountKey)> CreateFundedAccountAsync(
            ulong salt,
            decimal ethAmount = 1m)
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();

            var result = await AccountFactoryService.CreateAndDeployAccountAsync(
                ownerAddress,
                ownerAddress,
                EntryPointService.ContractAddress,
                accountKey,
                ethAmount,
                salt);

            return (result.AccountAddress, accountKey);
        }

        public async Task<string> GetAccountAddressAsync(string owner, ulong salt)
        {
            return await AccountFactoryService.GetAddressQueryAsync(owner, salt);
        }

        public async Task FundAccountAsync(string address, decimal ethAmount)
        {
            await Node.SetBalanceAsync(address, Nethereum.Web3.Web3.Convert.ToWei(ethAmount));
        }

        #endregion

        #region EIP-7702 Helpers

        public (EthECKey key, string address) GenerateNewAccount()
        {
            var key = EthECKey.GenerateKey();
            return (key, key.GetPublicAddress());
        }

        public async Task<string> SetupEIP7702DelegatedEOAAsync(
            EthECKey authorityKey,
            string delegateAddress,
            decimal fundAmount = 1m)
        {
            var authorityAddress = authorityKey.GetPublicAddress();
            await FundAccountAsync(authorityAddress, fundAmount);

            var auth = SignAuthorization(authorityKey, delegateAddress, 0);
            var signedTx = CreateType4Transaction(
                await Node.GetNonceAsync(OperatorAccount.Address),
                authorityAddress,
                new List<Authorisation7702Signed> { auth });

            var result = await Node.SendTransactionAsync(signedTx);
            if (!result.Success)
                throw new Exception($"EIP-7702 delegation failed: {result.RevertReason ?? "Unknown error"} (GasUsed: {result.GasUsed})");

            return authorityAddress;
        }

        public Authorisation7702Signed SignAuthorization(
            EthECKey authorityKey,
            string delegateAddress,
            BigInteger nonce,
            BigInteger? chainId = null)
        {
            var auth = new Authorisation7702
            {
                ChainId = chainId ?? CHAIN_ID,
                Address = delegateAddress,
                Nonce = nonce
            };
            var authSigner = new Authorisation7702Signer();
            return authSigner.SignAuthorisation(authorityKey, auth);
        }

        public ISignedTransaction CreateType4Transaction(
            BigInteger senderNonce,
            string receiverAddress,
            List<Authorisation7702Signed> authList,
            BigInteger? gasLimit = null,
            byte[] data = null,
            BigInteger? value = null)
        {
            var tx7702 = new Transaction7702(
                chainId: CHAIN_ID,
                nonce: senderNonce,
                maxPriorityFeePerGas: 1_000_000_000,
                maxFeePerGas: 2_000_000_000,
                gasLimit: gasLimit ?? 200_000,
                receiverAddress: receiverAddress,
                amount: value ?? 0,
                data: data?.ToHex() ?? "",
                accessList: null,
                authorisationList: authList);

            var signedTxHex = _signer7702.SignTransaction(OperatorPrivateKey.Substring(2), tx7702);
            return TransactionFactory.CreateTransaction(signedTxHex);
        }

        #endregion

        #region UserOperation Helpers

        public async Task<PackedUserOperation> CreateAndSignUserOperationAsync(
            string sender,
            EthECKey signerKey,
            byte[] callData = null,
            byte[] initCode = null)
        {
            var userOp = new UserOperation
            {
                Sender = sender,
                CallData = callData ?? Array.Empty<byte>(),
                InitCode = initCode,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            return await EntryPointService.SignAndInitialiseUserOperationAsync(userOp, signerKey);
        }

        public async Task<UserOperationGasEstimate> EstimateUserOperationGasAsync(UserOperation userOp)
        {
            return await BundlerService.EstimateUserOperationGasAsync(
                userOp,
                EntryPointService.ContractAddress);
        }

        public async Task<string> SendUserOperationAsync(PackedUserOperation packedOp)
        {
            return await BundlerService.SendUserOperationAsync(
                packedOp,
                EntryPointService.ContractAddress);
        }

        public async Task<BundleExecutionResult?> ExecuteBundleAsync()
        {
            return await BundlerService.ExecuteBundleAsync();
        }

        #endregion

        #region Bundler Management

        public BundlerService CreateNewBundlerService(BundlerConfig config = null)
        {
            var bundlerWeb3 = Node.CreateWeb3(BundlerAccount);

            var bundlerConfig = config ?? new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPointService.ContractAddress },
                BeneficiaryAddress = BundlerAccount.Address,
                MaxBundleSize = 10,
                MaxMempoolSize = 100,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                SimulateValidation = false,
                UnsafeMode = true,
                ChainId = CHAIN_ID
            };

            return new BundlerService(bundlerWeb3, bundlerConfig);
        }

        public async Task ResetBundlerServiceAsync()
        {
            BundlerService?.Dispose();

            _bundlerResetCount++;
            var newBundlerKey = EthECKey.GenerateKey();
            BundlerAccount = new Web3Account(newBundlerKey, CHAIN_ID);

            await FundAccountAsync(BundlerAccount.Address, 100m);
            SetupBundlerService();
        }

        #endregion

        #region State Management

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            return await Node.GetBalanceAsync(address);
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            return await Node.GetCodeAsync(address);
        }

        public async Task<BigInteger> GetNonceAsync(string address)
        {
            return await Node.GetNonceAsync(address);
        }

        public async Task MineBlockAsync()
        {
            await Node.MineBlockAsync();
        }

        #endregion
    }
}
