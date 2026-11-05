package main

import "sort"

var kinds = map[string]func([]byte) response{
	// core/types/transaction.go Transaction.UnmarshalBinary
	"tx_binary": txBinary,
	// core/types/transaction.go Transaction.DecodeRLP through rlp.DecodeBytes
	"tx_rlp": txRLP,
	// core/types/block.go Header through rlp.DecodeBytes
	"header": headerKind,
	// core/types/block.go Block.DecodeRLP
	"block": blockKind,
	// eth/protocols/eth/protocol.go BlockBody, elements as eth/downloader/queue.go:591-602 decode them
	"body": bodyKind,
	// core/types/withdrawal.go Withdrawal
	"withdrawal": withdrawalKind,
	// core/types/log.go Log
	"log": logKind,
	// core/types/receipt.go Receipt.UnmarshalBinary
	"receipt_binary": receiptBinary,
	// core/types/receipt.go Receipt.DecodeRLP
	"receipt_rlp": receiptRLP,
	// core/types/receipt.go ReceiptForStorage.DecodeRLP
	"receipt_storage": receiptStorage,
	// eth/protocols/eth/receipt.go Receipt.decode through ReceiptList.Derivable
	"receipt_eth69": receiptEth69,
	// core/types/bal/bal_encoding.go BlockAccessList.DecodeRLP
	"bal": balKind,

	// eth/protocols/eth/handlers.go:49 handleStatus path, StatusPacket
	"eth_Status": ethStatus,
	// eth/protocols/eth/handlers.go:618 handleTransactions and eth/handler_eth.go:65 Items
	"eth_Transactions": ethTransactions,
	// eth/protocols/eth/handlers.go:35 handleGetBlockHeaders
	"eth_GetBlockHeaders": ethGetBlockHeaders,
	// eth/protocols/eth/handlers.go:348 handleBlockHeaders
	"eth_BlockHeaders": ethBlockHeaders,
	// eth/protocols/eth/handlers.go:218 handleGetBlockBodies
	"eth_GetBlockBodies": ethGetBlockBodies,
	// eth/protocols/eth/handlers.go:377 handleBlockBodies and eth/downloader/queue.go:591
	"eth_BlockBodies": ethBlockBodies,
	// eth/protocols/eth/handlers.go:565 handleNewPooledTransactionHashes
	"eth_NewPooledTransactionHashes": ethNewPooledTransactionHashes,
	// eth/protocols/eth/handlers.go:585 handleGetPooledTransactions
	"eth_GetPooledTransactions": ethGetPooledTransactions,
	// eth/protocols/eth/handlers.go:634 handlePooledTransactions and eth/handler_eth.go:75 Items
	"eth_PooledTransactions": ethPooledTransactions,
	// eth/protocols/eth/handlers.go:251 handleGetReceipts69
	"eth_GetReceipts69": ethGetReceipts69,
	// eth/protocols/eth/handlers.go:261 handleGetReceipts70
	"eth_GetReceipts70": ethGetReceipts70,
	// eth/protocols/eth/handlers.go:489 handleReceipts69
	"eth_Receipts69": ethReceipts69,
	// eth/protocols/eth/handlers.go:509 handleReceipts70
	"eth_Receipts70": ethReceipts70,
	// eth/protocols/eth/handlers.go:657 handleBlockRangeUpdate
	"eth_BlockRangeUpdate": ethBlockRangeUpdate,
	// eth/protocols/eth/handlers.go:671 handleGetBlockAccessLists
	"eth_GetBlockAccessLists": ethGetBlockAccessLists,
	// eth/protocols/eth/handlers.go:706 handleBlockAccessLists
	"eth_BlockAccessLists": ethBlockAccessLists,

	// eth/protocols/snap/handlers.go:38 handleGetAccountRange
	"snap_GetAccountRange": snapGetAccountRange,
	// eth/protocols/snap/handlers.go:121 handleAccountRange
	"snap_AccountRange": snapAccountRange,
	// eth/protocols/snap/handlers.go:156 handleGetStorageRanges
	"snap_GetStorageRanges": snapGetStorageRanges,
	// eth/protocols/snap/handlers.go:293 handleStorageRanges
	"snap_StorageRanges": snapStorageRanges,
	// eth/protocols/snap/handlers.go:330 handleGetByteCodes
	"snap_GetByteCodes": snapGetByteCodes,
	// eth/protocols/snap/handlers.go:375 handleByteCodes
	"snap_ByteCodes": snapByteCodes,
	// eth/protocols/snap/handlers.go:395 handleGetTrienodes, ServiceGetTrieNodesQuery pathset walk
	"snap_GetTrieNodes": snapGetTrieNodes,
	// eth/protocols/snap/handlers.go:538 handleTrieNodes
	"snap_TrieNodes": snapTrieNodes,
	// eth/protocols/snap/handlers.go:556 handleGetAccessLists
	"snap_GetAccessLists": snapGetAccessLists,
	// eth/protocols/snap/handlers.go:601 handleAccessLists
	"snap_AccessLists": snapAccessLists,
}

func kindNames() []string {
	names := make([]string, 0, len(kinds))
	for name := range kinds {
		names = append(names, name)
	}
	sort.Strings(names)
	return names
}
