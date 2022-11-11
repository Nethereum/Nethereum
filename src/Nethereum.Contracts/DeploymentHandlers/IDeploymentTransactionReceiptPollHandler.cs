using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.DeploymentHandlers
{
    public interface IDeploymentTransactionReceiptPollHandler<TContractDeploymentMessage> where TContractDeploymentMessage : ContractDeploymentMessage, new()
    {
        Task<TransactionReceipt> SendTransactionAsync(TContractDeploymentMessage deploymentMessage = null, CancellationTokenSource cancellationTokenSource = null);
        Task<TransactionReceipt> SendTransactionAsync(TContractDeploymentMessage deploymentMessage, CancellationToken cancellationToken);
    }
}