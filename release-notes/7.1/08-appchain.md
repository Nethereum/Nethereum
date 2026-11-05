# AppChain — Nethereum 7.1

Nethereum 7.1 makes an AppChain node safer to expose and easier to run as a cluster. A single-sequencer chain now seals every block with the sequencer's key, so a follower accepts only blocks signed by the sequencer address it was configured with; a node that both produces and imports blocks runs one execution core behind one write gate and one chain head; Clique persists a snapshot every 1024 blocks and reloads it on restart. The JSON-RPC port is the shared chain-hosting endpoint: it serves `eth`, `net` and `web3` by default, accepts JSON-RPC batches, refuses a foreign `Host`, a foreign WebSocket `Origin`, an oversized body and a non-JSON content type, and keeps `txpool`, `debug` and `admin` off until you list them (`--rpc-modules`, `--rpc-vhosts`, `--ws-origins`). Transaction admission is one rule shared by `eth_sendRawTransaction` and peer relay, including blob transactions; a transaction that faults the host is dropped without stopping block production; and the real BLS12-381 and KZG precompile backends are wired into `nethereum-appchain`. On the contract side, `ZiskVerifier` (Zisk 1.3.1 PLONK) ships with `ZiskVerifierAdapter` bound to the executed block, and `AppChainPolicy` bans a single-address blacklist and checks the invitee of an invite. `Nethereum.AppChain.Server.Core` and `Nethereum.AppChain.Anchoring.Postgres` now ship a README and the server is packed as a signed tool.

## Security audit and cluster validation

AppChain block production and cross-chain messaging (7 findings), the node JSON-RPC surface it shares with the other hosts, and the bundler host it embeds were audited before 7.1; each finding was fixed with a test that fails before the fix. The ones an AppChain operator would have met:

* Single-sequencer blocks were unsigned, so any peer a follower dialled could hand it a chain of its own; single-sequencer mode is now Clique with the one-signer set, and startup refuses a sequencer address that does not belong to the node's key
* A transaction declaring more gas than the block had left was claimed from the pool and then dropped, neither included nor deferred, and one transaction declaring the whole limit took the block
* One unfunded `eth_sendRawTransaction` calling a BLS or KZG precompile stopped a default AppChain from producing blocks for good
* Any sender could move a fee-free chain's base fee to 1 for good by filling half a block, after which zero-price transactions were refused
* Hub messages with no executor were recorded as executed and acknowledged

The Clique and lease clusters were run with transactions on in-memory and RocksDB storage (hash- and path-keyed, with a node restart), checking that every node's header roots equal a recompute from flat state. That found the defects listed under Bug Fixes: two executors and two chain heads on one signer, ancestor snapshot rebuilds that moved the head, lease-gated blocks built with default options, extra data over 32 bytes and repeated timestamps, each of which made followers refuse or stop.

## Nethereum.AppChain.Server.Core — Sealed single-sequencer blocks, one execution core

A node holds one chain head and builds blocks and imports them through the same code.

* `AppChainConsensusConfig.ResolveClique()` returns `Clique` in Clique mode, and in single-sequencer mode a Clique configuration whose signer is `Sequencer`, whose only initial signer is the sequencer address and whose `PeriodSeconds` is 0. Both modes run one Clique engine, gate and strategy, so every block carries the signer's seal and a follower rejects a block signed by anyone else. `AppChainServerConfig.ProducesBlocks` reads the signer from `ResolveClique()`
* `AppChainServerConfigValidator.Validate` refuses a single-sequencer configuration whose `Sequencer.Address` does not belong to `Sequencer.PrivateKey`, refuses an invalid `Node.Producer` (`ChainNodeProducerValidator`) and refuses a malformed configured enode (`ChainNodeEnodeValidator`)
* `AppChainExecutionCore` (`ChainConfigFor`, `FlushCadenceFor`, `Activations`, `StateRoot`, `Commit`, `Executor`, `SideChain`, `Clique`, `CliqueGate`, `CreateImporter()`, `ChainRewrittenAsync`) is the one execution stack a node builds its producer and its importer from, so the two never keep separate state-root calculators
* `AppChainDevP2PFollower` has constructors and `RunAsync` overloads that take the `AppChainExecutionCore` and a chain write gate (`AsyncGate?`); the 7.0 signatures are unchanged and forward to them. A node that produces and imports runs both under one gate and commits the head it produced
* `AppChainDevP2PFollower` skips snap bootstrap when the peer's tip is within the pivot trail distance of genesis, says so in the log, and forward-executes from genesis, so a fresh same-genesis cluster imports a sibling's blocks
* `RocksDbCliqueSnapshotStore` persists a Clique snapshot every 1024 blocks on the node metadata store and the engine loads it on restart instead of replaying from genesis
* `AppChainServerRunner.RunAsync(AppChainServerConfig config, CancellationToken cancellationToken)` runs the server until the token is cancelled; `RunAsync(config)` forwards to it
* `AppChainServerConfig.Registry` (`Nethereum.EVM.HardforkRegistry?`) is the hardfork registry the chain is built with; `AppChainServerConfig.MaxRequestBodySize` (10 MiB) bounds the RPC request body
* `AppChainPrometheusMetricsExtensions.AddPrometheusMetrics` and `MapPrometheusMetrics` live in this package, so it no longer references the Aspire service defaults
* The node's `Rpc.GasCap` defaults to a deploy-sized limit (`GasConstants.BlockGasLimitLargeEnoughToDeployAt` at Amsterdam) instead of 50 million, so `eth_call` and `eth_estimateGas` can run the MUD World deployment

