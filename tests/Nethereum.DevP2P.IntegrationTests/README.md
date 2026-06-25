# Nethereum.DevP2P.IntegrationTests

Integration tests that validate `Nethereum.DevP2P` end-to-end against go-ethereum's `cmd/devp2p` conformance harness — the canonical wire-protocol test suite used across the Ethereum client ecosystem.

## What this project asserts

Each conformance suite spawns the official `devp2p` binary as a black box; the .NET test starts a Nethereum listener, hands the binary its enode (or ENR) URL, and asserts the binary exits 0. Coverage as of the last validated run:

| Suite (binary subcommand) | C# test class | Pass |
|---------------------------|---------------|------|
| `devp2p discv4 test` | `Devp2pToolConformanceTests` | 15 / 15 |
| `devp2p rlpx snap-test` | `Devp2pSnapTestConformanceTests` | 5 / 5 |
| `devp2p rlpx eth-test` | `Devp2pEthTestConformanceTests` | 19 / 19 |
| `devp2p discv5 test` | `Devp2pDiscv5ConformanceTests` | 7 / 7 |

Sub-test breakdown per suite:

- **discv4** — BasicPing/WrongTo/WrongFrom/ExtraData/PastExpiration/WrongPacketType, BondThenPingWithWrongFrom, ENRRequest (EIP-868), Findnode/{WithoutEndpointProof,Basic,UnsolicitedNeighbors,PastExpiration}, InvalidPongHash, WrongIP (amplification-defence).
- **eth-test** — Status, MaliciousHandshake, GetBlockHeaders (3), GetNonexistentBlockHeaders, SimultaneousRequests, SameRequestID, ZeroRequestID, GetBlockBodies, GetReceipts, BlockRangeUpdate (3), Transaction, NewPooledTxs, InvalidTxs, LargeTxRequest, BlobViolations, BlobTxWithoutSidecar, BlobTxWithMismatchedSidecar.
- **snap-test** — Status, AccountRange, GetByteCodes, GetStorageRanges, GetTrieNodes.
- **discv5** — Ping, PingLargeRequestID, PingMultiIP, PingHandshakeInterrupted, TalkRequest, FindnodeZeroDistance, FindnodeResults.

## Reproducing the test environment

### 1. Pinned go-ethereum version

We validate against **go-ethereum `v1.16.4`** (commit `41714b4`, released 2025-09-25). Other releases may pass too, but the harness behaviour and the test fixtures (`chain.rlp`, `forkenv.json`, `headblock.json`, `genesis.json`, `headstate.json`) live in this version's `cmd/devp2p/internal/ethtest/testdata` and `cmd/devp2p/internal/snaptest/testdata`.

Clone alongside this repository so the test runner can find the testdata directories:

```bash
cd <repos>   # the parent directory that holds this repository
git clone https://github.com/ethereum/go-ethereum.git
cd go-ethereum
git checkout v1.16.4
```

### 2. Rebuild `devp2p.exe`

The C# tests look up `geth-tools/devp2p.exe` walking parent directories from `AppContext.BaseDirectory`. Build the binary into that location:

```powershell
# From the go-ethereum checkout:
$env:PATH = "C:\Program Files\Go\bin;" + $env:PATH
cd <repos>\go-ethereum
go build -o <path-to-Nethereum>\geth-tools\devp2p.exe .\cmd\devp2p\
```

Go 1.21+ is sufficient. The build pulls its own dependencies via Go modules.

### 3. Run the conformance suites

```bash
# Full sweep (~25 s on a recent laptop)
dotnet test tests/Nethereum.DevP2P.IntegrationTests \
    --filter "FullyQualifiedName~Devp2pDiscv5ConformanceTests | FullyQualifiedName~Devp2pToolConformanceTests | FullyQualifiedName~Devp2pEthTestConformanceTests | FullyQualifiedName~Devp2pSnapTestConformanceTests"

# Just discv5
dotnet test tests/Nethereum.DevP2P.IntegrationTests --filter "FullyQualifiedName~Devp2pDiscv5ConformanceTests"
```

## Two non-obvious discoveries surfaced by the harness

Both were caught by adding `fmt.Fprintf(os.Stderr, ...)` lines to the relevant `p2p/discover/v5wire` functions and rebuilding `devp2p.exe`. Documenting them here because the published spec wording disagrees with the reference implementation, and silently inheriting the spec wording will produce code that looks correct but doesn't interop.

### discv5 — AAD includes the masking-iv

