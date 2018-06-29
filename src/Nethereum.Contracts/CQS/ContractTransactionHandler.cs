using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{

    public class ContractTransactionHandler<TContractMessage> : ContractHandlerBase<TContractMessage>
        where TContractMessage : ContractMessage
    {
        private FunctionMessageEncodingService<TContractMessage> _functionMessageEncodingService = new FunctionMessageEncodingService<TContractMessage>();

#if !DOTNET35

        public async Task<string> SignTransactionAsync(TContractMessage functionMessage,
            string contractAddress, bool estimateGas = true)
        {
            EnsureInitEncodingService(contractAddress);
            TransactionInput transactionInput = null;
            if (estimateGas)
                transactionInput = await CreateTransactionInputEstimatingGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            else
                transactionInput = CreateTransactionInput(functionMessage);
            return await this.Eth.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);
        }

        public async Task<string> SignTransactionRetrievingNextNonceAsync(TContractMessage functionMessage,
            string contractAddress, bool estimateGas = true)
        {
            EnsureInitEncodingService(contractAddress);
            TransactionInput transactionInput = null;
            if (estimateGas)
                transactionInput = await CreateTransactionInputEstimatingGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            else
                transactionInput = CreateTransactionInput(functionMessage);
            return await this.Eth.TransactionManager.SignTransactionRetrievingNextNonceAsync(transactionInput).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TContractMessage functionMessage,
            string contractAddress, CancellationTokenSource tokenSource = null)
        {
            EnsureInitEncodingService(contractAddress);
            var transactionInput = await CreateTransactionInputEstimatingGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            return await Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(transactionInput, tokenSource).ConfigureAwait(false);
        }

        public async Task<string> SendRequestAsync(TContractMessage functionMessage, string contractAddress)
        {
            EnsureInitEncodingService(contractAddress);
            var transactionInput = await CreateTransactionInputEstimatingGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            return await Eth.TransactionManager.SendTransactionAsync(transactionInput).ConfigureAwait(false);
        }

        public async Task<TransactionInput> CreateTransactionInputEstimatingGasAsync(TContractMessage functionMessage,
            string contractAddress)
        {
            EnsureInitEncodingService(contractAddress);
            var gasEstimate = await GetOrEstimateMaximumGas(functionMessage, contractAddress).ConfigureAwait(false);
            functionMessage.Gas = gasEstimate;
            return _functionMessageEncodingService.CreateTransactionInput(functionMessage);
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGas(TContractMessage functionMessage,
            string contractAddress)
        {
            var maxGas = functionMessage.GetHexMaximumGas() ??
                         await EstimateGasAsync(functionMessage, contractAddress).ConfigureAwait(false);
            return maxGas;
        }

        public Task<HexBigInteger> EstimateGasAsync(TContractMessage functionMessage, string contractAddress)
        {
            EnsureInitEncodingService(contractAddress);
            var callInput = CreateCallInput(functionMessage);
            return Eth.TransactionManager.EstimateGasAsync(callInput);
        }

        private CallInput CreateCallInput(TContractMessage functionMessage)
        {
            return _functionMessageEncodingService.CreateCallInput(functionMessage);
        }

        private TransactionInput CreateTransactionInput(TContractMessage functionMessage)
        {
            return _functionMessageEncodingService.CreateTransactionInput(functionMessage);
        }

        private void EnsureInitEncodingService(string contractAddress)
        {
            _functionMessageEncodingService.SetContractAddress(contractAddress);
            _functionMessageEncodingService.DefaultAddressFrom = GetAccountAddressFrom();
        }

#endif
    }

}