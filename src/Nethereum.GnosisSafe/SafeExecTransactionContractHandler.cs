using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Services;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using System.Numerics;
using Nethereum.GnosisSafe.ContractDefinition;
using Nethereum.Model;
using Nethereum.Hex.HexTypes;

namespace Nethereum.GnosisSafe
{

    public static class SafeExecTransactionContractHandlerExtensions 
    {
        public static void ChangeContractHandlerToSafeExecTransaction<T>(this T service, string safeAddress, params string[] privateKeySigners) where T : ContractWeb3ServiceBase
        {
            service.ContractHandler =
                SafeExecTransactionContractHandler.CreateFromExistingContractService(service, safeAddress, privateKeySigners);
        }
    }

    public class SafeExecTransactionContractHandler : ContractHandler
    {
        public static SafeExecTransactionContractHandler CreateFromExistingContractService<T>(T service, string safeAddress, params string[] privateKeySigners) where T:ContractWeb3ServiceBase
        {
            var contractAddress = service.ContractAddress;
            var ethApiContractService = service.Web3;
            var handler = new SafeExecTransactionContractHandler(contractAddress, safeAddress, ethApiContractService, service.ContractHandler.AddressFrom, privateKeySigners);
            return handler;
        }

        public SafeExecTransactionContractHandler(string contractAddress, string safeAddress, IWeb3 web3, string addressFrom, params string[] privateKeySigners) : base(contractAddress, web3.Eth)
        {
            this.AddressFrom = addressFrom;
            this._privateKeySigners = privateKeySigners;
            SafeService = new GnosisSafeService(web3, safeAddress);
        }
        
        private string[] _privateKeySigners;
        public GnosisSafeService SafeService { get; protected set; }

#if !DOTNET35

        private BigInteger? _chainId;
        private async Task<BigInteger?> GetChainIdAsync()
        {
            if(_chainId == null)
            {
                if(EthApiContractService.TransactionManager.ChainId == null)
                {
                    _chainId = await EthApiContractService.ChainId.SendRequestAsync();
                }
                else
                {
                    _chainId = EthApiContractService.TransactionManager.ChainId;
                }
            }
            return _chainId;
        }

        public override async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null, CancellationTokenSource tokenSource = null)
        {
            
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var execTransactionFunction = await CreateExecTransactionFunction(transactionMessage);

            return await SafeService.ExecTransactionRequestAndWaitForReceiptAsync(execTransactionFunction);
        }

       

        public override async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage, CancellationToken cancellationToken)

        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var execTransactionFunction = await CreateExecTransactionFunction(transactionMessage);

            return await SafeService.ExecTransactionRequestAndWaitForReceiptAsync(execTransactionFunction);
        }

        private async Task<ExecTransactionFunction> CreateExecTransactionFunction<TEthereumContractFunctionMessage>(TEthereumContractFunctionMessage transactionMessage) where TEthereumContractFunctionMessage : FunctionMessage, new()
        {
            var chainId = await GetChainIdAsync();
            var execTransactionFunction = await SafeService.BuildMultiSignatureTransactionAsync(
                 new EncodeTransactionDataFunction() { To = ContractAddress }, transactionMessage, (int)chainId, false,
                 _privateKeySigners);
            return execTransactionFunction;
        }

        public async override Task<string> SendRequestAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var execTransactionFunction = await CreateExecTransactionFunction(transactionMessage);
            return await SafeService.ExecTransactionRequestAsync(execTransactionFunction);
        }

        public override async Task<string> SignTransactionAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)

        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var execTransactionFunction = await CreateExecTransactionFunction(transactionMessage);
            return await SafeService.ContractHandler.SignTransactionAsync(execTransactionFunction);
        }

        public override async Task<HexBigInteger> EstimateGasAsync<TEthereumContractFunctionMessage>(
            TEthereumContractFunctionMessage transactionMessage = null)
        {
            if (transactionMessage == null) transactionMessage = new TEthereumContractFunctionMessage();
            var execTransactionFunction = await CreateExecTransactionFunction(transactionMessage);
            return await SafeService.ContractHandler.EstimateGasAsync(execTransactionFunction);
        }
#endif

    }


}
