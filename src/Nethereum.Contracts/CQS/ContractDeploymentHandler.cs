using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{

    public class ContractDeploymentHandler<TContractDeploymentMessage> : ContractHandlerBase<TContractDeploymentMessage>
        where TContractDeploymentMessage : ContractDeploymentMessage, new()
    {

        private DeploymentMessageEncodingService<TContractDeploymentMessage> _deploymentMessageEncodingService = new DeploymentMessageEncodingService<TContractDeploymentMessage>();
#if !DOTNET35

        public async Task<string> SignTransactionAsync(TContractDeploymentMessage contractDeploymentMessage,
            string contractAddress, bool estimateGas = true)
        {
            EnsureInitEncodingService();
            TransactionInput transactionInput = null;
            if (estimateGas)
                transactionInput = await CreateTransactionInputEstimatingGasAsync(contractDeploymentMessage).ConfigureAwait(false);
            else
                transactionInput = _deploymentMessageEncodingService.CreateTransactionInput(contractDeploymentMessage);
            return await this.Eth.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);
        }

        public async Task<string> SignTransactionRetrievingNextNonceAsync(TContractDeploymentMessage contractDeploymentMessage,
            string contractAddress, bool estimateGas = true)
        {
            EnsureInitEncodingService();
            TransactionInput transactionInput = null;
            if (estimateGas)
                transactionInput = await CreateTransactionInputEstimatingGasAsync(contractDeploymentMessage).ConfigureAwait(false);
            else
                transactionInput = _deploymentMessageEncodingService.CreateTransactionInput(contractDeploymentMessage);
            return await this.Eth.TransactionManager.SignTransactionRetrievingNextNonceAsync(transactionInput).ConfigureAwait(false);
        }

        public Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(CancellationTokenSource tokenSource = null)
        {
            var contractDeploymentMessage = new TContractDeploymentMessage();
            return SendRequestAndWaitForReceiptAsync(contractDeploymentMessage, tokenSource);
        }

        public async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(
            TContractDeploymentMessage contractDeploymentMessage, CancellationTokenSource tokenSource = null)
        {
            EnsureInitEncodingService();
            var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            contractDeploymentMessage.Gas = gasEstimate;
            var transactionInput = _deploymentMessageEncodingService.CreateTransactionInput(contractDeploymentMessage);
            return await Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(transactionInput,  tokenSource).ConfigureAwait(false);
        }

        public Task<string> SendRequestAsync()
        {
            var contractDeploymentMessage = new TContractDeploymentMessage();
            return SendRequestAsync(contractDeploymentMessage);
        }

        public async Task<string> SendRequestAsync(TContractDeploymentMessage contractDeploymentMessage)
        {
            EnsureInitEncodingService();
            var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            contractDeploymentMessage.Gas = gasEstimate;
            var transactionInput = _deploymentMessageEncodingService.CreateTransactionInput(contractDeploymentMessage);
            return await Eth.TransactionManager.SendTransactionAsync(transactionInput).ConfigureAwait(false);
        }

        public async Task<TransactionInput> CreateTransactionInputEstimatingGasAsync(TContractDeploymentMessage contractDeploymentMessage)
        {
            EnsureInitEncodingService();
            var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            contractDeploymentMessage.Gas = gasEstimate;
            return _deploymentMessageEncodingService.CreateTransactionInput(contractDeploymentMessage);
        }

        public Task<HexBigInteger> EstimateGasAsync(TContractDeploymentMessage functionMessage)
        {
            EnsureInitEncodingService();
            var callInput = _deploymentMessageEncodingService.CreateCallInput(functionMessage);
            return Eth.TransactionManager.EstimateGasAsync(callInput);
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGas(
            TContractDeploymentMessage contractDeploymentMessage)
        {
            var maxGas = contractDeploymentMessage.GetHexMaximumGas();
            if (maxGas == null)
                maxGas = await EstimateGasAsync(contractDeploymentMessage).ConfigureAwait(false);
            return maxGas;
        }

        private void EnsureInitEncodingService()
        {
            _deploymentMessageEncodingService.DefaultAddressFrom = GetAccountAddressFrom();
        }

#endif
    }
}