using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers;
using Nethereum.CoreChain.Rpc;
using Nethereum.Documentation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc
{
    public static class BundlerRpcHandlerExtensions
    {
        [NethereumDocExample(DocSection.AccountAbstraction, "run-bundler", "Register every standard ERC-4337 JSON-RPC handler against a bundler service", Order = 1)]
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
            registry.Register(new DebugBundlerSendBundleNowHandler(bundlerService));
            registry.Register(new DebugBundlerDumpMempoolHandler(bundlerService));
            registry.Register(new DebugBundlerSetReputationHandler(bundlerService));
            registry.Register(new DebugBundlerDumpReputationHandler(bundlerService));
            registry.Register(new DebugBundlerClearReputationHandler(bundlerService));
            registry.Register(new DebugBundlerClearStateHandler(bundlerService));
            registry.Register(new DebugBundlerClearMempoolHandler(bundlerService));
            registry.Register(new DebugBundlerSetBundlingModeHandler(bundlerService));
            registry.Register(new DebugBundlerGetStakeStatusHandler(bundlerService));

            return registry;
        }
    }
}
