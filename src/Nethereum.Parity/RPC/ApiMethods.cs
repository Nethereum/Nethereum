namespace Nethereum.Parity.RPC
{
    public enum ApiMethods
    {
        parity_consensusCapability,
        parity_decryptMessage,
        parity_encryptMessage,
        parity_futureTransactions,
        parity_listOpenedVaults,
        parity_listStorageKeys,
        parity_listVaults,
        parity_localTransactions,
        parity_pendingTransactionsStats,
        parity_releasesInfo,
        parity_versionInfo,

        //Account Vaults
        parity_changeVault,
        parity_changeVaultPassword,
        parity_closeVault,
        parity_getVaultMeta,
        parity_newVault,
        parity_openVault,
        parity_setVaultMeta,

        //Accounts (read-only) and Signatures
        parity_accountsInfo,
        parity_checkRequest,
        parity_defaultAccount,
        parity_generateSecretPhrase,
        parity_hardwareAccountsInfo,
        parity_listAccounts,
        parity_phraseToAddress,
        parity_postSign,
        parity_postTransaction,

        //Block Authoring (aka "mining")
        parity_defaultExtraData,
        parity_extraData,
        parity_gasCeilTarget,
        parity_gasFloorTarget,
        parity_minGasPrice,
        parity_transactionsLimit,

        //Development
        parity_devLogs,
        parity_devLogsLevels,

        //Network Information
        parity_chainStatus,
        parity_gasPriceHistogram,
        parity_netChain,
        parity_netPeers,
        parity_netPort,
        parity_nextNonce,
        parity_pendingTransactions,
        parity_registryAddress,
        parity_rpcSettings,
        parity_unsignedTransactionsCount,

        //Node Settings
        parity_dappsInterface,
        parity_dappsPort,
        parity_enode,
        parity_mode,
        parity_nodeName,
        parity_signerPort,
        trace_block,
        trace_get,
        trace_transaction,
        trace_filter,
        trace_call,
        trace_rawTransaction
    }
}