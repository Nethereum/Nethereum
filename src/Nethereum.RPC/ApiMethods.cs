﻿namespace Nethereum.RPC
{
    public enum UnsupportedApiMethods {
        eth_signTransaction,
        engine_exchangeCapabilities,
        engine_exchangeTransitionConfigurationV1,
        engine_forkchoiceUpdatedV1,
        engine_forkchoiceUpdatedV2,
        engine_forkchoiceUpdatedV3,
        engine_getPayloadBodiesByHashV1,
        engine_getPayloadBodiesByRangeV1,
        engine_getPayloadV1,
        engine_getPayloadV2,
        engine_getPayloadV3,
        engine_newPayloadV1,
        engine_newPayloadV2,
        engine_newPayloadV3,
    }
    public enum ApiMethods
    {
        net_listening,
        net_version,
        net_peerCount,
        eth_chainId,
        eth_protocolVersion,
        eth_syncing,
        eth_coinbase,
        eth_mining,
        eth_hashrate,
        eth_gasPrice,
        eth_feeHistory,
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
        eth_sendTransaction,
        eth_sendRawTransaction,
        eth_call,
        eth_estimateGas,
        eth_getBlockByHash,
        eth_getBlockByNumber,
        eth_getBlockReceipts,
        eth_getTransactionByHash,
        eth_getTransactionByBlockHashAndIndex,
        eth_getTransactionByBlockNumberAndIndex,
        eth_getTransactionReceipt,
        eth_getUncleByBlockHashAndIndex,
        eth_getUncleByBlockNumberAndIndex,
        eth_getCompilers,
        eth_compileLLL,
        eth_compileSolidity,
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
        eth_subscribe,
        eth_unsubscribe,
        shh_version,
        shh_info,
        shh_setMaxMessageSize,
        shh_setMinPoW,
        shh_markTrustedPeer,
        shh_newKeyPair,
        shh_addPrivateKey,
        shh_deleteKeyPair,
        shh_hasKeyPair,
        shh_getPublicKey,
        shh_getPrivateKey,
        shh_newSymKey,
        shh_addSymKey,
        shh_generateSymKeyFromPassword,
        shh_hasSymKey,
        shh_getSymKey,
        shh_deleteSymKey,
        shh_subscribe,
        shh_unsubscribe,
        shh_newMessageFilter,
        shh_deleteMessageFilter,
        shh_getFilterMessages,
        shh_post,
        web3_clientVersion,
        web3_sha3,
        personal_listAccounts,
        personal_newAccount,
        personal_unlockAccount,
        personal_lockAccount,
        personal_sendTransaction,
        eth_getProof,
        eth_createAccessList,
        eth_maxPriorityFeePerGas,
        debug_getRawTransaction,
        debug_getBadBlocks,
        debug_getRawBlock,
        debug_getRawHeader,
        debug_getRawReceipts,
        debug_storageRangeAt,

        //wallet
        eth_requestAccounts,
        wallet_requestPermissions,
        wallet_getPermissions,
        wallet_addEthereumChain,
        wallet_switchEthereumChain,
        wallet_watchAsset,
        eth_signTypedData_v4,
        personal_sign,
        eth_sendUserOperation,
        eth_getUserOperationReceipt,
        eth_supportedEntryPoints,
        eth_getUserOperationByHash,
        eth_estimateUserOperationGas
    }
}