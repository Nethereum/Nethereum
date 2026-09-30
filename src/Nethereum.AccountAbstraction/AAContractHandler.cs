using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.Factory;
using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.AccountSigning;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.Accounts.AccountMessageSigning;
using Nethereum.AccountAbstraction.Execution;
using Nethereum.AccountAbstraction.Paymasters;
using Nethereum.AccountAbstraction.Signing;
using PackedUserOperation = Nethereum.AccountAbstraction.Structs.PackedUserOperation;

using Nethereum.Documentation;
namespace Nethereum.AccountAbstraction
{
    [NethereumDocExample(DocSection.AccountAbstraction, "account-abstraction", "AAContractHandler - the ContractHandler that sends UserOperations")]
    public class AAContractHandler : ContractHandler
    {
        private readonly string _accountAddress;
        private readonly IAccountSigningService _signingService;
        private readonly IErc7579ValidatorModule? _validator;
        private readonly IEip7702AuthSigner _eip7702AuthSigner;
        private readonly IAccountAbstractionBundlerService _bundlerService;
        private readonly string _entryPointAddress;
        private BigInteger? _chainId;

        private static readonly string ESTIMATION_DUMMY_SIGNATURE = "0xfffffffffffffffffffffffffffffff0000000000000000000000000000000007aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1c";

        private static readonly string ESTIMATION_DUMMY_PAYMASTER_DATA =
            VerifyingPaymasterManager.BuildVerifyingPaymasterData(
                validUntil: 0xFFFFFFFFFFFFUL,
                validAfter: 0UL,
                signature: ESTIMATION_DUMMY_SIGNATURE.HexToByteArray()).ToHex(true);

        private IAccountInitCodeBuilder _initCodeBuilder;

        private IExecuteEncoder _executeEncoder = new SimpleAccountExecuteEncoder();

        public FactoryConfig FactoryConfig { get; private set; }
        public PaymasterConfig PaymasterConfig { get; private set; }
        public Eip7702DelegationConfig Eip7702DelegationConfig { get; private set; }
        public AAGasConfig GasConfig { get; private set; } = AAGasConfig.Default;

        private Authorisation _pendingEip7702Auth;

        public string AccountAddress => _accountAddress;
        public string EntryPointAddress => _entryPointAddress;

        public AAContractHandler(
            string contractAddress,
            string accountAddress,
            IAccountSigningService signingService,
            IErc7579ValidatorModule? validator,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            IEthApiContractService ethApiContractService,
            string addressFrom = null)
            : base(contractAddress, ethApiContractService, addressFrom)
        {
            EntryPointAddresses.ValidateSupportedUserOpHashVersion(entryPointAddress);
            _accountAddress = accountAddress;
            _signingService = signingService;
            _validator = validator;
            _bundlerService = bundlerService;
            _entryPointAddress = entryPointAddress;
        }

        public AAContractHandler(
            string contractAddress,
            string accountAddress,
            EthECKey signerKey,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            IEthApiContractService ethApiContractService,
            string addressFrom = null)
            : this(contractAddress, accountAddress, new AccountSigningOfflineService(signerKey), null, bundlerService, entryPointAddress, ethApiContractService, addressFrom)
        {
            _eip7702AuthSigner = new PrivateKeyEip7702AuthSigner(signerKey);
        }

        public AAContractHandler(
            string contractAddress,
            string accountAddress,
            EthECKey signerKey,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            IWeb3 web3,
            string addressFrom = null)
            : this(contractAddress, accountAddress, signerKey, bundlerService, entryPointAddress, web3.Eth, addressFrom)
        {
        }

        public AAContractHandler(
            string contractAddress,
            string accountAddress,
            EthECKey signerKey,
            IErc7579ValidatorModule validator,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            IWeb3 web3,
            string addressFrom = null)
            : this(contractAddress, accountAddress, new AccountSigningOfflineService(signerKey), validator, bundlerService, entryPointAddress, web3.Eth, addressFrom)
        {
            _eip7702AuthSigner = new PrivateKeyEip7702AuthSigner(signerKey);
        }

