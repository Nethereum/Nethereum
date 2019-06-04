namespace Nethereum.Pantheon
{
    public enum ApiMethods
    {
        admin_addPeer,
        admin_nodeInfo,
        admin_peers,
        admin_removePeer,


        clique_discard,
        clique_getSigners,
        clique_getSignersAtHash,
        clique_propose,
        clique_proposals,
        debug_storageRangeAt,
        debug_metrics,
        debug_traceTransaction,
        miner_start,
        miner_stop,
        ibft_getPendingVotes,
        ibft_discardValidatorVote,
        ibft_proposeValidatorVote,
        ibft_getValidatorsByBlockNumber,
        ibft_getValidatorsByBlockHash,
        perm_addAccountsToWhitelist,
        perm_addNodesToWhitelist,
        perm_getAccountsWhitelist,
        perm_getNodesWhitelist,
        perm_reloadPermissionsFromFile,
        perm_removeAccountsFromWhitelist,
        perm_removeNodesFromWhitelist,
        txpool_pantheonStatistics,
        txpool_pantheonTransactions,
        eea_getTransactionReceipt,
        eea_sendRawTransaction
    }
}