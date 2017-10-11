using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Contracts.CQS
{
#if !DOTNET35
    public class ContractDeploymentHandler<TContractDeploymentMessage>: ContractHandler<TContractDeploymentMessage> where TContractDeploymentMessage : ContractDeploymentMessage
    {

        public async Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TContractDeploymentMessage contractDeploymentMessage, CancellationTokenSource tokenSource = null)
        {
            ValidateContractMessage(contractDeploymentMessage);
                var gasEstimate = await GetOrEstimateMaximumGas(contractDeploymentMessage).ConfigureAwait(false);
            return await SendRequestAndWaitForReceiptAsync(contractDeploymentMessage, gasEstimate, tokenSource);
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGas(TContractDeploymentMessage contractDeploymentMessage)
        {
            var maxGas = GetMaximumGas(contractDeploymentMessage);

            if (maxGas == null)
            {
                maxGas = await EstimateGasAsync(contractDeploymentMessage).ConfigureAwait(false);
            }

            return maxGas;
        }

        protected Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TContractDeploymentMessage contractDeploymentMessage, HexBigInteger gasEstimate, CancellationTokenSource tokenSource = null)
        {
            return Eth.DeployContract.SendRequestAndWaitForReceiptAsync<TContractDeploymentMessage>(contractDeploymentMessage.ByteCode,
                                                                                      contractDeploymentMessage.FromAddress,
                                                                                      gasEstimate,
                                                                                      GetGasPrice(contractDeploymentMessage),
                                                                                      GetValue(contractDeploymentMessage),
                                                                                      contractDeploymentMessage,
                                                                                      tokenSource);
        }

        protected Task<HexBigInteger> EstimateGasAsync(TContractDeploymentMessage contractDeploymentMessage)
        {
            return Eth.DeployContract.EstimateGasAsync<TContractDeploymentMessage>(contractDeploymentMessage.ByteCode, contractDeploymentMessage.FromAddress, null, GetValue(contractDeploymentMessage), contractDeploymentMessage);
        }
    }
#endif
}
