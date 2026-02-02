using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using PackedUserOperation = Nethereum.AccountAbstraction.Structs.PackedUserOperation;

namespace Nethereum.AccountAbstraction
{
    public class AAContractHandler : ContractHandler
    {
        private readonly string _accountAddress;
        private readonly EthECKey _signerKey;
        private readonly IAccountAbstractionBundlerService _bundlerService;
        private readonly string _entryPointAddress;
        private BigInteger? _chainId;

        public FactoryConfig FactoryConfig { get; private set; }
        public PaymasterConfig PaymasterConfig { get; private set; }
        public AAGasConfig GasConfig { get; private set; } = AAGasConfig.Default;

        public string AccountAddress => _accountAddress;
        public string EntryPointAddress => _entryPointAddress;

        public AAContractHandler(
            string contractAddress,
            string accountAddress,
            EthECKey signerKey,
            IAccountAbstractionBundlerService bundlerService,
            string entryPointAddress,
            IEthApiContractService ethApiContractService,
            string addressFrom = null)
            : base(contractAddress, ethApiContractService, addressFrom)
        {
            _accountAddress = accountAddress;
            _signerKey = signerKey;
            _bundlerService = bundlerService;
            _entryPointAddress = entryPointAddress;
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
            string bundlerRpcUrl,
            string entryPointAddress,
            IWeb3 web3,
            string addressFrom = null)
            : this(contractAddress, accountAddress, signerKey,
                  new AccountAbstractionBundlerService(new RpcClient(new Uri(bundlerRpcUrl))),
                  entryPointAddress, web3, addressFrom)
        {
        }

        public AAContractHandler(
            string contractAddress,
            string accountAddress,
            EthECKey signerKey,
            string bundlerRpcUrl,
            string entryPointAddress,
            IEthApiContractService ethApiContractService,
            string addressFrom = null)
            : this(contractAddress, accountAddress, signerKey,
                  new AccountAbstractionBundlerService(new RpcClient(new Uri(bundlerRpcUrl))),
                  entryPointAddress, ethApiContractService, addressFrom)
        {
        }

        public static AAContractHandler CreateFromContractHandler(
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

        public static AAContractHandler CreateFromExistingContractService<T>(
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

        public static AAContractHandler CreateFromExistingContractService<T>(
            T service,
            string accountAddress,
            EthECKey signerKey,
            string bundlerRpcUrl,
            string entryPointAddress) where T : ContractWeb3ServiceBase
        {
            return new AAContractHandler(
                service.ContractAddress,
                accountAddress,
                signerKey,
                bundlerRpcUrl,
                entryPointAddress,
                service.Web3,
                service.ContractHandler.AddressFrom);
        }

        public AAContractHandler WithFactory(FactoryConfig config)
        {
            FactoryConfig = config;
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

        public AAContractHandler WithGasConfig(AAGasConfig config)
        {
            GasConfig = config ?? AAGasConfig.Default;
            return this;
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
            return await _bundlerService.SendUserOperation.SendRequestAsync(rpcUserOp, _entryPointAddress);
        }

        public override async Task<string> SignTransactionAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(transactionMessage);

            var packedOp = await CreateUserOperationAsync(transactionMessage);
            var rpcUserOp = UserOperationConverter.ToRpcFormat(packedOp);
            return System.Text.Json.JsonSerializer.Serialize(rpcUserOp);
        }

        public override async Task<HexBigInteger> EstimateGasAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            SetAddressFrom(transactionMessage);

            var userOp = await BuildUserOperationAsync(transactionMessage);
            var rpcUserOp = UserOperationConverter.ToRpcFormat(userOp);
            var estimate = await _bundlerService.EstimateUserOperationGas.SendRequestAsync(rpcUserOp, _entryPointAddress);

            var totalGas = (estimate.CallGasLimit?.Value ?? 0) +
                          (estimate.VerificationGasLimit?.Value ?? 0) +
                          (estimate.PreVerificationGas?.Value ?? 0);

            return new HexBigInteger(totalGas);
        }

        public async Task<AATransactionReceipt> BatchExecuteAsync(
            params (string target, BigInteger value, byte[] callData)[] calls)
        {
            var callsList = calls.Select(c => new Call
            {
                Target = c.target,
                Value = c.value,
                Data = c.callData
            }).ToList();

            var batchFunc = new ExecuteBatchFunction { Calls = callsList };
            var packedOp = await CreateUserOperationFromCallDataAsync(batchFunc.GetCallData());
            return await SendAndWaitForReceiptAsync(packedOp, null);
        }

        public async Task<PackedUserOperation> CreateUserOperationAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            var callData = WrapInExecuteCallData(transactionMessage);
            return await CreateUserOperationFromCallDataAsync(callData);
        }

        private async Task<UserOperation> BuildUserOperationAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            var callData = WrapInExecuteCallData(transactionMessage);
            return await BuildUserOperationFromCallDataAsync(callData);
        }

        private byte[] WrapInExecuteCallData<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage)
            where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            var executeFunction = new ExecuteFunction
            {
                Target = ContractAddress,
                Value = transactionMessage.AmountToSend,
                Data = transactionMessage.GetCallData()
            };
            return executeFunction.GetCallData();
        }

        private async Task<PackedUserOperation> CreateUserOperationFromCallDataAsync(byte[] callData)
        {
            var userOp = await BuildUserOperationFromCallDataAsync(callData);

            var entryPointService = new EntryPointService(EthApiContractService, _entryPointAddress);
            return await entryPointService.SignAndInitialiseUserOperationAsync(userOp, _signerKey);
        }

        private async Task<UserOperation> BuildUserOperationFromCallDataAsync(byte[] callData)
        {
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
                userOp.PaymasterData = await PaymasterConfig.GetPaymasterDataAsync(userOp);
            }

            return userOp;
        }

        private async Task<(string factory, byte[] factoryData)> GetInitCodeIfNeededAsync()
        {
            if (FactoryConfig == null)
                return (null, null);

            var code = await EthApiContractService.GetCode.SendRequestAsync(_accountAddress);
            if (!string.IsNullOrEmpty(code) && code != "0x" && code.Length > 2)
                return (null, null);

            var createAccountFunc = new SimpleAccountFactoryCreateAccountFunction
            {
                Owner = FactoryConfig.Owner,
                Salt = FactoryConfig.Salt
            };

            return (FactoryConfig.FactoryAddress, createAccountFunc.GetCallData());
        }

        [Function("createAccount", "address")]
        private class SimpleAccountFactoryCreateAccountFunction : FunctionMessage
        {
            [Parameter("address", "owner", 1)]
            public string Owner { get; set; }

            [Parameter("uint256", "salt", 2)]
            public BigInteger Salt { get; set; }
        }

        private async Task<AATransactionReceipt> SendAndWaitForReceiptAsync(
            PackedUserOperation packedOp, CancellationToken? cancellationToken)
        {
            var rpcUserOp = UserOperationConverter.ToRpcFormat(packedOp);
            var userOpHash = await _bundlerService.SendUserOperation.SendRequestAsync(rpcUserOp, _entryPointAddress);

            var receipt = await WaitForReceiptAsync(userOpHash, cancellationToken);
            return AATransactionReceipt.FromUserOperationReceipt(receipt);
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
