package main

import (
	"bytes"
	"fmt"

	"github.com/ethereum/go-ethereum/core/types"
	"github.com/ethereum/go-ethereum/eth/protocols/eth"
	"github.com/ethereum/go-ethereum/eth/protocols/snap"
	"github.com/ethereum/go-ethereum/rlp"
)

func envelope(data []byte, into any) (response, bool) {
	if err := rlp.DecodeBytes(data, into); err != nil {
		return fail(err), false
	}
	return response{}, true
}

func countOnly(count int) response {
	return response{OK: true, Extra: map[string]any{"count": count, "items_ok": true}}
}

func withCount(r response, count int) response {
	if r.Extra == nil {
		r.Extra = map[string]any{}
	}
	r.Extra["count"] = count
	return r
}

func ethTransactions(data []byte) response {
	var p eth.TransactionsPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	txs, err := p.Items()
	if err != nil {
		return withCount(itemsFailed(err), p.Len())
	}
	r := transactionList(txs)
	if p.Len() > 5000 {
		r.Extra["policy_refuse"] = "too many transactions (eth/protocols/eth/handlers.go:628)"
	} else if r.Extra["has_blob"] == true {
		r.Extra["policy_refuse"] = "disallowed broadcast blob transaction (eth/handler_eth.go:96)"
	} else if r.Extra["duplicate"] == true {
		r.Extra["policy_refuse"] = "multiple copies of the same hash (eth/handler_eth.go:107)"
	}
	return r
}

func ethPooledTransactions(data []byte) response {
	var p eth.PooledTransactionsPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	txs, err := p.List.Items()
	if err != nil {
		return withCount(itemsFailed(err), p.List.Len())
	}
	r := transactionList(txs)
	r.Extra["id"] = p.RequestId
	if r.Extra["duplicate"] == true {
		r.Extra["policy_refuse"] = "multiple copies of the same hash (eth/handler_eth.go:107)"
	}
	return r
}

func transactionList(txs []*types.Transaction) response {
	hashes := make([]string, len(txs))
	seen := map[string]bool{}
	extra := map[string]any{"count": len(txs), "items_ok": true}
	for i, tx := range txs {
		hashes[i] = hx(tx.Hash().Bytes())
		if seen[hashes[i]] {
			extra["duplicate"] = true
		}
		seen[hashes[i]] = true
		if tx.Type() == types.BlobTxType {
			extra["has_blob"] = true
		}
	}
	extra["hashes"] = hashes
	return response{OK: true, Extra: extra}
}

func ethBlockHeaders(data []byte) response {
	var p eth.BlockHeadersPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	headers, err := p.List.Items()
	if err != nil {
		return withCount(itemsFailed(err), p.List.Len())
	}
	hashes := make([]string, len(headers))
	for i, h := range headers {
		hashes[i] = hx(h.Hash().Bytes())
	}
	return response{OK: true, Extra: map[string]any{"count": len(headers), "items_ok": true, "hashes": hashes, "id": p.RequestId}}
}

func ethBlockBodies(data []byte) response {
	var p eth.BlockBodiesPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	bodies, err := p.List.Items()
	if err != nil {
		return withCount(itemsFailed(err), p.List.Len())
	}
	canonicals := make([]string, len(bodies))
	extra := map[string]any{"count": len(bodies), "items_ok": true, "id": p.RequestId}
	for i := range bodies {
		r := completeBody(&bodies[i])
		if r.Err != "" {
			return withCount(r, len(bodies))
		}
		canonicals[i] = r.Canonical
		if reason, ok := r.Extra["policy_refuse"]; ok {
			extra["policy_refuse"] = reason
		}
	}
	extra["bodies"] = canonicals
	return response{OK: true, Extra: extra}
}

func receiptsMessage(requestId uint64, list *rlpReceiptLists, incomplete *bool) response {
	items, err := list.Items()
	if err != nil {
		return withCount(itemsFailed(err), list.Len())
	}
	extra := map[string]any{"count": len(items), "items_ok": true, "id": requestId}
	if incomplete != nil {
		extra["incomplete"] = *incomplete
	}
	for i, item := range items {
		r := receiptListItems(item, false)
		if r.Err != "" {
			extra["items_ok"] = false
			return response{OK: true, Err: fmt.Sprintf("list %d: %s", i, r.Err), Extra: extra}
		}
	}
	return response{OK: true, Extra: extra}
}

