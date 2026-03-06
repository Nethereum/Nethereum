using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers;
using Nethereum.CoreChain.Rpc;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc
{
    public static class BundlerRpcHandlerExtensions
    {
        public static RpcHandlerRegistry AddBundlerHandlers(
            this RpcHandlerRegistry registry,
            IBundlerService bundlerService)
        {
            registry.Register(new EthSendUserOperationHandler(bundlerService));
            registry.Register(new EthEstimateUserOperationGasHandler(bundlerService));
            registry.Register(new EthGetUserOperationByHashHandler(bundlerService));
            registry.Register(new EthGetUserOperationReceiptHandler(bundlerService));
            registry.Register(new EthSupportedEntryPointsHandler(bundlerService));
            registry.Register(new BundlerEthChainIdHandler(bundlerService));

            return registry;
        }

        public static RpcHandlerRegistry AddBundlerDebugHandlers(
            this RpcHandlerRegistry registry,
            IBundlerServiceExtended bundlerService)
        {
            registry.Register(new DebugBundlerFlushHandler(bundlerService));
            registry.Register(new DebugBundlerDumpMempoolHandler(bundlerService));
            registry.Register(new DebugBundlerSetReputationHandler(bundlerService));
            registry.Register(new DebugBundlerGetReputationHandler(bundlerService));

            return registry;
        }
    }
}
