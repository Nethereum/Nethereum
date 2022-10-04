using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.CQS
{
    public interface IContractDeploymentTransactionHandler<TContractDeploymentMessage> where TContractDeploymentMessage : ContractDeploymentMessage, new()
    {
        Task<TransactionInput> CreateTransactionInputEstimatingGasAsync(TContractDeploymentMessage deploymentMessage = null);
        Task<HexBigInteger> EstimateGasAsync(TContractDeploymentMessage contractDeploymentMessage);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TContractDeploymentMessage contractDeploymentMessage = null, CancellationTokenSource tokenSource = null);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(TContractDeploymentMessage contractDeploymentMessage, CancellationToken cancellationToken);
        Task<string> SendRequestAsync(TContractDeploymentMessage contractDeploymentMessage = null);
        Task<string> SignTransactionAsync(TContractDeploymentMessage contractDeploymentMessage);
    }
}