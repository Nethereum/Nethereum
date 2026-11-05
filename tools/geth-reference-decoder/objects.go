package main

import (
	"bytes"
	"errors"
	"fmt"

	"github.com/ethereum/go-ethereum/core/types"
	"github.com/ethereum/go-ethereum/core/types/bal"
	"github.com/ethereum/go-ethereum/eth/protocols/eth"
	"github.com/ethereum/go-ethereum/rlp"
)

func fail(err error) response { return response{Err: err.Error()} }

func hx(b []byte) string { return fmt.Sprintf("%x", b) }

func encoded(v any) string {
	b, err := rlp.EncodeToBytes(v)
	if err != nil {
		return ""
	}
	return hx(b)
}

func itemsFailed(err error) response {
	return response{OK: true, Err: "items: " + err.Error(), Extra: map[string]any{"items_ok": false}}
}

func hasSidecar(txs []*types.Transaction) bool {
	for _, tx := range txs {
		if tx.Type() == types.BlobTxType && tx.BlobTxSidecar() != nil {
			return true
		}
	}
	return false
}

func senderOf(tx *types.Transaction, extra map[string]any) {
	defer func() {
		if r := recover(); r != nil {
			extra["sender_err"] = fmt.Sprint(r)
		}
	}()
	var signer types.Signer = types.HomesteadSigner{}
	if tx.Type() != types.LegacyTxType || tx.Protected() {
		signer = types.LatestSignerForChainID(tx.ChainId())
	}
	from, err := types.Sender(signer, tx)
	if err != nil {
		extra["sender_err"] = err.Error()
		return
	}
	extra["sender"] = hx(from.Bytes())
}

func describeTx(tx *types.Transaction) (string, map[string]any) {
	extra := map[string]any{"type": int(tx.Type())}
	senderOf(tx, extra)
	if sc := tx.BlobTxSidecar(); sc != nil {
		extra["sidecar"] = map[string]any{"version": int(sc.Version), "blobs": len(sc.Blobs), "commitments": len(sc.Commitments), "proofs": len(sc.Proofs)}
	}
	return hx(tx.Hash().Bytes()), extra
}

func txBinary(data []byte) response {
	var tx types.Transaction
	if err := tx.UnmarshalBinary(data); err != nil {
		return fail(err)
	}
	canonical, err := tx.MarshalBinary()
	if err != nil {
		return fail(err)
	}
	hash, extra := describeTx(&tx)
	return response{OK: true, Canonical: hx(canonical), Hash: hash, Extra: extra}
}

func txRLP(data []byte) response {
	var tx types.Transaction
	if err := rlp.DecodeBytes(data, &tx); err != nil {
		return fail(err)
	}
	hash, extra := describeTx(&tx)
	return response{OK: true, Canonical: encoded(&tx), Hash: hash, Extra: extra}
}

func headerKind(data []byte) response {
	var h types.Header
	if err := rlp.DecodeBytes(data, &h); err != nil {
		return fail(err)
	}
	return response{OK: true, Canonical: encoded(&h), Hash: hx(h.Hash().Bytes())}
}

func blockKind(data []byte) response {
	var b types.Block
	if err := rlp.DecodeBytes(data, &b); err != nil {
		return fail(err)
	}
	extra := map[string]any{"txs": len(b.Transactions()), "uncles": len(b.Uncles())}
	if b.Withdrawals() != nil {
		extra["withdrawals"] = len(b.Withdrawals())
	}
	if hasSidecar(b.Transactions()) {
		extra["policy_refuse"] = "blob sidecar in block body (core/block_validator.go:95)"
	}
	return response{OK: true, Canonical: encoded(&b), Hash: hx(b.Hash().Bytes()), Extra: extra}
}

func bodyKind(data []byte) response {
	var body eth.BlockBody
	if err := rlp.DecodeBytes(data, &body); err != nil {
		return fail(err)
	}
	return completeBody(&body)
}

func completeBody(body *eth.BlockBody) response {
	txs, err := body.Transactions.Items()
	if err != nil {
		return itemsFailed(err)
	}
	uncles, err := body.Uncles.Items()
	if err != nil {
		return itemsFailed(err)
	}
	plain := types.Body{Transactions: txs, Uncles: uncles}
	extra := map[string]any{"items_ok": true, "txs": len(txs), "uncles": len(uncles)}
	if hasSidecar(txs) {
		extra["policy_refuse"] = "blob sidecar in block body (core/block_validator.go:95)"
	}
	if body.Withdrawals != nil {
		wds, err := body.Withdrawals.Items()
		if err != nil {
			return itemsFailed(err)
		}
		plain.Withdrawals = wds
		extra["withdrawals"] = len(wds)
	}
	return response{OK: true, Canonical: encoded(&plain), Extra: extra}
}

