# Nethereum 7.1.0

Nethereum 7.1 brings the node stack introduced in 7.0 to release quality. DevP2P networking and sync are now stable, with snap sync's healing reworked. The EVM and CoreChain shed their main per-call and per-block costs. x402 becomes a conformant, hardened v2 stack. Proving Ethereum blocks in the Zisk zkVM moves to Zisk 1.3.1-alpha, with proofs verifiable on chain. The surfaces that handle untrusted input were audited and hardened. Testing now runs on the Ethereum conformance suites (EEST) through the production code paths.

[Full Changelog](https://github.com/Nethereum/Nethereum/compare/7.0.0...7.1.0)

## DevP2P: Ready for Release

`Nethereum.DevP2P` and `Nethereum.DevP2P.Sync` were previews in 7.0 and ship as stable packages in 7.1. A .NET node can join the Ethereum peer-to-peer network directly, sync the chain from untrusted peers, follow the head and serve other peers over the native devp2p stack.

### Snap sync: Phase 2 and healing reworked

Snap sync downloads the state in Phase 2 and repairs what changed underneath it in the Phase 3 heal. Both were reworked against the failures seen while syncing a mainnet node. A sync that is interrupted, overtaken by the chain or served by peers that lose the state still ends on a complete, certified state.

* **Complete trie nodes.** The heal guarantees that a stored trie node has a complete subtree. Stale nodes, stale sub-trie roots and nodes inside an extension's path are removed when they are found, and Phase 2 clears the nodes inside the extensions it writes when it resumes over an earlier run.
* **One converged root.** Heal and certify end on the same root, and finalize certifies and commits that root, never a pivot that moved in the meantime.
* **Near the tip.** A sync starts within 64 blocks of the verified tip, and the pivot never moves to a stale block.
* **Cheap repairs.** A hole is repaired by healing only the path down to it, not by downloading the contract again.
* **No deferred work.** Mismatched storage is healed at the live pivot instead of being recorded as deferred debt. One heal engine handles code, damage and re-pivoting.
* **Peers that lose the state.** Such peers are remembered per root. A stalled heal waits for a new pivot or a new snap peer instead of retrying in a loop.

### Following the head

Blocks propagate the way the current `eth` protocol does: a node announces its head with `BlockRangeUpdate`, and its peers pull the block. `BlockRangeAnnouncer` and `FollowingBlockSource` replace `NewBlock` push. A forged head announcement costs a follower one block, not a re-download.

### Bounded and accountable peers

* **Bounded inputs.** Every message is limited before it is decoded, every response is matched against the request that asked for it, and one budget covers the bytes being decoded across all connections.
* **Admission and deadlines.** Admission is bounded per IP and per subnet, and every frame read and write has a deadline.
* **Accountability.** Peers are held to account: an empty reply earns no credit, a header reply that does not chain is a protocol violation, and a peer from another network, genesis or fork is refused.
* **Discovery.** discv5 records and DNS trees are signature-checked, discv4 bonding is capped and revalidated, and routing tables limit nodes per subnet.

### Verified against the reference implementation

* **Reference decoder.** Peer-message decoding is compared in CI against a pinned reference decoder on 200,000 inputs, with no disagreement.
* **Conformance suites.** The reference `devp2p` conformance suites for `eth`, `snap`, discv4 and discv5 pass against a Nethereum node.

Details: [DevP2P](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/06-devp2p.md)

## Performance: EVM and CoreChain

A 500-block mainnet replay runs at about 590–640 ms per block, about 36–40 Mgas/s, on an AMD Ryzen 9 3900X limited to 12 threads and 45 GB of memory, with the state on a consumer 1 TB NVMe SSD.

### EVM

The engine no longer pays for work that is not the opcode's own:

* **Snapshots.** Call-frame snapshots are a revision journal instead of a deep copy.
* **Dispatch.** Synchronous opcodes run in a plain loop without allocated tasks.
* **Frames.** Frames no longer create identifiers, full-size stacks or context they never use.
* **Data.** Addresses, salts, topics and stack words stay bytes instead of round-tripping through hex strings.
* **Code analysis.** Analysis is cached by code hash.
* **Precompiles.** `ecAdd` and `ecMul` run on managed Montgomery arithmetic (`ManagedBn254Backend`, the new default), and MODEXP on 64-bit-limb Montgomery multiplication.

The snapshot journal, the bn254 arithmetic, MODEXP and the 256-bit division are each checked against an independent oracle in CI.

### CoreChain

* **One composition.** `ExecutionStackFactory` builds the same executor, state-root calculator and importer for every node role: mainnet follower, AppChain and DevChain. Sender recovery runs in every role; the prefetcher and parallel storage-trie updates are options (`Storage.PrefetchWorkers`, `Storage.StateRootWorkers`) that the mainnet follower turns on.
* **Parallel work.** Each block's senders are recovered on dedicated threads, and its transactions are pre-executed on a throwaway layer to load the state they touch (`StatePrefetcher`). The trie paths it writes are warmed, and dirty storage tries are updated in parallel when the root is computed.
* **Costs that grew with the wrong thing.** Transaction snapshots are undo journals instead of copies of everything dirtied so far in the block. An account created and destroyed in a block costs no storage scan.
* **Off the block path.** The flush stages its write batch in the background, and freezer promotion and indexing are background jobs that yield to execution.
* **Measurement.** `BlockExecutionStats` reports per-block reads, updates, hashes and precompile calls. `--replay-blocks` re-executes stored blocks through the production node and reports each stage.

Details: [EVM](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/03-evm.md) · [CoreChain & Storage](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/05-corechain-storage.md)

## x402: Protocol v2

`Nethereum.X402` moves from the 7.0 v2 wire format to a stack conformant with the x402 v2 specification and its reference implementation:

* **Several offers.** A route can offer several payment requirements (`RoutePaymentConfig.Accepts`), and only the offer the payer accepted is verified and settled.
* **Upfront payment.** The upfront flow (`extra.paymentFlow = "upfront"`) settles before the handler runs.
* **Permit2 gas sponsoring.** A payer who holds only the token can pay through Permit2 with the `eip2612GasSponsoring` and `erc20ApprovalGasSponsoring` extensions.
* **Cancellation.** An unsettled EIP-3009 authorization can be cancelled with a signed `cancelAuthorization`.
* **Facilitator.** `X402FacilitatorOptions` sets a gas ceiling, a per-network asset allowlist, an authorization policy, confirmations and a route prefix. The send path is bounded and serialised, and ERC-1271 smart wallets can settle.
* **Hardening.** Replay keys, route matching, canonical forms, caching and denial-of-service limits are hardened, and error reasons follow the reference.

Several error codes, HTTP statuses and validation rules changed; read the note before upgrading.

Details: [X402](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/12-x402.md)

## Zisk: Zisk 1.3.1 and On-Chain Verification

The 7.0 path for proving Ethereum blocks inside the Zisk zkVM moves to **Zisk 1.3.1-alpha**.

* **Stock toolchain.** The guest links Zisk's own `libziskos.a`, and the stock Zisk release binaries prove and verify the ELF, with no patched prover and no security-cookie patch. The whole pipeline runs on the CPU in the `nethereum-zisk` image.
* **On-chain verification.** `cargo-zisk wrap --plonk` turns a block's STARK proof into a PLONK proof that a Solidity contract verifies. `ZiskVerifier` ships with `ZiskVerifierAdapter` for the AppChain anchoring contracts.
* **Anchored proofs.** A block proof can be bound to an anchor contract's inputs. The guest commits `keccak256(AnchorInputs)` in its first eight public values, so the adapter accepts a proof only for the inputs it was made for.
* **Failed calls, not crashes.** The guest reports an invalid BN254 or BLS12-381 precompile input as a failed call, as the EVM requires, instead of stopping.
* **Proved on the release build.** The ten test blocks are proved and verified, and the two PLONK proofs are wrapped. On a 24-thread host each block proves in about 460 to 700 s and a PLONK wrap takes 340 to 500 s.
* **Portable RapidSnark.** `Nethereum.ZkProofs.RapidSnark` ships Linux libraries that load on glibc 2.14 (x64) and 2.17 (arm64) and later.

Details: [ZK / Zisk](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/13-zk-zisk.md)

## Security and Hardening

Before release, these surfaces went through a findings-driven security audit:

* peer networking and discovery;
* node JSON-RPC and the Engine API;
* decoders, key storage, signing and EIP-712;
* the beacon light client and the proof verifiers;
* the ERC-4337 bundler, x402, AppChain production, Sign-In with Ethereum and the wallet.

Every finding was fixed with a test that fails before the fix. The result is validation enforced at each boundary:

* **Decoding.** RLP, transactions, headers, receipts, ABI values and ENR records are read canonically, and they and SSZ input are bounded by the bytes actually supplied. Input is either decoded exactly or refused.
* **Node JSON-RPC.** The mainnet follower, DevChain and AppChain serve one endpoint with a content-type check, size and batch limits and timeouts on EVM calls; the mainnet follower and AppChain also apply a Host allow-list and serve `eth`, `net` and `web3` by default, while DevChain keeps its development method set.
* **Blocks.** Blocks are judged by the complete header and body rule set before they execute, each refusal naming its rule.
* **Keys and signing.**
  * `KeyStoreKdfLimits` caps what a keystore may cost to open.
  * Mnemonics are validated.
  * EIP-712 produces the reference hashes for recursive types and every value form.
  * Wallet vaults are AES-256-GCM with a per-vault salt.
* **Account Abstraction.** ERC-7562 validation is on by default, so an operation whose validation breaks the opcode and storage rules is refused at admission.
* **Sign-In with Ethereum.** The Blazor Server provider verifies the signature on every request, and each nonce is used once.

Details are listed per area under each note's Bug Fixes.

## Testing: Streamlined Around EEST

* **One conformance runner.** One runner owns the Ethereum conformance corpora: EEST stable per fork, the EEST devnet tests, ethereum/tests, and the legacy Cancun and Constantinople tests. It runs them through the production execution and block import paths, with one rejection rule table.
* **Results on the release build.** 72,715 EEST blockchain tests, 61,813 EEST state tests, 38,881 ethereum/tests cases and 144,470 legacy cases pass.
* **Diagnosis.** A failing case is diagnosed with an aligned trace and post-state comparison against a pinned reference client.
* **Pinned inputs.** Every external input (corpora, reference binaries, bundler spec tests) is pinned in one file and fetched by one tool, and tests read their paths from one settings file.
* **One test image.** The suites run the same way in one test image on any Docker host.
* **Removed.** The vendored test copies and the in-project test engines they replace are gone.

Details: [CI & Test Environment](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/CI-test-environment.md)

## Also in This Release

* **Mainnet follower under a consensus client.**
  * The new `Nethereum.EngineApi.Hosting` package hosts the JWT-authenticated Engine API.
  * `Nethereum.MainnetChain` can follow mainnet driven by a consensus client (`--engine`), or run engine-only on a custom genesis.
  * DevChain adds `testing_buildBlockV1` and `testing_commitBlockV1`.
  * [Chain Hosting](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/07-chain-hosting.md)
* **AppChain (preview).**
  * Single-sequencer blocks are sealed with the sequencer's key, and followers accept only the configured sequencer's chain.
  * Production and import run on one execution core.
  * [AppChain](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/08-appchain.md)
* **Account Abstraction.**
  * Bundler admission follows EntryPoint v0.9, with bounded mempool, reputation and receipt caches.
  * Account-creating UserOperations estimate and bundle on Amsterdam.
  * [Account Abstraction](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/09-account-abstraction.md)
* **EVM.** Blob parameters come from a per-chain blob schedule, so the Fusaka BPO steps are data. Every exceptional halt carries an `EvmHaltReason`.
* **Consensus.**
  * Clique validates each block against its parent's snapshot and survives restarts.
  * The beacon light client catches up period by period.
  * `StackTrie` hashes sorted keys without a store.
  * [Consensus](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/04-consensus.md)
* **RPC & Web3.** A nonce rejected by the node is released for the next transaction, and HD derivation paths are bounded. [RPC & Web3](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/02-rpc-web3.md)
* **Wallet, SIWE, MUD, DID, Explorer and Unity.**
  * The MUD Postgres indexer keeps several worlds apart.
  * The ethr DID resolver drops revoked entries.
  * Explorer ABI uploads require an `ApiKey`.
  * Unity WebGL wallet detection works.
  * [Wallet & UI](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/10-wallet-ui.md) · [ENS, MUD, DID & SIWE](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/11-ens-mud-did.md) · [Indexing & Explorer](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/14-indexing.md) · [Unity](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/15-unity.md)

## Upgrading from 7.0

The changes most callers will meet:

**Validation is stricter.**

* **Input that 7.0 accepted is refused.** Non-canonical or oversized RLP, transactions, headers, receipts, ABI values and ENR input now throws, and SSZ readers refuse hostile counts and lengths earlier.
* **Mnemonics.**
  * An invalid BIP-39 mnemonic throws.
  * `MinimalHDWallet` and `Bip39` derive from the canonical phrase, so a 7.0 phrase with capitals or extra whitespace gives different keys.
  * Pass `validateMnemonic: false` to keep the 7.0 behaviour.
* **Key recovery.** `EthECKey.RecoverFromSignature*` returns `null` where it threw.
* **Keystores.** A keystore over `KeyStoreKdfLimits` (scrypt 512 MiB, `p` 16, PBKDF2 10,000,000) needs the limits raised.
* **Response size.** `RestHttpHelper` refuses response bodies over 32 MiB unless `MaxResponseBytes` is raised.

**Defaults changed.**

* **Peers.** Handshake timeout 5 s and idle timeout 30 s.
* **Storage.** Background freeze indexing is on.
* **JSON-RPC.** Batch, filter and subscription limits and a 5-second EVM-call timeout.
* **Mainnet and AppChain RPC ports.** They no longer serve `debug_*` or `txpool_*` unless listed.
* **Bundler.** `EnableERC7562Validation` is `true` and `MaxVerificationGas` is 500,000.
* **Explorer.** ABI uploads need an `ApiKey`.

**APIs moved or removed in the node packages.**

* **Snapshots.** `IStateSnapshot`, `StateSnapshot` and `AccountStateSnapshot` are removed; use `ExecutionStateService` snapshots.
* **Block push.** `IBlockPublisher`, `PushedBlockSource` and the `NewBlockReceived` events are replaced by `BlockRangeAnnouncer`, `FollowingBlockSource` and `BlockRangeUpdateReceived`.
* **Engine API host.** It moved from `Nethereum.DevChain.Hosting` to `Nethereum.EngineApi.Hosting`.
* **Hardfork resolution.** `MainnetChainActivations.ResolveAt` returns `Osaka` across the BPO steps.
* **Block witnesses.** `BinaryBlockWitness.VERSION` is 4; 7.0 witnesses and proofs must be regenerated.

**Data formats.**

* **AppChain history.** A single-sequencer AppChain created by 7.0 has unsigned history that does not validate under 7.1.
* **Wallet vaults.** A vault saved by 7.1 cannot be read by 7.0.
* **SIWE tokens.** Tokens from the 7.0 Blazor Server provider are no longer accepted.

**x402.** Error codes, HTTP statuses and route matching changed; see the x402 note.

Each area note lists its breaking changes in full.

## Packages

Most packages ship as **7.1.0**. `Nethereum.DevP2P` and `Nethereum.DevP2P.Sync` are stable. The mainnet-follower and AppChain packages ship as **7.1.0-preview**:

* `Nethereum.MainnetChain`, `Nethereum.MainnetChain.Server`
* `Nethereum.AppChain`, `Nethereum.AppChain.Sequencer`, `Nethereum.AppChain.Policy`, `Nethereum.AppChain.Anchoring`, `Nethereum.AppChain.Anchoring.Postgres`, `Nethereum.AppChain.Server`, `Nethereum.AppChain.Server.Core`, `Nethereum.AccountAbstraction.AppChain`, `Nethereum.Explorer.Anchoring`

New in 7.1: `Nethereum.EngineApi.Hosting`.

Published for the first time:

* `Nethereum.AppChain.Anchoring`, `Nethereum.AppChain.Anchoring.Postgres`, `Nethereum.AppChain.Policy`
* `Nethereum.AccountAbstraction.AppChain`, `Nethereum.Consensus.Clique`
* `Nethereum.AppChain.Server` (as a dotnet tool)
* `Nethereum.AccountAbstraction.SimpleAccount`, `Nethereum.ABI.ABIInfoSqliteStorage`, `Nethereum.Wallet.UI.Components.Avalonia`

Packaging details:

* Every library with a README packs it.
* The libraries are strong-name signed with the shared key (`Nethereum.HdWallet` on .NET Framework excepted).
* `Newtonsoft.Json` 13.0.4 or later is required (the net35 and Unity AOT builds keep the Newtonsoft.Json they bundle).
* `Nethereum.HdWallet` moves to NBitcoin 10 and drops `net452`.

Details: [Build & Packaging](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/00-build-solution.md) · [Foundations](https://github.com/Nethereum/Nethereum/blob/7.1.0/release-notes/7.1/01-foundations.md)
