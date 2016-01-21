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
        eth_getBalance,
        eth_getStorageAt,
        eth_getTransactionCount,
        eth_getBlockTransactionCountByHash,
        eth_getBlockTransactionCountByNumber,
        eth_getUncleCountByBlockHash,
        eth_getUncleCountByBlockNumber,
        eth_getCode,
        eth_sign,
        eth_sendTransaction, //done
        eth_sendRawTransaction,
        eth_call,
        eth_estimateGas,
        eth_getBlockByHash,
        eth_getBlockByNumber,
        eth_getTransactionByHash,
        eth_getTransactionByBlockHashAndIndex,
        eth_getTransactionByBlockNumberAndIndex,
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
        db_putString,
        db_getString,
        db_putHex,
        db_getHex,
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
