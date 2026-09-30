# filtermaps-vectors.json — provenance

Ground-truth `(value, mapIndex, layer, lvIndex) → (maskedMapIndex, rowIndex, columnIndex)` vectors for the
`LogValueHasher` conformance test (impl-plan Task 3, spec §C2, checklist #9).

## Why a harness, not an extract

go-ethereum `core/filtermaps/math_test.go` @ `bbb9119caaefbbf619363493e54cc8362d65188d` contains **no
hardcoded golden vectors** — `TestSingleMatch`/`TestPotentialMatches` are round-trip + statistical only, and
`rowIndex`/`columnIndex`/`maskedMapIndex` are never asserted against constants (verified 2026-08-31). A ported
C# round-trip would prove nothing (a shared bug agrees with itself). So these vectors are **emitted from geth's
real code**.

## How this file was generated

- Harness: `tools/filtermaps-vector-gen/main.go` — the four functions (`rowIndex`, `columnIndex`,
  `maskedMapIndex`, `addressValue`/`topicValue`) are copied **verbatim** from geth
  `core/filtermaps/math.go` @ `bbb9119c` (only change: `common.Hash`→`[32]byte`, `common.Address`→`[20]byte`,
  which are exactly those aliases in geth). Derived `Params` fields (mapHeight/valuesPerMap/baseRowLength) are
  the geth powers-of-two.
- Command: `cd tools/filtermaps-vector-gen && go run . > tests/Nethereum.Freezer.UnitTests/Fixtures/filtermaps-vectors.json`
- Go: 1.26.3. Params: geth `DefaultParams`.

## Coverage (80 vectors)

- 4 value sources: `addressValue` of two addresses, `topicValue` of two topics.
- Layers 0–4; mapIndexes {5, 1000, 2048, 100000} (5 and 1000 share an epoch → the layer-0 constant-per-epoch
  case: both yield identical `rowIndex`); lvIndexes spanning the `valuesPerMap=65536` boundary.
- Sanity asserted at generation: layer-0 `rowIndex` identical for mapIndex 5 and 1000 (masking = constant per
  epoch); every `columnIndex < 2^24` (mapWidth); every `rowIndex < 2^16` (mapHeight); high-column vectors
  present so `columnIndex`'s truncate-then-shift fold (`uint32(hash)>>(32-hashBits)`) is exercised.

## To regenerate / re-pin to a newer geth

Re-copy the four functions from the target commit's `math.go` into the harness, update the commit hash here,
re-run. If geth ever adds real golden vectors upstream, prefer those.
