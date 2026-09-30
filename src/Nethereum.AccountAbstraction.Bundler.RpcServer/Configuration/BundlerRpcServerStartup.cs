using System.Numerics;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Configuration
{
    public static class BundlerRpcServerStartup
    {
        public static async Task ValidateAndResolveChainIdAsync(BundlerRpcServerConfig config, IWeb3 web3)
        {
            EnsureSignerConfigured(config);
            await ResolveChainIdAsync(config, web3).ConfigureAwait(false);
        }

        public static void EnsureSignerConfigured(BundlerRpcServerConfig config)
        {
            if (config.RequireSigner && string.IsNullOrEmpty(config.PrivateKey))
            {
                throw new InvalidOperationException(
                    "No signer is configured (PrivateKey is empty) but RequireSigner is true. The " +
                    "bundler cannot submit the handleOps bundle transaction without a signer. " +
                    "Configure a PrivateKey, or set RequireSigner=false for a read-only / " +
                    "validation-only server that never bundles.");
            }
        }

        public static async Task ResolveChainIdAsync(BundlerRpcServerConfig config, IWeb3 web3)
        {
            var nodeChainId = (await web3.Eth.ChainId.SendRequestAsync().ConfigureAwait(false)).Value;

            if (config.ChainId == 0)
            {
                config.ChainId = nodeChainId;
                return;
            }

            if (config.ChainId != nodeChainId)
            {
                throw new InvalidOperationException(
                    $"Configured ChainId ({config.ChainId}) does not match the node's chainId " +
                    $"({nodeChainId}) at {config.RpcUrl}. A wrong chainId corrupts userOpHash and " +
                    "signature validation. Fix the configured ChainId, point at the correct node, " +
                    "or leave ChainId unset (0) to adopt the node's chainId.");
            }
        }
    }
}
