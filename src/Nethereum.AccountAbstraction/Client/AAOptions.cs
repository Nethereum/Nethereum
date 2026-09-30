using System;
using System.Numerics;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Client
{
    public class AAOptions
    {
        internal IWeb3? Web3 { get; private set; }
        internal AADeploymentAddresses? DeploymentAddresses { get; private set; }
        internal IAccountAbstractionBundlerService? Bundler { get; private set; }

        public AAOptions UseWeb3(IWeb3 web3)
        {
            Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            return this;
        }

        public AAOptions UseDeploymentAddresses(AADeploymentAddresses deploymentAddresses)
        {
            DeploymentAddresses = deploymentAddresses ?? throw new ArgumentNullException(nameof(deploymentAddresses));
            return this;
        }

        public AAOptions UseDeploymentAddresses(IAADeploymentAddressProvider deploymentAddressProvider, BigInteger chainId)
        {
            if (deploymentAddressProvider == null) throw new ArgumentNullException(nameof(deploymentAddressProvider));
            DeploymentAddresses = deploymentAddressProvider.Get(chainId);
            return this;
        }

        public AAOptions UseBundler(IAccountAbstractionBundlerService bundler)
        {
            Bundler = bundler ?? throw new ArgumentNullException(nameof(bundler));
            return this;
        }

        public AAOptions UseBundlerUrl(string bundlerRpcUrl)
        {
            Bundler = new AccountAbstractionBundlerService(new RpcClient(new Uri(bundlerRpcUrl)));
            return this;
        }
    }
}