type rlpReceiptLists = rlp.RawList[*eth.ReceiptList]

func ethReceipts69(data []byte) response {
	var p eth.ReceiptsPacket69
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return receiptsMessage(p.RequestId, &p.List, nil)
}

func ethReceipts70(data []byte) response {
	var p eth.ReceiptsPacket70
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return receiptsMessage(p.RequestId, &p.List, &p.LastBlockIncomplete)
}

func ethBlockAccessLists(data []byte) response {
	var p eth.BlockAccessListPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	items, err := p.List.Items()
	if err != nil {
		r := withCount(itemsFailed(err), p.List.Len())
		r.Err = "items: " + balEntryFailure(&p.List) + ": " + err.Error()
		return r
	}
	return response{OK: true, Extra: map[string]any{"count": len(items), "items_ok": true, "id": p.RequestId}}
}

func balEntryFailure(list *rlp.RawList[eth.RawBlockAccessList]) string {
	it := list.ContentIterator()
	for it.Next() {
		kind, content, _, err := rlp.Split(it.Value())
		if err == nil && kind != rlp.List {
			if len(content) == 0 {
				return "unavailable marker (empty string entry)"
			}
			return "non-list entry"
		}
	}
	return "entry"
}

func ethNewPooledTransactionHashes(data []byte) response {
	var p eth.NewPooledTransactionHashesPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	if len(p.Hashes) != len(p.Types) || len(p.Hashes) != len(p.Sizes) {
		return response{Err: fmt.Sprintf("invalid len of fields in %d %d %d", len(p.Hashes), len(p.Types), len(p.Sizes))}
	}
	return countOnly(len(p.Hashes))
}

func ethBlockRangeUpdate(data []byte) response {
	var p eth.BlockRangeUpdatePacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	r := response{OK: true, Canonical: encoded(&p)}
	if err := p.Validate(); err != nil {
		r.Extra = map[string]any{"policy_refuse": err.Error() + " (eth/protocols/eth/handlers.go:661)"}
	}
	return r
}

func ethStatus(data []byte) response {
	var p eth.StatusPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	r := response{OK: true, Canonical: encoded(&p)}
	initRange := eth.BlockRangeUpdatePacket{EarliestBlock: p.EarliestBlock, LatestBlock: p.LatestBlock, LatestBlockHash: p.LatestBlockHash}
	if err := initRange.Validate(); err != nil {
		r.Extra = map[string]any{"policy_refuse": "invalid block range in status: " + err.Error() + " (eth/protocols/eth/handshake.go:93)"}
	}
	return r
}

func ethGetBlockHeaders(data []byte) response {
	var p eth.GetBlockHeadersPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return response{OK: true, Canonical: encoded(&p)}
}

func ethGetBlockBodies(data []byte) response {
	var p eth.GetBlockBodiesPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return withCount(response{OK: true, Canonical: encoded(&p)}, len(p.GetBlockBodiesRequest))
}

func ethGetPooledTransactions(data []byte) response {
	var p eth.GetPooledTransactionsPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return withCount(response{OK: true, Canonical: encoded(&p)}, len(p.GetPooledTransactionsRequest))
}

func ethGetReceipts69(data []byte) response {
	var p eth.GetReceiptsPacket69
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return withCount(response{OK: true, Canonical: encoded(&p)}, len(p.GetReceiptsRequest))
}

func ethGetReceipts70(data []byte) response {
	var p eth.GetReceiptsPacket70
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return withCount(response{OK: true, Canonical: encoded(&p)}, len(p.GetReceiptsRequest))
}

func ethGetBlockAccessLists(data []byte) response {
	var p eth.GetBlockAccessListsPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return withCount(response{OK: true, Canonical: encoded(&p)}, len(p.GetBlockAccessListsRequest))
}

type snapAccountRangeInput struct {
	ID       uint64
	Accounts rlp.RawList[*snap.AccountData]
	Proof    rlp.RawList[[]byte]
}

type snapStorageRangesInput struct {
	ID    uint64
	Slots rlp.RawList[[]*snap.StorageData]
	Proof rlp.RawList[[]byte]
}

type snapByteCodesInput struct {
	ID    uint64
	Codes rlp.RawList[[]byte]
}

type snapTrieNodesInput struct {
	ID    uint64
	Nodes rlp.RawList[[]byte]
}

