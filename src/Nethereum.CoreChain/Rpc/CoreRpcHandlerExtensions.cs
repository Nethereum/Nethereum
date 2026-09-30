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
            registry.Register(new EthCreateAccessListHandler());
            registry.Register(new EthGetTransactionByHashHandler());
            registry.Register(new EthMaxPriorityFeePerGasHandler());
            registry.Register(new EthFeeHistoryHandler());
            registry.Register(new EthGetBlockTransactionCountByHashHandler());
            registry.Register(new EthGetBlockTransactionCountByNumberHandler());
            registry.Register(new EthGetUncleCountByBlockHashHandler());
            registry.Register(new EthGetUncleCountByBlockNumberHandler());
            registry.Register(new EthGetBlockReceiptsHandler());
            registry.Register(new EthGetBlockAccessListHandler());
            registry.Register(new EthGetTransactionByBlockHashAndIndexHandler());
            registry.Register(new EthGetTransactionByBlockNumberAndIndexHandler());
            registry.Register(new EthSyncingHandler());
            registry.Register(new EthMiningHandler());
            registry.Register(new EthCoinbaseHandler());
            registry.Register(new NetVersionHandler());
            registry.Register(new NetListeningHandler());
            registry.Register(new NetPeerCountHandler());
            registry.Register(new Web3ClientVersionHandler());
            registry.Register(new Web3Sha3Handler());

            registry.Register(new EthNewFilterHandler());
            registry.Register(new EthGetFilterChangesHandler());
            registry.Register(new EthGetFilterLogsHandler());
            registry.Register(new EthUninstallFilterHandler());
            registry.Register(new EthNewBlockFilterHandler());

            registry.Register(new EthBlobBaseFeeHandler());
            registry.Register(new EthConfigHandler());
            registry.Register(new EthSimulateV1Handler());
            registry.Register(new EthCapabilitiesHandler());

            registry.Register(new TxpoolStatusHandler());
            registry.Register(new TxpoolContentHandler());
            registry.Register(new TxpoolContentFromHandler());

            registry.Register(new DebugTraceTransactionHandler());
            registry.Register(new DebugTraceCallHandler());
            registry.Register(new DebugTraceBlockByNumberHandler());
            registry.Register(new DebugTraceBlockByHashHandler());
            registry.Register(new DebugGetRawHeaderHandler());
            registry.Register(new DebugGetRawBlockHandler());
            registry.Register(new DebugGetRawReceiptsHandler());
            registry.Register(new DebugGetRawTransactionHandler());

            return registry;
        }
    }
}
