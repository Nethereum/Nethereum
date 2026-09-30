using System;
using System.Numerics;

namespace Nethereum.CoreChain.Rpc
{
    public class RpcContext
    {
        private readonly IChainNode _node;
        private readonly Func<IChainNode> _nodeProvider;

        public IChainNode Node => _node ?? _nodeProvider();
        public BigInteger ChainId { get; }
        public IServiceProvider Services { get; }
        public ITxPool TxPool { get; set; }

        public RpcContext(IChainNode node, BigInteger chainId, IServiceProvider services)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            ChainId = chainId;
            Services = services;
        }

        public RpcContext(Func<IChainNode> nodeProvider, BigInteger chainId, IServiceProvider services)
        {
            _nodeProvider = nodeProvider ?? throw new ArgumentNullException(nameof(nodeProvider));
            ChainId = chainId;
            Services = services;
        }

        public T GetService<T>() where T : class
        {
            return Services.GetService(typeof(T)) as T;
        }

        public T GetRequiredService<T>() where T : class
        {
            var service = GetService<T>();
            if (service == null)
                throw new InvalidOperationException($"Service {typeof(T).Name} not found");
            return service;
        }
    }
}
