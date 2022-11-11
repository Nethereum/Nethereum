using System.Threading.Tasks;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.DeploymentHandlers
{
#if !DOTNET35
    /// <summary>
    /// Signs a transaction estimating the gas if not set and retrieving the next nonce if not set
    /// </summary>
    public class DeploymentSigner<TContractDeploymentMessage> : DeploymentHandlerBase<TContractDeploymentMessage>, 
        IDeploymentSigner<TContractDeploymentMessage> where TContractDeploymentMessage : ContractDeploymentMessage, new()
    {
        private readonly IDeploymentEstimatorHandler<TContractDeploymentMessage> _deploymentEstimatorHandler;
        private ITransactionManager _transactionManager;

       
        public DeploymentSigner(ITransactionManager transactionManager) : this(transactionManager,
            new DeploymentEstimatorHandler<TContractDeploymentMessage>(transactionManager))
        {

        }

        public DeploymentSigner(ITransactionManager transactionManager, 
            IDeploymentEstimatorHandler<TContractDeploymentMessage> deploymentEstimatorHandler) : base(transactionManager)  
        {
            _deploymentEstimatorHandler = deploymentEstimatorHandler;
        }

        public async Task<string> SignTransactionAsync(TContractDeploymentMessage deploymentMessage = null)
        {
            if (deploymentMessage == null) deploymentMessage = new TContractDeploymentMessage();
            deploymentMessage.Gas = await GetOrEstimateMaximumGasAsync(deploymentMessage).ConfigureAwait(false);
            var transactionInput = DeploymentMessageEncodingService.CreateTransactionInput(deploymentMessage);
            return await TransactionManager.SignTransactionAsync(transactionInput).ConfigureAwait(false);
        }

        protected virtual async Task<HexBigInteger> GetOrEstimateMaximumGasAsync(
            TContractDeploymentMessage deploymentMessage)
        {
            return deploymentMessage.GetHexMaximumGas()
                   ?? await _deploymentEstimatorHandler.EstimateGasAsync(deploymentMessage).ConfigureAwait(false);
        }
    }
#endif
}