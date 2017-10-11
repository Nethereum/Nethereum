using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Contracts.CQS
{
#if !DOTNET35
    public class ContractDeploymentHandler<TContractDeploymentMessage>: ContractTransactionHandlerBase<TContractDeploymentMessage> where TContractDeploymentMessage : ContractDeployment
    {
        protected override Task<TransactionReceipt> ExecuteTransactionAsync(TContractDeploymentMessage contractDeploymentMessage, HexBigInteger gasEstimate, CancellationTokenSource tokenSource = null)
        {
            return Eth.DeployContract.SendRequestAndWaitForReceiptAsync<TContractDeploymentMessage>(contractDeploymentMessage.ByteCode,
                                                                                      contractDeploymentMessage.FromAddress,
                                                                                      gasEstimate,
                                                                                      GetGasPrice(contractDeploymentMessage),
                                                                                      GetValue(contractDeploymentMessage),
                                                                                      contractDeploymentMessage,
                                                                                      tokenSource);
        }

        protected override Task<HexBigInteger> EstimateGasAsync(TContractDeploymentMessage contractDeploymentMessage)
        {
            return Eth.DeployContract.EstimateGasAsync<TContractDeploymentMessage>(contractDeploymentMessage.ByteCode, contractDeploymentMessage.FromAddress, null, GetValue(contractDeploymentMessage), contractDeploymentMessage);
        }
    }
#endif
}
