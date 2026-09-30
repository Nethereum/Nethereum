# geth-ancient — real go-ethereum freezer conformance corpus

A **self-consistent 2048-item slice** of a real mainnet go-ethereum chain freezer
(`/geth-data/geth/chaindata/ancient/chain/`), used to prove `Nethereum.Freezer` reads geth's on-disk format
byte-for-byte.

## Contents (per table: headers, bodies, receipts, hashes, bals)

- `<table>.cidx` / `hashes.ridx` — the fixed 6-byte-entry index, first **2048 entries** (2047 items + sentinel).
- `<table>.0000.cdat` / `hashes.0000.rdat` — the data file, **truncated to the last index entry's offset** so it
  contains exactly items 0..2046 and stays consistent with the index.
- `<table>.meta` — the REAL 3-field meta as captured (`c7 02 80 84 …` → Version 2, VirtualTail 0, FlushOffset
  ≈ 155 M). NOTE: FlushOffset reflects the FULL production file, so it is far larger than this truncated slice —
  this is the genuine "index shorter than flushOffset" case, which read-only `Validate` accepts and append
  `Repair` rewinds (spec §A8); a useful conformance shape, not an inconsistency.

## Provenance

Captured from the NUC mainnet geth (v1.17) 2026-08-31; genesis region (blocks 0..2046). Early blocks are
empty (bodies ≈ 5-byte, receipts ≈ 1-byte, bals = 1-byte `0x00` empty-BAL placeholder — confirming bals is
lockstep from genesis, spec §15e). Trimmed from a 6 MB/table raw pull to ~900 KB total for repo hygiene.

## Limitation / TODO

This slice is pre-transaction (mainnet txs start ~block 46147), so bodies/receipts here carry no txs/logs.
Receipt-field-derivation and filtermaps conformance (impl-plan Tasks 10/11/14+) need a **richer slice from a
post-46147 region** (with txs + logs) — pull that when those tasks land, as a second `geth-ancient-txs/` fixture.
