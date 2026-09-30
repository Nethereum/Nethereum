using System;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Validators.ECDSASessionValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Validators.ECDSASessionValidator.ContractDefinition;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Deployment
{
    public class AAModuleDeployer
    {
        private readonly IWeb3 _web3;

        public AAModuleDeployer(IWeb3 web3)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
        }

        public async Task<AAModuleAddresses> DeployAsync(AAModuleDeploymentOptions? options = null)
        {
            options ??= new AAModuleDeploymentOptions();
            var addresses = new AAModuleAddresses();

            if (options.EcdsaValidator)
            {
                var service = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                    _web3, new ECDSAValidatorDeployment()).ConfigureAwait(false);
                addresses.EcdsaValidator = service.ContractAddress;
            }

            if (options.SmartSession)
            {
                var smartSession = await SmartSessionService.DeployContractAndGetServiceAsync(
                    _web3, new SmartSessionDeployment()).ConfigureAwait(false);
                addresses.SmartSession = smartSession.ContractAddress;

                var sudoPolicy = await SudoPolicyService.DeployContractAndGetServiceAsync(
                    _web3, new SudoPolicyDeployment()).ConfigureAwait(false);
                addresses.SudoPolicy = sudoPolicy.ContractAddress;

                var uniActionPolicy = await UniActionPolicyService.DeployContractAndGetServiceAsync(
                    _web3, new UniActionPolicyDeployment()).ConfigureAwait(false);
                addresses.UniActionPolicy = uniActionPolicy.ContractAddress;

                var sessionValidator = await ECDSASessionValidatorService.DeployContractAndGetServiceAsync(
                    _web3, new ECDSASessionValidatorDeployment()).ConfigureAwait(false);
                addresses.EcdsaSessionValidator = sessionValidator.ContractAddress;
            }

            if (options.SocialRecovery)
            {
                var service = await SocialRecoveryService.DeployContractAndGetServiceAsync(
                    _web3, new SocialRecoveryDeployment()).ConfigureAwait(false);
                addresses.SocialRecovery = service.ContractAddress;
            }

            if (options.OwnableExecutor)
            {
                var service = await OwnableExecutorService.DeployContractAndGetServiceAsync(
                    _web3, new OwnableExecutorDeployment()).ConfigureAwait(false);
                addresses.OwnableExecutor = service.ContractAddress;
            }

            return addresses;
        }
    }
}
