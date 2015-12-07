using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ethereum.RPC
{
    public enum ApiMethods
    {
        net_listening,
        eth_protocolVersion,
        eth_syncing,
        eth_coinbase,
        eth_mining,
        eth_hashrate,
        eth_gasPrice,
        eth_accounts,
        eth_blockNumber,
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
        eth_getTransactionReceipt,
        eth_getUncleByBlockHashAndIndex,
        eth_getUncleByBlockNumberAndIndex,
        eth_getCompilers,
        eth_compileLLL,
        eth_compileSolidity, //done
        eth_compileSerpent,
        eth_newFilter,
        eth_newBlockFilter,
        eth_newPendingTransactionFilter,
        eth_uninstallFilter,
        eth_getFilterChanges,
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
        shh_version,
        shh_newIdentity,
        shh_hasIdentity,
        shh_newGroup,
        shh_addToGroup,
        shh_newFilter,
        shh_uninstallFilter,
        shh_getFilterChanges,
        shh_getMessages,
        web3_clientVersion, //done
        web3_sha3, //done
        net_version,
        net_peerCount
    }
}
