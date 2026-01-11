using Nethereum.CoreChain.Rpc.Handlers.Standard;

namespace Nethereum.CoreChain.Rpc
{
    public static class CoreRpcHandlerExtensions
    {
        public static RpcHandlerRegistry AddStandardHandlers(this RpcHandlerRegistry registry)
        {
            registry.Register(new EthChainIdHandler());
            registry.Register(new EthBlockNumberHandler());
            registry.Register(new EthGasPriceHandler());
            registry.Register(new EthGetBalanceHandler());
            registry.Register(new EthGetCodeHandler());
            registry.Register(new EthGetStorageAtHandler());
            registry.Register(new EthGetTransactionCountHandler());
            registry.Register(new EthGetBlockByNumberHandler());
            registry.Register(new EthGetBlockByHashHandler());
            registry.Register(new EthGetTransactionReceiptHandler());
            registry.Register(new EthGetLogsHandler());
            registry.Register(new EthGetProofHandler());
            registry.Register(new EthSendRawTransactionHandler());
            registry.Register(new EthCallHandler());
            registry.Register(new EthEstimateGasHandler());
            registry.Register(new EthGetTransactionByHashHandler());
            registry.Register(new EthMaxPriorityFeePerGasHandler());
            registry.Register(new EthFeeHistoryHandler());
            registry.Register(new EthGetBlockTransactionCountByHashHandler());
            registry.Register(new EthGetBlockTransactionCountByNumberHandler());
            registry.Register(new EthGetBlockReceiptsHandler());
            registry.Register(new NetVersionHandler());
            registry.Register(new Web3ClientVersionHandler());

            // Filter handlers
            registry.Register(new EthNewFilterHandler());
            registry.Register(new EthGetFilterChangesHandler());
            registry.Register(new EthGetFilterLogsHandler());
            registry.Register(new EthUninstallFilterHandler());
            registry.Register(new EthNewBlockFilterHandler());

            return registry;
        }
    }
}