## Nethereum.AppChain.Server.Core — JSON-RPC and WebSocket exposure

`POST /` is mapped by the shared chain-hosting endpoint and `/ws` goes through the same envelope.

* `Node.Rpc.Modules` (default `eth,net,web3`) selects the served modules; `txpool`, `debug` and `admin` are opt-in and an unknown name throws. `admin_*` handlers are registered only when `admin` is listed, `eth_subscribe` only when `eth` is
* `Node.Rpc.AllowedHosts` and `Node.Rpc.AllowedOrigins` (both empty by default) extend the Host allow-list (`localhost`, IP literals and the bind host are always accepted) and the WebSocket origin list. A foreign `Host` is answered `403`, a non-JSON content type `415`, and a WebSocket upgrade from an unlisted browser origin is refused
* `POST /` accepts a JSON-RPC batch: the response is an array in request order, a notification has no entry, a batch of notifications returns nothing, an empty array and an invalid member are `Invalid Request`
* `WebSocketEndpoints.MapWebSocketEndpoint(this WebApplication app, WebSocketRpcHandler wsHandler, RpcHttpOptions options)` applies the same host and origin policy. Block notifications are queued to a bounded broadcaster (128 blocks, oldest dropped) instead of being sent inside the sequencer's block event, and a connection holds at most 200 subscriptions
* `nethereum-appchain` gains `--rpc-modules`, `--rpc-vhosts` and `--ws-origins`, and `--devp2p-peers` / `--trusted-peer` accept a comma-separated list through `ChainNodeNetworkConfig.SplitEnodeList`

## Nethereum.AppChain.Server — Precompile backends and packaging

* `AppChainPrecompileBackends.Build()` returns a hardfork registry with the BLS12-381 and KZG precompile backends; `Program` assigns it to `AppChainServerConfig.Registry`, so a transaction that calls those precompile addresses executes instead of faulting the host. The project references `Nethereum.EVM.Precompiles.Bls`, `Nethereum.EVM.Precompiles.Kzg` and `Nethereum.Signer.Bls.Herumi` and copies the native library next to the tool

## Nethereum.AppChain.Sequencer — Admission, fault isolation and block options

* `Sequencer.SubmitTransactionAsync` runs `TransactionAdmissionRules.FirstRefusal` before the policy check, the rule `eth_sendRawTransaction` and peer relay share: blob count and blob transaction size (a network-form blob transaction under the cap is accepted and mined), the size cap, signature, chain id, gas limit against the block gas limit, and fee cap against priority fee. A refusal throws `InvalidOperationException` (`Transaction rejected: <reason>`)
* `Sequencer(IAppChain appChain, SequencerConfig config, AsyncGate? chainWriteGate, Action<ulong, byte[]>? commitHead, ...)` produces blocks under a chain write gate and reports each produced block's number and hash to `commitHead`; the 7.0 constructor forwards to it
* `BlockProducer(IAppChain appChain, BlockExecutor engine, IIncrementalStateRootCalculator stateRootCalculator, ITrieNodeStore trieNodeStore, ..., BlockCommit? commit = null)` builds production on a prebuilt executor and calculator
* A transaction that faults the host is dropped, its sender's later transactions are deferred and the block is produced from the rest (at most four drops per block, then the block is cut); dropped transactions are logged and not returned to the pool. A transaction that did not fit the gas or blob budget goes back to the pool only if it would fit an empty block; any other is dropped
* `AppChainBlockProductionStrategy(AppChainConfig config)` is the default block options of the producer and of a lease-gated producer: timestamp `max(clock, parent.Timestamp + 1)`, the gas limit stepping one bound step per block toward `AppChainConfig.BlockGasLimit` within the parent bound, extra data `AppChain:<name>` cut to 32 bytes, and a base fee kept at zero on a fee-free chain
* `AppChainBuilder` in-memory chains use the journaled in-memory bundle, so the per-transaction revert that fault isolation relies on is available