`discv5-wire.md §"message-ad"` says the AES-GCM AAD is the unmasked header only and explicitly notes that the masking-iv is *not* additional data. The actual `v5wire.encryptGCM` in go-ethereum passes `aad = maskingIv || unmaskedHeader`. Confirmed by logging `encryptGCM(key, nonce, authData, plaintext)` on the Geth side: every observed AAD starts with the 16-byte masking-iv before the `discv5` protocol-id bytes.

Implementation: `Discv5Packet.BuildAad(maskingIv, rawHeader)` (cited in source).

### discv5 — `reqresp()` consumes the first packet after our WHOAREYOU reply

`v5test/framework.go reqresp()` sends the initial request, reads one packet, and either treats it as the response or — if it's a WHOAREYOU — retries the request as a handshake and reads *one* more packet. That second read consumes whichever packet arrives next on the bystander's UDP socket. If the responder sends a *reciprocal* ping (to add the bystander to its own routing table per `FindnodeResults`) before sending the Pong, reqresp eats the Ping as if it were the response and the bystander's main switch never sees it. The bystander then never marks itself "added" and the suite times out.

Fix: defer the reciprocal Ping ~200 ms (`Discv5Listener.ReciprocalPingDelayMs`) so the Pong is delivered first.

## Spec-vector pin

`Discv5KeyDerivationVectorTests` (in `Nethereum.DevP2P.SpecTests`) pins both:

- `TestVector_ECDH` from `go-ethereum/p2p/discover/v5wire/crypto_test.go` — confirms ECDH outputs the 33-byte compressed shared point (`0x033b11a2...`).
- `TestVector_KDF` from the same file — confirms HKDF-SHA256 with challenge-data as salt produces the expected `initiator-key = 0xdccc82d8...` and `recipient-key = 0xac74bb87...`.

Both pass independently of network interop, so a regression in the core crypto surfaces in the unit suite without needing a Geth binary.

## How the harness fixture data is used

- `chain.rlp` — concatenated RLP-encoded blocks (501 blocks for the eth-test fixture). Loaded by `GethTestdataChainBackedEthHandler` to serve `GetBlockHeaders`/`GetBlockBodies`/`GetReceipts`.
- `genesis.json` — initial allocation. Loaded by `GethTestdataGenesisBuilder` to compute the genesis state root and block hash.
- `forkenv.json` / `headblock.json` — fork transition heights and head block reference. Used to derive the EIP-2124 ForkID for the `Status` handshake.
- `headstate.json` — slim-account snapshot at the chain head. Loaded by the snap-test fixture for AccountRange responses.

For the eth-test mempool sub-tests (`InvalidTxs`, `LargeTxRequest`, blob variants), the historical state at head is rebuilt by `GethTestdataHistoricalStateBuilder` replaying every block through `Nethereum.CoreChain.BlockExecutor` (via `BlockImporter`). The result is cached statically so the ~2 s cost is paid once per process.

## Helpers

- `Helpers/MockEngineApiHttpServer` — loopback HTTP listener answering `200 OK {}` to every POST. Lets the eth-test tool's `sendForkchoiceUpdated()` succeed so it'll progress to the transaction-pool sub-tests.
- `Helpers/EthTestMempoolValidator` — nonce/balance/intrinsic-gas/block-gas-limit checks that the `InvalidTxs` sub-test exercises. Reads sender state from the head `IStateStore`.
- `Helpers/BlobSidecarValidator` — EIP-4844 versioned-hash check used by the blob sub-tests to disconnect peers whose KZG commitments don't reproduce the declared `blob_versioned_hashes`.

## When something regresses

1. Re-run the spec-vector unit tests first — `Nethereum.DevP2P.SpecTests`. If they fail, the regression is in core crypto (ECDH, HKDF, node-id derivation) and fixing them in isolation is faster than re-running the harness.
2. Re-run the affected suite in isolation, e.g. `dotnet test --filter "DisplayName~Ping"` for discv5 Ping.
3. If the failure mode is "Geth reports an error we can't reproduce in unit tests", add temporary `fmt.Fprintf(os.Stderr, ...)` lines to the relevant Geth function (`encryptGCM`, `decodeWhoareyou`, `deriveKeys` in `p2p/discover/v5wire/`), rebuild `devp2p.exe` per the instructions above, and run the failing test — Geth's stderr is captured by `_output.WriteLine` in each conformance test. Remember to revert the Geth source after.
