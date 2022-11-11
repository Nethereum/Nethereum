using System.Threading;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.DeploymentHandlers
{
#if !DOTNET35
    public class
        DeploymentTransactionReceiptPollHandler<TContractDeploymentMessage> :
            DeploymentHandlerBase<TContractDeploymentMessage>,
            IDeploymentTransactionReceiptPollHandler<TContractDeploymentMessage>
        where TContractDeploymentMessage : ContractDeploymentMessage, new()
    {
        private readonly IDeploymentTransactionSenderHandler<TContractDeploymentMessage>
            _deploymentTransactionHandler;


        public DeploymentTransactionReceiptPollHandler(ITransactionManager transactionManager,
            IDeploymentTransactionSenderHandler<TContractDeploymentMessage> deploymentTransactionHandler) : base(transactionManager)
        {
            _deploymentTransactionHandler = deploymentTransactionHandler;
        }

        public DeploymentTransactionReceiptPollHandler(ITransactionManager transactionManager) : this(transactionManager,
            new DeploymentTransactionSenderHandler<TContractDeploymentMessage>(transactionManager))
        {

        }

        public async Task<TransactionReceipt> SendTransactionAsync(TContractDeploymentMessage deploymentMessage,
           CancellationToken cancellationToken)
        {
            if (deploymentMessage == null) deploymentMessage = new TContractDeploymentMessage();
            var transactionHash = await _deploymentTransactionHandler.SendTransactionAsync(deploymentMessage)
                .ConfigureAwait(false);
            return await TransactionManager.TransactionReceiptService
                .PollForReceiptAsync(transactionHash, cancellationToken).ConfigureAwait(false);
        }

        public Task<TransactionReceipt> SendTransactionAsync(TContractDeploymentMessage deploymentMessage = null,
          CancellationTokenSource cancellationTokenSource = null)
        {
            return cancellationTokenSource == null
                ? SendTransactionAsync(deploymentMessage, CancellationToken.None)
                : SendTransactionAsync(deploymentMessage, cancellationTokenSource.Token);
        }
    }
#endif
}


   