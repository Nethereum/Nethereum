namespace Nethereum.Geth.RPC
{
    public enum ApiMethods
    {
        admin_addPeer,
        admin_removePeer,
        admin_addTrustedPeer,
        admin_removeTrustedPeer,
        admin_importChain,
        admin_exportChain,
        /*
        admin_sleepBlocks
        */
        admin_startHTTP,
        admin_stopHTTP,
        admin_startRPC,
        admin_stopRPC,
        admin_startWS,
        admin_stopWS,

        admin_nodeInfo,
        admin_datadir,
        admin_peers,
        
        admin_setSolc,

        debug_blockProfile,
        debug_backtraceAt,
        debug_dumpBlock,
        debug_cpuProfile,
        debug_gcStats,
        debug_getBlockRlp,
        debug_goTrace,
        debug_memStats,
        debug_seedHash,
        debug_setBlockProfileRate,
        debug_stacks,
        debug_startCPUProfile,
        debug_startGoTrace,
        debug_stopCPUProfile,
        debug_stopGoTrace,
        debug_traceBadBlock,
        debug_traceBlock,
        debug_traceBlockByNumber,
        debug_traceBlockByHash,
        debug_traceBlockFromFile,
        debug_traceTransaction,
        debug_traceCall,
        debug_verbosity,
        debug_vmodule,

        eth_pendingTransactions,
        eth_call,

        miner_hashrate,
        miner_start,
        miner_stop,
        miner_setGasPrice,
        txpool_content,
        txpool_inspect,
        txpool_status,
      


        
    }
}