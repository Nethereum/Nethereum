using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{

    public class ContractDeploymentHandler<TContractDeploymentMessage> : ContractHandlerBase<TContractDeploymentMessage>
        where TContractDeploymentMessage : ContractDeploymentMessage, new()
    {
        public string GetData(TContractDeploymentMessage contractDeploymentMessage)
        {
            ValidateContractMessage(contractDeploymentMessage);
            var deployContractTransactionBuilder = new DeployContractTransactionBuilder();
            return deployContractTransactionBuilder.GetData(contractDeploymentMessage.ByteCode, contractDeploymentMessage);
        }

        public TransactionInput CreateTransactionInput(
            TContractDeploymentMessage contractDeploymentMessage)
        {
            ValidateContractMessage(contractDeploymentMessage);
            var deployContractTransactionBuilder = new DeployContractTransactionBuilder();
            var transactionInput =  deployContractTransactionBuilder.BuildTransaction(contractDeploymentMessage.ByteCode,
                GetDefaultAddressFrom(contractDeploymentMessage),
                GetMaximumGas(contractDeploymentMessage), GetGasPrice(contractDeploymentMessage), GetValue(contractDeploymentMessage),
                contractDeploymentMessage);
            transactionInput.Nonce = GetNonce(contractDeploymentMessage);
            return transactionInput;
        }

#if !DOTNET35

        public async Task<string> SignTransactionAsync(TContractDeploymentMessage contractDeploymentMessage,
            string contractAddress, bool estimateGas = true)
        {
            TransactionInput transactionInput = null;
            if (estimateGas)
                transactionInput = await CreateTransactionInputEstimatingGasAsync(contractDeploymentMessage).ConfigureAwait(false);
            else
                transactionInput = CreateTransactionInput(contractDeploymentMessage);
            return await this.Eth.TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);
        }

        public async Task<string> SignTransactionRetrievingNextNonceAsync(TContractDeploymentMessage contractDeploymentMessage,
            string contractAddress, bool estimateGas = true)
        {
            TransactionInput transactionInput = null;
            if (estimateGas)
                transactionInput = await CreateTransactionInputEstimatingGasAsync(contractDeploymentMessage).ConfigureAwait(false);
            else
                transactionInput = CreateTransactionInput(contractDeploymentMessage);
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
            ValidateContractMessage(contractDeploymentMessage);
            var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            return await SendRequestAndWaitForReceiptAsync(contractDeploymentMessage, gasEstimate, tokenSource);
        }

        public Task<string> SendRequestAsync()
        {
            var contractDeploymentMessage = new TContractDeploymentMessage();
            return SendRequestAsync(contractDeploymentMessage);
        }

        public async Task<string> SendRequestAsync(TContractDeploymentMessage contractDeploymentMessage)
        {
            ValidateContractMessage(contractDeploymentMessage);
            var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            return await SendRequestAsync(contractDeploymentMessage, gasEstimate).ConfigureAwait(false);
        }

        public async Task<TransactionInput> CreateTransactionInputEstimatingGasAsync(
            TContractDeploymentMessage contractDeploymentMessage)
        {
            ValidateContractMessage(contractDeploymentMessage);
            var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            var deployContractTransactionBuilder = new DeployContractTransactionBuilder();
            var transactionInput = deployContractTransactionBuilder.BuildTransaction(contractDeploymentMessage.ByteCode,
                GetDefaultAddressFrom(contractDeploymentMessage),
                gasEstimate, GetGasPrice(contractDeploymentMessage), GetValue(contractDeploymentMessage),
                contractDeploymentMessage);
            transactionInput.Nonce = GetNonce(contractDeploymentMessage);
            return transactionInput;
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGas(
            TContractDeploymentMessage contractDeploymentMessage)
        {
            var maxGas = GetMaximumGas(contractDeploymentMessage);

            if (maxGas == null)
                maxGas = await EstimateGasAsync(contractDeploymentMessage).ConfigureAwait(false);

            return maxGas;
        }

        protected Task<string> SendRequestAsync(TContractDeploymentMessage contractDeploymentMessage,
            HexBigInteger gasEstimate)
        {
            return Eth.DeployContract.SendRequestAsync(contractDeploymentMessage.ByteCode,
                GetDefaultAddressFrom(contractDeploymentMessage),
                gasEstimate,
                GetGasPrice(contractDeploymentMessage),
                GetValue(contractDeploymentMessage),
                GetNonce(contractDeploymentMessage),
                contractDeploymentMessage);
        }

        protected Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(
            TContractDeploymentMessage contractDeploymentMessage, HexBigInteger gasEstimate,
            CancellationTokenSource tokenSource = null)
        {
            return Eth.DeployContract.SendRequestAndWaitForReceiptAsync(contractDeploymentMessage.ByteCode,
                GetDefaultAddressFrom(contractDeploymentMessage),
                gasEstimate,
                GetGasPrice(contractDeploymentMessage),
                GetValue(contractDeploymentMessage),
                GetNonce(contractDeploymentMessage),
                contractDeploymentMessage,
                tokenSource);
        }
       
        public Task<HexBigInteger> EstimateGasAsync(TContractDeploymentMessage contractDeploymentMessage)
        {
            ValidateContractMessage(contractDeploymentMessage);
            return Eth.DeployContract.EstimateGasAsync(contractDeploymentMessage.ByteCode,
                GetDefaultAddressFrom(contractDeploymentMessage), null, GetValue(contractDeploymentMessage),
                contractDeploymentMessage);
        }
#endif
    }
}