## Nethereum.AppChain — Fee-free chains and trie store

* `AppChainConfig.FeeFreeBaseFee` is `true` when `BaseFee` is zero: the base fee stays at zero in production, header validation, `eth_baseFee` and `eth_feeHistory`, so a block over half full no longer raises the base fee and gets zero-price transactions refused
* `AppChain` given no trie node store creates an in-memory content node store

## Nethereum.AppChain.Anchoring — Message accumulator and Zisk verification

* `IMessageMerkleAccumulator.AppendLeaf(ulong sourceChainId, ulong targetChainId, MessageLeaf leaf)`: message ids are contiguous per target chain, so interleaved source chains are all processed. `MessageMerkleAccumulator.AppendLeaf(sourceChainId, leaf)` forwards with the source as target; `MessageAccumulatorExtensions.RebuildFromStoreAsync(accumulator, store, targetChainId)` rebuilds in message-id order
* `MessageProcessor` with no message executor records a message as not executed (`Success` false, zero transaction hash, a warning) instead of a synthetic success, and the message queue orders by target chain
* `ZiskVerifierAdapter` accepts a proof for exactly 11 public inputs and 64 Zisk public values, requires the first eight values to commit to `keccak256(abi.encodePacked(publicInputs))` and requires the inputs to name the block the proof executed; a wrong count, length or block reverts. `ZiskVerifier` and `PlonkVerifier` (Zisk 1.3.1 PLONK) are added with the generated C# service `ZiskVerifierService`; `IZiskVerifier.verifySnarkProof` takes `bytes32` keys and the adapter constructor still takes `uint64[4]`

## Nethereum.AppChain.Policy — Blacklist

* `AppChainPolicy.isValidWriter` bans an address held alone in the blacklist tree, and `invite` checks the invitee against the blacklist. The generated service takes the extra proof: `AppChainPolicyService.InviteRequestAsync(string invitee, byte[] newWritersRoot, List<byte[]> proofCallerIsWriter, List<byte[]> proofInviteeNotBlacklisted)` and the matching `InviteRequestAndWaitForReceiptAsync`; `InviteFunction.ProofInviteeNotBlacklisted` carries it

## Nethereum.AppChain.Anchoring.Postgres — README

* The package packs a README that documents the anchor index, the summary denormalizer and the message index, with the registration extensions and their options

## Bug Fixes

* Single-sequencer blocks were unsigned, so any peer a follower dialled could hand it a chain of its own; they are sealed and verified (see above)
* A transaction declaring more gas than the remaining budget was lost instead of included or deferred, and one transaction declaring the whole limit took the block; the executor's fit check decides and every transaction that did not fit goes back to the pool
* RPC admission lacked the size cap, gas-limit cap and chain-id check that peer admission had, and blob transactions sent with `eth_sendRawTransaction` were refused for their network-form size or deferred for ever when over the blob count; one shared rule decides
* One unfunded transaction calling a BLS or KZG precompile stopped a default AppChain from producing blocks; the backends are wired and a faulting transaction is dropped
* On a fee-free chain, one block over half full moved the base fee to 1 for good
* The metrics for JSON-RPC method names held every distinct name a caller sent; unknown methods are metered under one key
* Log filters and subscriptions accepted any number of addresses and topics; more than four topic positions, or 1,000 addresses or values in a position, answer `-32602`
* WebSocket fan-out ran inside the block event and could stretch the block interval
* In-memory chains produced on a bare store, so a faulting transaction halted production
* Hub messages with no executor were recorded as successes and acknowledged
* Followers hung at height 0 when two blocks were sealed within a second (timestamps are now strictly increasing) or when a chain name over 23 characters made the extra data exceed 32 bytes
* A configured gas limit that differed from the parent's by more than the parent bound produced blocks every importer refused; lease-gated blocks carried default block options and were refused at block 1
* A Clique signer ran two executors with separate state-root calculators and two chain heads; a sibling sealed at a height already sealed locally was executed on the wrong state and rejected. Rebuilding ancestor snapshots moved the Clique head back, and the head did not survive a restart
* A fresh same-genesis cluster never imported a sibling's blocks because the snap orchestrator waited for a pivot that could not exist
* A one-address blacklist never banned, and an invite did not check the invitee
* Two in-memory nodes in one process shared one DevP2P identity; each now generates its own
* A forged head announcement made a follower pull the chain again from the announced height; a rewind of more than one block needs the announced header fetched and hash-checked within the reorg limit

