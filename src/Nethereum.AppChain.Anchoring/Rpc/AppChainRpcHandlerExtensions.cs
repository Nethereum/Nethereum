using Nethereum.CoreChain.Rpc;

namespace Nethereum.AppChain.Anchoring.Rpc
{
    public static class AppChainRpcHandlerExtensions
    {
        public static RpcHandlerRegistry AddMessageProofHandlers(this RpcHandlerRegistry registry)
        {
            registry.Register(new AppChainGetMessageProofHandler());
            registry.Register(new AppChainGetMessageResultsHandler());
            return registry;
        }
    }
}