        internal static AAContractHandler CreateFromContractHandler(
            ContractHandler existingHandler,
            string accountAddress,
            EthECKey signerKey,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress)
        {
            return new AAContractHandler(
                existingHandler.ContractAddress,
                accountAddress,
                signerKey,
                bundlerService,
                entryPointAddress,
                existingHandler.EthApiContractService,
                existingHandler.AddressFrom);
        }

        internal static AAContractHandler CreateFromExistingContractService<T>(
            T service,
            string accountAddress,
            IAccountSigningService signingService,
            IErc7579ValidatorModule? validator,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress) where T : ContractWeb3ServiceBase
        {
            return new AAContractHandler(
                service.ContractAddress,
                accountAddress,
                signingService,
                validator,
                bundlerService,
                entryPointAddress,
                service.Web3.Eth,
                service.ContractHandler.AddressFrom);
        }

        internal static AAContractHandler CreateFromExistingContractService<T>(
            T service,
            string accountAddress,
            EthECKey signerKey,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress) where T : ContractWeb3ServiceBase
        {
            return new AAContractHandler(
                service.ContractAddress,
                accountAddress,
                signerKey,
                bundlerService,
                entryPointAddress,
                service.Web3,
                service.ContractHandler.AddressFrom);
        }

        internal static AAContractHandler CreateFromExistingContractService<T>(
            T service,
            IAccount account,
            IErc7579ValidatorModule? validator,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress) where T : ContractWeb3ServiceBase
        {
            var (signingService, resolvedValidator) = SignerFromAccount(account, validator);
            return CreateFromExistingContractService(
                service,
                account.Address,
                signingService,
                resolvedValidator,
                bundlerService,
                entryPointAddress);
        }

        internal static (IAccountSigningService signingService, IErc7579ValidatorModule? validator) SignerFromAccount(IAccount account, IErc7579ValidatorModule? validator)
        {
            if (account.AccountSigningService == null)
                throw new ArgumentException(
                    "the account has no signing service; an AA account must carry one", nameof(account));

            return (account.AccountSigningService, validator);
        }

        public AAContractHandler WithFactory(FactoryConfig config)
        {
            FactoryConfig = config;
            _initCodeBuilder = config != null ? new SimpleAccountInitCodeBuilder(config) : null;
            return this;
        }

        public AAContractHandler WithFactory(IAccountInitCodeBuilder initCodeBuilder)
        {
            _initCodeBuilder = initCodeBuilder;
            return this;
        }

        public AAContractHandler WithPaymaster(string paymasterAddress, byte[] paymasterData = null)
        {
            PaymasterConfig = new PaymasterConfig(paymasterAddress, paymasterData);
            return this;
        }

        public AAContractHandler WithPaymaster(PaymasterConfig config)
        {
            PaymasterConfig = config;
            return this;
        }

        /// <summary>
        /// Sends the userOp from a plain EOA that is delegated to <paramref name="delegateAddress"/> in
        /// the same bundle via EIP-7702 - "your existing EOA becomes smart". The handler's account (and
        /// signer) must be that EOA: it is both the userOp sender and the 7702 authority, matching the
        /// bundler's authority==sender rule. Optionally runs a post-delegation initializer via
        /// <paramref name="factoryData"/> (packed as the 0x7702 marker + factoryData).
        /// </summary>
        public AAContractHandler WithEip7702Delegation(string delegateAddress, byte[] factoryData = null)
        {
            Eip7702DelegationConfig = new Eip7702DelegationConfig(delegateAddress, factoryData);
            return this;
        }

        public AAContractHandler WithGasConfig(AAGasConfig config)
        {
            GasConfig = config ?? AAGasConfig.Default;
            return this;
        }

