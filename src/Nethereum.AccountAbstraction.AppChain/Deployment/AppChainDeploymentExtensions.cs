using System;
using Nethereum.AccountAbstraction.Configuration;

namespace Nethereum.AccountAbstraction.AppChain.Deployment
{
    public static class AppChainDeploymentExtensions
    {
        public static AADeploymentAddresses ToAADeploymentAddresses(this AppChainDeployment deployment)
        {
            if (deployment == null) throw new ArgumentNullException(nameof(deployment));
            if (string.IsNullOrEmpty(deployment.Modules?.EcdsaValidator))
                throw new InvalidOperationException(
                    "AppChainDeployment has no ECDSAValidator module - deploy the modules (AADeployer) before composing AADeploymentAddresses.");

            return new AADeploymentAddresses(
                deployment.EntryPointAddress,
                deployment.AccountFactoryAddress,
                deployment.Modules.EcdsaValidator,
                VerifyingPaymasterAddress: string.Empty);
        }
    }
}
