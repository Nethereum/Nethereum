using Nethereum.CoreChain.Rpc;

namespace Nethereum.AppChain.Sequencer.Rpc
{
    public static class ReplicaRpcHandlerExtensions
    {
        public static RpcHandlerRegistry AddReplicaHandlers(this RpcHandlerRegistry registry)
        {
            registry.Register(new ReplicaSyncStatusHandler());
            return registry;
        }
    }
}
