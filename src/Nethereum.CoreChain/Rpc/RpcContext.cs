using System;
using System.Numerics;

namespace Nethereum.CoreChain.Rpc
{
    public class RpcContext
    {
        public IChainNode Node { get; }
        public BigInteger ChainId { get; }
        public IServiceProvider Services { get; }

        public RpcContext(IChainNode node, BigInteger chainId, IServiceProvider services)
        {
            Node = node;
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
