using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ethereum.RPC
{
    public enum ApiMethods
    {
        net_listening, //done
        eth_protocolVersion, //done
        eth_syncing, //done
        eth_coinbase, //done
        eth_mining, //done
        eth_hashrate, //done
        eth_gasPrice, //done
        eth_accounts, //done
        eth_blockNumber, //done
        eth_getBalance, //done
        eth_getStorageAt, //done
        eth_getTransactionCount, //done
        eth_getBlockTransactionCountByHash, //done
        eth_getBlockTransactionCountByNumber, //done
        eth_getUncleCountByBlockHash, //done
        eth_getUncleCountByBlockNumber, //done
        eth_getCode, //done
        eth_sign, //done
        eth_sendTransaction, //done
        eth_sendRawTransaction, //done
        eth_call, //done
        eth_estimateGas, //done
        eth_getBlockByHash, //done
        eth_getBlockByNumber,
        eth_getTransactionByHash, //done
        eth_getTransactionByBlockHashAndIndex, //done
        eth_getTransactionByBlockNumberAndIndex, //done
        eth_getTransactionReceipt, //done
        eth_getUncleByBlockHashAndIndex,
        eth_getUncleByBlockNumberAndIndex,
        eth_getCompilers, //done
        eth_compileLLL,
        eth_compileSolidity, //done
        eth_compileSerpent,
        eth_newFilter, //done
        eth_newBlockFilter,
        eth_newPendingTransactionFilter,
        eth_uninstallFilter,
        eth_getFilterChanges, //done
        eth_getFilterLogs,
        eth_getLogs,
        eth_getWork,
        eth_submitWork,
        eth_submitHashrate,
        shh_post,
        shh_version, //done
        shh_newIdentity, //done
        shh_hasIdentity,
        shh_newGroup,
        shh_addToGroup,
        shh_newFilter,
        shh_uninstallFilter,
        shh_getFilterChanges,
        shh_getMessages,
        web3_clientVersion, //done
        web3_sha3, //done
        net_version, //done
        net_peerCount //done
    }
}
