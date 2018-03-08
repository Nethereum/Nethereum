using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{
#if !DOTNET35
    public class ContractDeploymentHandler<TContractDeploymentMessage> : ContractHandlerBase<TContractDeploymentMessage>
        where TContractDeploymentMessage : ContractDeploymentMessage
    {
        public async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(
            TContractDeploymentMessage contractDeploymentMessage, CancellationTokenSource tokenSource = null)
        {
            ValidateContractMessage(contractDeploymentMessage);
            var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            return await SendRequestAndWaitForReceiptAsync(contractDeploymentMessage, gasEstimate, tokenSource);
        }

        public async Task<string> SendRequestAsync(TContractDeploymentMessage contractDeploymentMessage)
        {
            ValidateContractMessage(contractDeploymentMessage);
            var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            return await SendRequestAsync(contractDeploymentMessage, gasEstimate).ConfigureAwait(false);
        }

        public async Task<TransactionInput> CreateTransactionInputAsync(
            TContractDeploymentMessage contractDeploymentMessage)
        {
            ValidateContractMessage(contractDeploymentMessage);
            var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            var deployContractTransactionBuilder = new DeployContractTransactionBuilder();
            return deployContractTransactionBuilder.BuildTransaction(contractDeploymentMessage.ByteCode,
                contractDeploymentMessage.FromAddress,
                gasEstimate, GetGasPrice(contractDeploymentMessage), GetValue(contractDeploymentMessage),
                contractDeploymentMessage);
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
                contractDeploymentMessage.FromAddress,
                gasEstimate,
                GetGasPrice(contractDeploymentMessage),
                GetValue(contractDeploymentMessage),
                contractDeploymentMessage);
        }

        protected Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(
            TContractDeploymentMessage contractDeploymentMessage, HexBigInteger gasEstimate,
            CancellationTokenSource tokenSource = null)
        {
            return Eth.DeployContract.SendRequestAndWaitForReceiptAsync(contractDeploymentMessage.ByteCode,
                contractDeploymentMessage.FromAddress,
                gasEstimate,
                GetGasPrice(contractDeploymentMessage),
                GetValue(contractDeploymentMessage),
                contractDeploymentMessage,
                tokenSource);
        }
       
        public Task<HexBigInteger> EstimateGasAsync(TContractDeploymentMessage contractDeploymentMessage)
        {
            ValidateContractMessage(contractDeploymentMessage);
            return Eth.DeployContract.EstimateGasAsync(contractDeploymentMessage.ByteCode,
                contractDeploymentMessage.FromAddress, null, GetValue(contractDeploymentMessage),
                contractDeploymentMessage);
        }
    }
#endif
}