func withdrawalKind(data []byte) response {
	var w types.Withdrawal
	if err := rlp.DecodeBytes(data, &w); err != nil {
		return fail(err)
	}
	return response{OK: true, Canonical: encoded(&w)}
}

func logKind(data []byte) response {
	var l types.Log
	if err := rlp.DecodeBytes(data, &l); err != nil {
		return fail(err)
	}
	return response{OK: true, Canonical: encoded(&l)}
}

func receiptSummary(r *types.Receipt, canonical []byte) response {
	extra := map[string]any{"type": int(r.Type), "gas": r.CumulativeGasUsed, "logs": len(r.Logs)}
	if r.PostState != nil {
		extra["post_state"] = hx(r.PostState)
	} else {
		extra["status"] = r.Status
	}
	return response{OK: true, Canonical: hx(canonical), Extra: extra}
}

func receiptBinary(data []byte) response {
	var r types.Receipt
	if err := r.UnmarshalBinary(data); err != nil {
		return fail(err)
	}
	canonical, err := r.MarshalBinary()
	if err != nil {
		return fail(err)
	}
	return receiptSummary(&r, canonical)
}

func receiptRLP(data []byte) response {
	var r types.Receipt
	if err := rlp.DecodeBytes(data, &r); err != nil {
		return fail(err)
	}
	canonical, err := r.MarshalBinary()
	if err != nil {
		return fail(err)
	}
	return receiptSummary(&r, canonical)
}

func receiptStorage(data []byte) response {
	var r types.ReceiptForStorage
	if err := rlp.DecodeBytes(data, &r); err != nil {
		return fail(err)
	}
	return response{OK: true, Canonical: encoded(&r), Extra: map[string]any{"gas": r.CumulativeGasUsed, "logs": len(r.Logs)}}
}

func singleItemList(data []byte) ([]byte, error) {
	_, _, rest, err := rlp.Split(data)
	if err != nil {
		return nil, err
	}
	if len(rest) != 0 {
		return nil, fmt.Errorf("trailing bytes after value")
	}
	return rlp.EncodeToBytes([]rlp.RawValue{data})
}

func receiptEth69(data []byte) response {
	list, err := singleItemList(data)
	if err != nil {
		return fail(err)
	}
	var rl eth.ReceiptList
	if err := rlp.DecodeBytes(list, &rl); err != nil {
		return fail(err)
	}
	return receiptListItems(&rl, true)
}

func receiptListItems(rl *eth.ReceiptList, firstCanonical bool) response {
	derivable := rl.Derivable()
	extra := map[string]any{"items_ok": true, "count": derivable.Len()}
	var canonical string
	for i := 0; i < derivable.Len(); i++ {
		var buf bytes.Buffer
		derivable.EncodeIndex(i, &buf)
		if buf.Len() == 0 {
			extra["items_ok"] = false
			return response{OK: true, Err: fmt.Sprintf("receipt %d does not decode", i), Extra: extra}
		}
		var committed types.Receipt
		if err := committed.UnmarshalBinary(buf.Bytes()); err != nil && !errors.Is(err, types.ErrTxTypeNotSupported) {
			extra["items_ok"] = false
			return response{OK: true, Err: fmt.Sprintf("receipt %d root-committed encoding is not a receipt: %v", i, err), Extra: extra}
		}
		if firstCanonical && i == 0 {
			canonical = hx(buf.Bytes())
		}
	}
	if _, err := rl.EncodeForStorage(); err != nil {
		extra["items_ok"] = false
		return response{OK: true, Err: err.Error(), Extra: extra}
	}
	return response{OK: true, Canonical: canonical, Extra: extra}
}

func balKind(data []byte) response {
	var list bal.BlockAccessList
	if err := rlp.DecodeBytes(data, &list); err != nil {
		return fail(err)
	}
	return response{OK: true, Canonical: encoded(list), Hash: hx(list.Hash().Bytes()), Extra: map[string]any{"accounts": len(list)}}
}
