# geth-ancient-txs — real geth freezer slice WITH transactions and logs

A **self-consistent 257-item mini-freezer** rebased to item 0 = **block 1,500,000** (blocks 1,500,000..
1,500,256), for Part B conformance that needs real transactions, receipts, and **logs** — which the
genesis-region `geth-ancient/` slice (pre-block-46147, empty bodies) does not have.

## Why a second slice

`geth-ancient/` is the genesis region (empty bodies/receipts, empty-BAL placeholders) — perfect for the format
primitives, useless for transaction/receipt/log conformance. This slice sits in the ~2016 token era: block
1.5M has ~122 logs per 100 blocks (confirmed via `eth_getLogs` on the NUC geth), real multi-tx bodies, and
contract-creation txs — the shapes `BodyClusterItemCodec`, `ReceiptsItemCodec`, `ReceiptFieldDeriver`
(Bloom/TxHash/GasUsed/ContractAddress/LogIndex), and the filtermaps renderer need.

## Contents (per table: headers, bodies, receipts, hashes, bals)

- `<table>.cidx`/`hashes.ridx` — 258 entries (257 items + sentinel), REBASED so entry 0 = (0,0) and item 0 =
  block 1,500,000.
- `<table>.0000.cdat`/`hashes.0000.rdat` — the item bytes, re-concatenated into a single file 0 (the source
  range crossed geth data-file boundaries; the extractor read each item via geth's `bounds()` rule and
  rebased offsets).
- `<table>.meta` — a CLEAN consistent meta `c5 02 80 82 06 0c` = Version 2, VirtualTail 0, FlushOffset 1548
  (= index byte length). (Unlike `geth-ancient/`, whose meta carries the full production FlushOffset — both
  shapes are useful; this one is the clean-flush case, that one is the index<flushOffset case.)

## Provenance

Extracted from the NUC mainnet geth (v1.17) 2026-08-31 via a rebasing extractor (single output file per table;
offsets recomputed cumulatively). Item 0 = block 1,500,000; pre-EIP-7928 so `bals` items are still the 1-byte
`0x00` empty placeholder. ~378 KB total.

## To regenerate / pull a different range

`scratchpad/extract-txs-slice.py` (cross-file capable): set `START`/`END`, run on the NUC against
`/geth-data/geth/chaindata/ancient/chain`, tar the output. For a heavier-log era use block 4M (~1800 logs/100
blocks) or 6M (~7600), at the cost of larger blocks.
