using System;
using System.Numerics;
using Nethereum.CoreChain.Rpc;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace Nethereum.DevChain
{
    /// <summary>
    /// Extension methods for creating Web3 instances connected to DevChainNode.
    /// </summary>
    public static class DevChainWeb3Extensions
    {
        /// <summary>
        /// Creates a Web3 instance connected to this DevChainNode.
        /// </summary>
        /// <param name="node">The DevChainNode to connect to.</param>
        /// <param name="account">The account to use for signing transactions.</param>
        /// <param name="serviceProvider">Optional service provider for RPC handlers.</param>
        /// <returns>A Web3 instance connected to the DevChain.</returns>
        public static IWeb3 CreateWeb3(this DevChainNode node, Account account, IServiceProvider serviceProvider = null)
        {
            var dispatcher = CreateDispatcher(node, serviceProvider);
            var rpcClient = new DevChainRpcClient(dispatcher);
            return new Web3.Web3(account, rpcClient);
        }

        /// <summary>
        /// Creates a Web3 instance connected to this DevChainNode using a private key.
        /// </summary>
        /// <param name="node">The DevChainNode to connect to.</param>
        /// <param name="privateKey">The private key to use for signing.</param>
        /// <param name="serviceProvider">Optional service provider for RPC handlers.</param>
        /// <returns>A Web3 instance connected to the DevChain.</returns>
        public static IWeb3 CreateWeb3(this DevChainNode node, string privateKey, IServiceProvider serviceProvider = null)
        {
            var chainId = (int)node.Config.ChainId;
            var account = new Account(privateKey, chainId);
            return CreateWeb3(node, account, serviceProvider);
        }

        /// <summary>
        /// Creates a read-only Web3 instance connected to this DevChainNode (no signing capability).
        /// </summary>
        /// <param name="node">The DevChainNode to connect to.</param>
        /// <param name="serviceProvider">Optional service provider for RPC handlers.</param>
        /// <returns>A read-only Web3 instance connected to the DevChain.</returns>
        public static IWeb3 CreateWeb3(this DevChainNode node, IServiceProvider serviceProvider = null)
        {
            var dispatcher = CreateDispatcher(node, serviceProvider);
            var rpcClient = new DevChainRpcClient(dispatcher);
            return new Web3.Web3(rpcClient);
        }

        /// <summary>
        /// Creates an RpcDispatcher for this DevChainNode with standard handlers registered.
        /// </summary>
        public static RpcDispatcher CreateDispatcher(this DevChainNode node, IServiceProvider serviceProvider = null)
        {
            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();

            var context = new RpcContext(node, node.Config.ChainId, serviceProvider ?? new EmptyServiceProvider());
            return new RpcDispatcher(registry, context);
        }

        private class EmptyServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType) => null;
        }
    }
}