        public AAContractHandler WithExecuteEncoder(IExecuteEncoder executeEncoder)
        {
            _executeEncoder = executeEncoder ?? new SimpleAccountExecuteEncoder();
            return this;
        }

        public AAContractHandler WithErc7579Execution()
        {
            return WithExecuteEncoder(new Erc7579ExecuteEncoder());
        }

#if !DOTNET35

        private async Task<BigInteger> GetChainIdAsync()
        {
            if (_chainId == null)
            {
                if (EthApiContractService.TransactionManager.ChainId != null)
                    _chainId = EthApiContractService.TransactionManager.ChainId;
                else
                    _chainId = await EthApiContractService.ChainId.SendRequestAsync();
            }
            return _chainId.Value;
        }

        public override async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null, CancellationTokenSource tokenSource = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(transactionMessage);

            var packedOp = await CreateUserOperationAsync(transactionMessage);
            return await SendAndWaitForReceiptAsync(packedOp, tokenSource?.Token);
        }

        public override async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage, CancellationToken cancellationToken)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(transactionMessage);

            var packedOp = await CreateUserOperationAsync(transactionMessage);
            return await SendAndWaitForReceiptAsync(packedOp, cancellationToken);
        }

        public override async Task<string> SendRequestAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(transactionMessage);

            var packedOp = await CreateUserOperationAsync(transactionMessage);
            var rpcUserOp = UserOperationConverter.ToRpcFormat(packedOp);
            AttachPendingEip7702Auth(rpcUserOp);
            return await _bundlerService.SendUserOperation.SendRequestAsync(rpcUserOp, _entryPointAddress);
        }

        public override Task<string> SignTransactionAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
        {
            throw new NotSupportedException(
                "UserOperations have no raw transaction encoding. Use CreateUserOperationAsync to " +
                "build and sign the PackedUserOperation, and the bundler service to submit it.");
        }

        public override async Task<HexBigInteger> EstimateGasAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(transactionMessage);

            var userOp = await BuildUserOperationAsync(transactionMessage);
            var entryPointService = new EntryPointService(EthApiContractService, _entryPointAddress);
            var estimate = await EstimateUserOperationGasAsync(userOp, entryPointService);

            var totalGas = estimate.CallGasLimit.Value +
                          estimate.VerificationGasLimit.Value +
                          estimate.PreVerificationGas.Value;

            return new HexBigInteger(totalGas);
        }

        public async Task<AATransactionReceipt> BatchExecuteAsync(params BatchCall[] calls)
        {
            var callData = _executeEncoder.EncodeBatch(
                calls.Select(c => (ContractAddress, c.Value, c.CallData)).ToList());

            var packedOp = await CreateUserOperationFromCallDataAsync(callData);
            return await SendAndWaitForReceiptAsync(packedOp, null);
        }

        public async Task<AATransactionReceipt> BatchExecuteAsync(params byte[][] callDatas)
        {
            var batchCalls = callDatas.Select(data => new BatchCall(data)).ToArray();
            return await BatchExecuteAsync(batchCalls);
        }

        public async Task<AATransactionReceipt> BatchExecuteAsync<TFunctionMessage>(
            params TFunctionMessage[] functionMessages)
            where TFunctionMessage : FunctionMessage
        {
            var batchCalls = functionMessages.Select(msg => msg.ToBatchCall()).ToArray();
            return await BatchExecuteAsync(batchCalls);
        }

        public async Task<PackedUserOperation> CreateUserOperationAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            var userOp = await BuildUserOperationAsync(transactionMessage);
            return await CompleteAndSignAsync(userOp);
        }

        private static void ApplyExplicitValuesFromMessage(UserOperation userOp, FunctionMessage message)
        {
            if (message.Gas != null) userOp.CallGasLimit = message.Gas;
            if (message.Nonce != null) userOp.Nonce = message.Nonce;
            if (message.MaxFeePerGas != null) userOp.MaxFeePerGas = message.MaxFeePerGas;
            if (message.MaxPriorityFeePerGas != null) userOp.MaxPriorityFeePerGas = message.MaxPriorityFeePerGas;
            if (message.GasPrice != null)
            {
                userOp.MaxFeePerGas ??= message.GasPrice;
                userOp.MaxPriorityFeePerGas ??= message.GasPrice;
            }
        }

        private async Task<UserOperation> BuildUserOperationAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            var callData = WrapInExecuteCallData(transactionMessage);
            var userOp = await BuildUserOperationFromCallDataAsync(callData);
            ApplyExplicitValuesFromMessage(userOp, transactionMessage);
            return userOp;
        }

        private byte[] WrapInExecuteCallData<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            return _executeEncoder.EncodeExecute(
                ContractAddress,
                transactionMessage.AmountToSend,
                transactionMessage.GetCallData());
        }

        private async Task<PackedUserOperation> CreateUserOperationFromCallDataAsync(byte[] callData)
        {
            var userOp = await BuildUserOperationFromCallDataAsync(callData);
            return await CompleteAndSignAsync(userOp);
        }

        private async Task<PackedUserOperation> CompleteAndSignAsync(UserOperation userOp)
        {
            var entryPointService = new EntryPointService(EthApiContractService, _entryPointAddress);
            await FillMissingGasFromBundlerAsync(userOp, entryPointService);

            if (PaymasterConfig?.Data == null && PaymasterConfig?.DataProvider != null)
            {
                await EnsureNonceAsync(userOp, entryPointService);
                userOp.PaymasterData = await PaymasterConfig.DataProvider(userOp);
            }

            return await entryPointService.SignAndInitialiseUserOperationAsync(
                userOp, _signingService, _validator, Eip7702DelegationConfig?.DelegateAddress);
        }

        private static async Task EnsureNonceAsync(UserOperation userOp, EntryPointService entryPointService)
        {
            if (userOp.Nonce == null && !AAEIP7702Utils.IsEip7702UserOp(userOp))
                userOp.Nonce = await entryPointService.GetNonceQueryAsync(userOp.Sender, 0);
        }

        private async Task<RPC.AccountAbstraction.DTOs.UserOperationGasEstimate> EstimateUserOperationGasAsync(UserOperation userOp, EntryPointService entryPointService)
        {
            await EnsureNonceAsync(userOp, entryPointService);

            userOp.MaxPriorityFeePerGas ??= UserOperation.DEFAULT_MAX_PRIORITY_FEE_PER_GAS;
            if (userOp.MaxFeePerGas == null)
            {
                var block = await EthApiContractService.Blocks.GetBlockWithTransactionsHashesByNumber
                    .SendRequestAsync(BlockParameter.CreateLatest());
                userOp.MaxFeePerGas = (block.BaseFeePerGas?.Value ?? 0) + userOp.MaxPriorityFeePerGas.Value;
            }

            var rpcUserOp = UserOperationConverter.ToRpcFormat(userOp);
            if (string.IsNullOrEmpty(rpcUserOp.Signature) || rpcUserOp.Signature == "0x")
            {
                rpcUserOp.Signature = _validator is { } validator
                    ? validator.Address + validator.GetEstimationStubSignature().ToHex(false)
                    : ESTIMATION_DUMMY_SIGNATURE;
            }

            if (userOp.VerificationGasLimit == null) rpcUserOp.VerificationGasLimit = null;
            if (userOp.PreVerificationGas == null) rpcUserOp.PreVerificationGas = null;

            if (!string.IsNullOrEmpty(rpcUserOp.Paymaster))
            {
                if (userOp.PaymasterVerificationGasLimit == null)
                    rpcUserOp.PaymasterVerificationGasLimit = new HexBigInteger(UserOperation.DEFAULT_PAYMASTER_VERIFICATION_GAS_LIMIT);
                if (userOp.PaymasterPostOpGasLimit == null)
                    rpcUserOp.PaymasterPostOpGasLimit = new HexBigInteger(UserOperation.DEFAULT_PAYMASTER_POST_OP_GAS_LIMIT);

                if (PaymasterConfig?.Data == null && PaymasterConfig?.DataProvider != null &&
                    (userOp.PaymasterData == null || userOp.PaymasterData.Length == 0))
                {
                    rpcUserOp.PaymasterData = ESTIMATION_DUMMY_PAYMASTER_DATA;
                }
            }

            RPC.AccountAbstraction.DTOs.UserOperationGasEstimate estimate;
            try
            {
                estimate = await _bundlerService.EstimateUserOperationGas.SendRequestAsync(rpcUserOp, _entryPointAddress);
            }
            catch (RpcResponseException ex) when (TryGetCustomErrorRevertData(ex, out var revertData))
            {
                throw new SmartContractCustomErrorRevertException(revertData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Bundler gas estimation (eth_estimateUserOperationGas) failed for account {userOp.Sender} " +
                    $"on EntryPoint {_entryPointAddress}. Verify the bundler url/service configuration and that it " +
                    $"supports this EntryPoint. Bundler error: {ex.Message}", ex);
            }

            if (estimate?.CallGasLimit == null || estimate.VerificationGasLimit == null || estimate.PreVerificationGas == null)
            {
                var missing = estimate == null
                    ? "the whole estimate"
                    : estimate.CallGasLimit == null ? "callGasLimit"
                    : estimate.VerificationGasLimit == null ? "verificationGasLimit"
                    : "preVerificationGas";
                throw new InvalidOperationException(
                    $"Bundler gas estimate for account {userOp.Sender} on EntryPoint {_entryPointAddress} " +
                    $"is missing {missing}; refusing to sign a UserOperation with unset gas limits.");
            }

            return estimate;
        }

        private static bool TryGetCustomErrorRevertData(RpcResponseException ex, out string revertData)
        {
            revertData = ex.RpcError?.GetDataAsString();
            try
            {
                ContractRevertExceptionHandler.HandleContractRevertException(revertData);
            }
            catch (SmartContractCustomErrorRevertException)
            {
                return true;
            }
            catch (SmartContractRevertException)
            {
            }

            revertData = null;
            return false;
        }

        private async Task FillMissingGasFromBundlerAsync(UserOperation userOp, EntryPointService entryPointService)
        {
            if (userOp.CallGasLimit != null && userOp.VerificationGasLimit != null && userOp.PreVerificationGas != null)
                return;

            var estimate = await EstimateUserOperationGasAsync(userOp, entryPointService);

            userOp.CallGasLimit ??= AdjustGas(estimate.CallGasLimit.Value, GasConfig.CallGasMultiplier, GasConfig.CallGasBuffer);
            userOp.VerificationGasLimit ??= AdjustGas(estimate.VerificationGasLimit.Value, GasConfig.VerificationGasMultiplier, GasConfig.VerificationGasBuffer)
                + (_validator?.GetVerificationGasBuffer() ?? 0);
            userOp.PreVerificationGas ??= estimate.PreVerificationGas.Value + (GasConfig.PreVerificationGasBuffer ?? 0);

            if (!string.IsNullOrEmpty(userOp.Paymaster))
            {
                userOp.PaymasterVerificationGasLimit ??= estimate.PaymasterVerificationGasLimit?.Value;
                userOp.PaymasterPostOpGasLimit ??= estimate.PaymasterPostOpGasLimit?.Value;
            }
        }

        private static BigInteger AdjustGas(BigInteger estimated, decimal multiplier, BigInteger? buffer)
        {
            var adjusted = multiplier == 1.0m
                ? estimated
                : new BigInteger(decimal.Ceiling((decimal)estimated * multiplier));
            return adjusted + (buffer ?? BigInteger.Zero);
        }

        private async Task<UserOperation> BuildUserOperationFromCallDataAsync(byte[] callData)
        {
            _pendingEip7702Auth = null;

            var (factory, factoryData) = await GetInitCodeIfNeededAsync();

            var userOp = new UserOperation
            {
                Sender = _accountAddress,
                CallData = callData
            };

            if (!string.IsNullOrEmpty(factory))
            {
                userOp.InitCode = Nethereum.Util.ByteUtil.Merge(
                    factory.HexToByteArray(),
                    factoryData ?? Array.Empty<byte>());
            }

            if (PaymasterConfig != null)
            {
                userOp.Paymaster = PaymasterConfig.Address;
                userOp.PaymasterData = PaymasterConfig.Data ?? Array.Empty<byte>();
            }

            if (Eip7702DelegationConfig != null)
                await ApplyEip7702DelegationAsync(userOp);

            return userOp;
        }

        private async Task ApplyEip7702DelegationAsync(UserOperation userOp)
        {
            var chainId = await GetChainIdAsync();

            var accountNonce = await EthApiContractService.Transactions.GetTransactionCount
                .SendRequestAsync(userOp.Sender);

            if (_eip7702AuthSigner != null)
                _pendingEip7702Auth = _eip7702AuthSigner.SignAuthorisation(chainId, Eip7702DelegationConfig.DelegateAddress, accountNonce.Value);
            else
                throw new NotSupportedException("The active signer cannot produce EIP-7702 authorisations; 7702 requires a secp256k1 signer.");

            userOp.Eip7702Auth = _pendingEip7702Auth;

            var entryPointService = new EntryPointService(EthApiContractService, _entryPointAddress);
            userOp.Nonce ??= await entryPointService.GetNonceQueryAsync(userOp.Sender, 0);

            if (Eip7702DelegationConfig.FactoryData != null)
                userOp.InitCode = AAEIP7702Utils.BuildInitCode(
                    "0x7702", Eip7702DelegationConfig.FactoryData);
        }

        private async Task<(string factory, byte[] factoryData)> GetInitCodeIfNeededAsync()
        {
            if (_initCodeBuilder == null)
                return (null, null);

            var code = await EthApiContractService.GetCode.SendRequestAsync(_accountAddress);
            if (!string.IsNullOrEmpty(code) && code != "0x" && code.Length > 2)
                return (null, null);

            return (_initCodeBuilder.FactoryAddress, _initCodeBuilder.BuildFactoryData());
        }

        private async Task<AATransactionReceipt> SendAndWaitForReceiptAsync(
            PackedUserOperation packedOp, CancellationToken? cancellationToken)
        {
            var rpcUserOp = UserOperationConverter.ToRpcFormat(packedOp);
            AttachPendingEip7702Auth(rpcUserOp);
            var userOpHash = await _bundlerService.SendUserOperation.SendRequestAsync(rpcUserOp, _entryPointAddress);

            var receipt = await WaitForReceiptAsync(userOpHash, cancellationToken);
            return AATransactionReceipt.FromUserOperationReceipt(receipt);
        }

        private void AttachPendingEip7702Auth(RPC.AccountAbstraction.DTOs.UserOperation rpcUserOp)
        {
            if (_pendingEip7702Auth != null)
                rpcUserOp.Eip7702Auth = _pendingEip7702Auth;
        }

        private async Task<RPC.AccountAbstraction.DTOs.UserOperationReceipt> WaitForReceiptAsync(
            string userOpHash, CancellationToken? cancellationToken)
        {
            var timeout = TimeSpan.FromMilliseconds(GasConfig.ReceiptTimeoutMs);
            var pollInterval = TimeSpan.FromMilliseconds(GasConfig.ReceiptPollIntervalMs);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                var receipt = await _bundlerService.GetUserOperationReceipt.SendRequestAsync(userOpHash);
                if (receipt != null)
                    return receipt;

                await Task.Delay(pollInterval);
            }

            throw new TimeoutException($"UserOperation {userOpHash} not mined within {GasConfig.ReceiptTimeoutMs}ms");
        }

#endif
    }
}