func snapAccountRange(data []byte) response {
	var p snapAccountRangeInput
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	if p.Proof.Len() > 128 {
		return response{Err: fmt.Sprintf("invalid proof (length %d)", p.Proof.Len())}
	}
	accounts, err := p.Accounts.Items()
	if err != nil {
		return withCount(itemsFailed(err), p.Accounts.Len())
	}
	if _, err := p.Proof.Items(); err != nil {
		return withCount(itemsFailed(err), p.Accounts.Len())
	}
	unpacked := &snap.AccountRangePacket{ID: p.ID, Accounts: accounts}
	if _, _, err := unpacked.Unpack(); err != nil {
		return withCount(itemsFailed(err), len(accounts))
	}
	monotonic := true
	for i := 1; i < len(accounts); i++ {
		if bytes.Compare(accounts[i-1].Hash[:], accounts[i].Hash[:]) >= 0 {
			monotonic = false
		}
	}
	return response{OK: true, Extra: map[string]any{"count": len(accounts), "items_ok": true, "id": p.ID, "monotonic": monotonic, "proof": p.Proof.Len()}}
}

func snapStorageRanges(data []byte) response {
	var p snapStorageRangesInput
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	if p.Proof.Len() > 128 {
		return response{Err: fmt.Sprintf("invalid proof (length %d)", p.Proof.Len())}
	}
	lists, err := p.Slots.Items()
	if err != nil {
		return withCount(itemsFailed(err), p.Slots.Len())
	}
	if _, err := p.Proof.Items(); err != nil {
		return withCount(itemsFailed(err), p.Slots.Len())
	}
	monotonic := true
	for _, slots := range lists {
		for j := 1; j < len(slots); j++ {
			if bytes.Compare(slots[j-1].Hash[:], slots[j].Hash[:]) >= 0 {
				monotonic = false
			}
		}
	}
	return response{OK: true, Extra: map[string]any{"count": len(lists), "items_ok": true, "id": p.ID, "monotonic": monotonic, "proof": p.Proof.Len()}}
}

func snapByteCodes(data []byte) response {
	var p snapByteCodesInput
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	codes, err := p.Codes.Items()
	if err != nil {
		return withCount(itemsFailed(err), p.Codes.Len())
	}
	return response{OK: true, Extra: map[string]any{"count": len(codes), "items_ok": true, "id": p.ID}}
}

func snapTrieNodes(data []byte) response {
	var p snapTrieNodesInput
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	nodes, err := p.Nodes.Items()
	if err != nil {
		return withCount(itemsFailed(err), p.Nodes.Len())
	}
	return response{OK: true, Extra: map[string]any{"count": len(nodes), "items_ok": true, "id": p.ID}}
}

func snapAccessLists(data []byte) response {
	var p snap.AccessListsPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return response{OK: true, Extra: map[string]any{"count": p.AccessLists.Len(), "items_ok": true, "id": p.ID}}
}

func snapGetAccountRange(data []byte) response {
	var p snap.GetAccountRangePacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return response{OK: true, Canonical: encoded(&p)}
}

func snapGetStorageRanges(data []byte) response {
	var p snap.GetStorageRangesPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return withCount(response{OK: true, Canonical: encoded(&p)}, len(p.Accounts))
}

func snapGetByteCodes(data []byte) response {
	var p snap.GetByteCodesPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return withCount(response{OK: true, Canonical: encoded(&p)}, len(p.Hashes))
}

func snapGetAccessLists(data []byte) response {
	var p snap.GetAccessListsPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	return withCount(response{OK: true, Canonical: encoded(&p)}, len(p.Hashes))
}

func snapGetTrieNodes(data []byte) response {
	var p snap.GetTrieNodesPacket
	if r, ok := envelope(data, &p); !ok {
		return r
	}
	count := 0
	it := p.Paths.ContentIterator()
	for it.Next() {
		count++
		inner, err := rlp.NewListIterator(it.Value())
		if err != nil {
			return withCount(itemsFailed(err), p.Paths.Len())
		}
		if inner.Count() == 0 {
			return withCount(itemsFailed(fmt.Errorf("zero-item pathset requested")), p.Paths.Len())
		}
		for inner.Next() {
			if _, _, err := rlp.SplitString(inner.Value()); err != nil {
				return withCount(itemsFailed(err), p.Paths.Len())
			}
		}
	}
	return response{OK: true, Extra: map[string]any{"count": count, "items_ok": true, "id": p.ID, "bytes": p.Bytes}}
}