## Breaking Changes

* Removed: `AppChainServerConfig.BaseFee` (`BigInteger?`). Set `Node.Producer.BaseFee` (`AppChain:Node:Producer:BaseFee`); there was no flag
* Removed: `DevP2PProducedBlockPublisher` and `ChainNodeSyncConfig.EnablePushedBlocks` (the AppChain default configuration no longer sets it). Blocks propagate by announcing the head and pulling; use `ChainNodeServeCallbacks.BlockRangeUpdateReceived` where `NewBlockReceived` was used (see Chain Hosting)
* Signature: `WebSocketEndpoints.MapWebSocketEndpoint(this WebApplication app, WebSocketRpcHandler wsHandler)` gained a required `RpcHttpOptions options`; pass `config.Node.Rpc.ToHttpOptions()`
* Interface: `IMessageMerkleAccumulator` gained `AppendLeaf(ulong sourceChainId, ulong targetChainId, MessageLeaf leaf)`; an implementer must add it
* Generated contract API: `AppChainPolicyService.InviteRequestAsync` and `InviteRequestAndWaitForReceiptAsync` gained `proofInviteeNotBlacklisted`; the three-parameter forms are gone and `InviteFunction.ProofInviteeNotBlacklisted` must be set
* Solidity and contract behaviour: `IZiskVerifier.verifySnarkProof` takes `bytes32` keys instead of `uint64[4]`; `ZiskVerifierAdapter.verify` reverts unless it receives exactly 11 public inputs and 64 public values, and returns `false` unless the first eight public values commit to the inputs. Proofs of the guest's generic outputs are not accepted by the adapter
* Behaviour: single-sequencer chains are signed. A chain created by 7.0 has unsigned history and does not validate under 7.1, and a follower accepts only blocks sealed by the key behind `--sequencer-address`. Fees go to the sequencer as the block author instead of the zero coinbase
* Behaviour: the AppChain RPC port serves `eth`, `net` and `web3` only; `admin_*`, `debug_*` and `txpool_*` need `--rpc-modules` (`Node.Rpc.Modules`). A request with a `Host` outside the allow-list is `403`, a non-JSON content type is `415`, and a WebSocket upgrade from a browser origin needs `--ws-origins`. A request body over `MaxRequestBodySize` is refused
* Behaviour: a malformed enode in `Sync.FollowPeerEnode`, `Network.TrustedPeers` or `Network.TrustedBootnodes` refuses the start instead of logging a warning
* Behaviour: `MessageProcessor` with no message executor records messages as not executed; a transaction over the shared admission limits is refused at `SubmitTransactionAsync` and `eth_sendRawTransaction`; the node's `Rpc.GasCap` default is the deploy-sized limit (set `Node.Rpc.GasCap` to keep 50 million)
* Behaviour: a sequencer on a chain with `BaseFee` zero keeps the base fee at zero (`FeeFreeBaseFee`); `ITransactionOrderingPolicy.Order` lost its `gasLimit` parameter (CoreChain)

## Build & Packaging

* `Nethereum.AppChain.Server` is `IsPackable`, signed with the shared key and packs as the `nethereum-appchain` tool; `Nethereum.AppChain.Server.Core` is signed; both are preview packages (`$(NethereumVersionPreview)`)
* `nuget.bat` now packs `Nethereum.AppChain.Anchoring`, `Nethereum.AppChain.Anchoring.Postgres`, `Nethereum.AppChain.Policy`, `Nethereum.AccountAbstraction.AppChain` and `Nethereum.Consensus.Clique`, which the published AppChain packages depend on
* `Nethereum.AppChain.Server.Core` no longer references `Nethereum.Aspire.ServiceDefaults`
* `Newtonsoft.Json` is required at 13.0.4 or later
* READMEs: `Nethereum.AppChain.Server.Core` and `Nethereum.AppChain.Anchoring.Postgres` are new and pack with the package; `Nethereum.AppChain.Server` gains the RPC exposure flags; `Nethereum.AppChain.Sequencer`, `Nethereum.AppChain.Anchoring`, `Nethereum.AppChain.Policy` and `Nethereum.AppChain.Contracts` gain the 7.1 text above
