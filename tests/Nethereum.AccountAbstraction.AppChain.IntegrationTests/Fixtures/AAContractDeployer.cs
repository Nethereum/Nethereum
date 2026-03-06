using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Fixtures
{
    public class AAContractDeployer
    {
        private readonly IWeb3 _web3;

        public AAContractDeployer(IWeb3 web3)
        {
            _web3 = web3;
        }

        public async Task<AppChainDeployment> DeployAllAsync(AppChainConfig config)
        {
            var deployer = new AADeployer(_web3);
            return await deployer.DeployAsync(config);
        }

        public async Task<bool> IsDeployedAsync(string address)
        {
            var code = await _web3.Eth.GetCode.SendRequestAsync(address);
            return code != null && code.Length > 2 && code != "0x";
        }
    }
}
