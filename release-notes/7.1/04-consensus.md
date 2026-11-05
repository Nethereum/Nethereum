# Consensus — Nethereum 7.1

Nethereum 7.1 makes the consensus packages safe to run against untrusted input and able to restart. The Clique engine validates every block against the snapshot of its own parent, enforces the block period and the recent-signer window, rejects a checkpoint whose signer list is not the snapshot's, and persists a snapshot every 1024 blocks so a node recovers its head after a restart. The beacon light client catches up period by period when the store is behind, refuses updates signed in a slot that the wall clock does not allow, only advances the optimistic head on real participation, and measures header staleness from the execution block's own timestamp. The Merkle Patricia package gains `StackTrie` (a hashing-only trie for sorted keys), throws `MissingTrieNodeException` for a node a store cannot resolve, and returns `false` for malformed proofs instead of throwing. The SSZ readers and the execution-layer SSZ decoders bound every declared count and length by the bytes that remain.

## Security audit and cluster validation

The beacon light client's update processing and the SSZ decoders were audited against the consensus-specs Altair light-client sync protocol and as decoders of untrusted bytes; Clique was validated by running signer clusters with transactions, rotations, leases and restarts on in-memory and RocksDB storage, checking every node's header roots against a recompute from flat state. Each finding was fixed with a test that fails before the fix.

* An update signed by one sync-committee member could move the optimistic head and its execution state root; the optimistic head now advances only above the safety threshold of participation
* An older genuine update could move finality backwards; the finalized header advances only to a newer slot
* A light client resumed from a store several periods behind never caught up, and a store that stopped advancing still looked fresh because staleness was measured from the last write
* Clique judged a block on a fork with the wrong signer set, let a recent signer seal again, accepted a checkpoint that replaced the signer list, and lost its head on restart; a cluster with a restart or a sibling block could diverge or stop

## Nethereum.Consensus.Clique — Per-parent snapshots, block period and restart recovery

Every block is judged against the snapshot of its parent, found by block hash, so a competing fork is checked against its own signer set instead of the engine's current head.

* `CliqueEngine.ValidateBlockInternal(header, parent)` refuses a `parent` that is not number-1 with a hash equal to `header.ParentHash` (`Unknown ancestor`), a header earlier than `parent.Timestamp + BlockPeriodSeconds` (`Invalid timestamp`; a period of 0 lets a header share its parent's timestamp), a parent with no snapshot, and a checkpoint block whose signer list differs from the snapshot. Authorisation, the recent-signers limit and the in-turn difficulty are checked against the parent's snapshot
* `CliqueEngine.CanProduceBlock(blockNumber)` is `false` while the local signer sealed one of the last `signers / 2 + 1` blocks; `GetSigningDelayAsync` waits until the head timestamp plus `BlockPeriodSeconds` has passed (in-turn) and adds the wiggle (out-of-turn)
* `CliqueEngine.NextBlockTimestamp(BlockHeader? parent)` is `max(parent.Timestamp + BlockPeriodSeconds, now)`; `CliqueBlockProductionStrategy.PrepareBlockOptions` stamps it and carries `ChainConfig.FeeFreeBaseFee` into `BlockProductionOptions`
* `CliqueEngine.AuthorOf(BlockHeader)` returns the local signer for a header whose `ExtraData` is `null`, shorter than 97 bytes (`EXTRA_VANITY` + `EXTRA_SEAL`) or ends in 65 zero seal bytes; for a sealed header it returns the signer recovered from the seal, or `header.Coinbase` when recovery fails
* `CliqueEngine.HasSnapshot(byte[] blockHash)`, `CliqueEngine.SnapshotInterval` (1024), and a constructor `CliqueEngine(CliqueConfig, ILogger<CliqueEngine>?, ICliqueSnapshotStore?)`
* `ICliqueSnapshotStore` (`Save(CliqueSnapshot)`, `Load(byte[] blockHash)`), `InMemoryCliqueSnapshotStore`, and `CliqueSnapshotCodec.Encode` / `Decode` (a truncated or corrupt buffer decodes to `null`). `ApplyBlock` saves a snapshot at every multiple of `SnapshotInterval`
* `CliqueConsensusBlockGate.RestoreHeadSnapshotAsync(CancellationToken ct)` rebuilds the head snapshot from the block store, starting at the nearest persisted or checkpoint snapshot, and adopts the genesis snapshot when the head is genesis. Before it validates a block, the gate rebuilds any missing parent snapshot
* `CliqueSnapshot.Timestamp`

## Nethereum.Consensus.LightClient — Catch-up, wall-clock check and optimistic safety

* `LightClientService` requests `GetUpdatesAsync(startPeriod, count)` (at most `MaxRequestLightClientUpdates` = 128 periods per call) and applies the updates in order when a finality or optimistic update is signed more than one period ahead of the store. When the gap exceeds `LightClientConfig.MaxCatchUpPeriods` (16), the range request fails, or the range lacks the next period, it re-bootstraps: from a newer configured `WeakSubjectivityRoot`, then only if `LightClientConfig.AllowBeaconRebootstrap` is `true` from the beacon node's finalized root. A bootstrap that is not newer than the store, or whose slot the clock has not reached, is refused; with `AllowBeaconRebootstrap` left `false` the verified head is kept
* `LightClientService.CatchUpProgress` raises `LightClientCatchUpProgress` (`Stage`, `StorePeriod`, `TargetPeriod`, `Reason`) with `LightClientCatchUpStage` `PeriodApplied`, `Completed`, `UpdateRejected`, `Rebootstrapped`, `RebootstrapFailed`
* An update whose `SignatureSlot` is later than the current slot (`GenesisTime`, `ChainSpec.SecondsPerSlot`, `Clock`, plus `MaximumGossipClockDisparityMilliseconds` = 500) is rejected with a `LastRejectReason` starting `future_signature_slot`
* The optimistic head advances only when more than half of the larger of `LightClientState.PreviousMaxActiveParticipants` and `CurrentMaxActiveParticipants` signed the update; both are tracked per period and persisted by `LightClientStateCodec` (a frame without them decodes as 0)
* A bootstrap sets the optimistic head to the bootstrap header; an applied finality update only moves the finalized header forward
* `LightClientConfig.MaxCatchUpPeriods`, `AllowBeaconRebootstrap`, `GenesisTime`, `Clock`; `LightClientNetworks.MainnetGenesisTime`, `SepoliaGenesisTime`, `HoleskyGenesisTime`, `TryGetGenesisTime(BigInteger chainId, out ulong)`; `CreateConfig` sets `GenesisTime`
* `TrustedHeaderProvider` measures staleness as `DateTimeOffset.UtcNow - header.Timestamp`, the age of the execution block, instead of the time the store was last written. `ITrustedHeaderProvider.Name` is a default interface member (the implementing type's name); `TrustedHeaderProvider.Name` is `"LightClient"`

## Nethereum.Merkle.Patricia — StackTrie, missing nodes and non-throwing verifiers

* `StackTrie(byte[] prefixNibbles = null, NodeEmitted onNode = null)` with `Update(byte[] key, byte[] value)`, `Hash()` and `Reference()` hashes 32-byte keys in strictly ascending order with only the open path in memory and emits each node of 32 bytes or more through `delegate void NodeEmitted(byte[] path, byte[] blob)`. Its root equals the `PatriciaTrie` root over the same rows
* `MissingTrieNodeException(byte[] owner, byte[] path, byte[] hash)` (`Owner`, `Path`, `Hash`) is thrown by `NodeDecoder.Decode` when a store has no blob for a `HashNode`; the empty-trie root still decodes as an `EmptyNode`
* `TrieNodeVerification.RequireAuthentic(Node reference, byte[] blob)` and `RootBlobHashesTo(byte[] blob, byte[] stateRoot)`
* `PatriciaPathWalker.NibblesToCompact(byte[] nibbles, bool leaf)`; `NibblesToCompact(byte[])` is `leaf: false`
* `PatriciaTrie.ClearExtensionSpansOnSave`: with an `IRawNodeReader` store, a commit deletes the stored standalone nodes inside the path an extension node now spans
* The account, storage, range and transaction verifiers, `PatriciaProofVerifier.TryVerify` and `VerifyEntry` return `false` (`RangeProofResult.Invalid` for `Verify`) for a proof that cannot be decoded, such as an empty compact key or an undecodable node. A key outside the requested range, unsorted or prefix keys, an omitted or altered entry and a proof of presence offered as absence are rejected

## Nethereum.Ssz, Nethereum.Consensus.Ssz, Nethereum.Model.SSZ — Bounded decoding

* `SszReader.Remaining`; `ReadList<T>` and `ReadVector` throw before allocating when the declared count needs more bytes than remain (`InvalidOperationException`; `ArgumentOutOfRangeException` for a negative count or size); a read with a negative or oversized length throws `InvalidOperationException`
* `SszBasicTypes.ReadBytesVector(this ref SszReader, int elementCount, int elementLength)` and `ReadFixedRootVector(this ref SszReader, int expectedCount)` apply the same bounds
* `SszAccessListEncoder.DecodeAccessList`, `SszTransactionEncoder.DecodeAuthorisationList` throw `InvalidOperationException` for a first offset below 4 or past the data; `DecodeAccessTuple`, the log, receipt authority list and receipt log list decoders size their lists by the bytes that remain; `SszTransactionEncoder.ParseTransaction` throws `ArgumentException` when the payload or signature length exceeds the record

## Bug Fixes

* Clique validated each block against the engine's current snapshot, so a block on a fork was judged by the wrong signer set; each block now uses its parent's snapshot
* Clique accepted a block whose supplied parent was not its parent, whose timestamp was earlier than parent plus period, or that was signed by a signer inside the recent-signers window; all are now rejected, and a signer inside the window no longer tries to produce
* Clique block timestamps were the wall clock; they are now `max(parent + period, now)`
* A Clique checkpoint block replaced the snapshot signers with its listed signers; the listed signers must now equal the snapshot's
* A Clique node lost its head snapshot on restart; it is now saved every 1024 blocks and restored, and rewinding to genesis adopts the genesis snapshot
* A Clique snapshot's `BlockHash` was the seal hash; it is now the block hash
* The light client could not advance when the store was more than one period behind the head; it now catches up or re-bootstraps
* The light client accepted an update signed in a future slot, and let a minority of the committee move the optimistic head; both are rejected
* Header staleness was measured from the last store write, so a store that stopped advancing looked fresh; it is measured from the header timestamp
* A missing trie node read as an empty subtree; it throws `MissingTrieNodeException`
* Patricia proof verifiers could throw on hostile proofs; they return `false`, and soundness tests pin the rejected shapes
* A hostile SSZ count or length could allocate far more than the buffer held; every reader bounds it first

## Breaking Changes

* No public member is removed or renamed in this slice
* Behaviour: `CliqueEngine.ValidateBlockInternal` rejects blocks it accepted before (unrelated parent, early timestamp, recent signer, checkpoint mismatch, no parent snapshot). A caller that validates a header against a parent it has not applied through `ApplyBlock` or the block gate now gets `Unknown ancestor`; apply the parent first, or use `CliqueConsensusBlockGate`
* Behaviour: `CliqueEngine.GetSigningDelayAsync` no longer returns zero for the in-turn signer; it returns the time until the parent timestamp plus the period. When `ApplyBlock` is called without a `blockHash`, `CliqueSnapshot.BlockHash` is now the block hash (Keccak-256 of the encoded header) instead of the seal hash
* Behaviour: `TrustedHeaderProvider` raises `StaleHeaderDetected` / `StaleHeaderException` from the header's timestamp; a provider that was never stale because the store was rewritten recently can now report stale
* Behaviour: `LightClientService` resumed from a store that is many periods behind catches up or re-bootstraps instead of staying put. `AllowBeaconRebootstrap` is `false` by default; set it to `true` to re-bootstrap from the beacon node's own finalized root
* Behaviour: `MissingTrieNodeException` is thrown for a hash node whose blob the store does not hold, where an empty subtree was returned. It propagates from `NodeDecoder.Decode(HashNode, ITrieNodeStore, bool)`, `NodeRlpDecoder.Decode(HashNode, ITrieNodeStore, bool)`, `HashNode.DecodeInnerNode`, `PatriciaTrie.Get(byte[])`, `PatriciaTrie.Get(Node, byte[])`, `PatriciaTrie.GetFromHashNode`, `PatriciaTrie.Put(byte[], byte[])`, `PatriciaTrie.Put(Node, byte[], byte[])`, `PatriciaTrie.Delete(byte[])`, `PatriciaPathWalker.WalkPath`, `ProofGenerator.GenerateProof` (both overloads), `ProofGenerator.GeneratePathProof` (both overloads), `PatriciaRangeProofGenerator.GenerateProof` (both overloads) and `PatriciaRangeIterator.EnumerateRange`, and by `NodeDecoder.DecodeFromRlpData` / `NodeRlpDecoder.DecodeFromRlpData` when `decodeHashNodes` is true (7.0 returned an `EmptyNode` there). Catch it where a partially synced store is read
* Behaviour: the Patricia verifiers return `false` where they could throw
* Behaviour: `CliqueEngine.ApplyGenesisSigners` no longer stores a copy of the genesis snapshot under block 0 in the snapshot cache; it clears the cache and keeps the genesis snapshot separately, and block 1 is validated against it
* Behaviour: `LightClientService.InitializeAsync` resuming from a stored state runs `UpdateAsync`, `UpdateFinalityAsync` and `UpdateOptimisticAsync` as independent steps; an exception in one no longer skips the others (cancellation is still thrown)
* Behaviour: `SszReader` and the SSZ decoders throw earlier for hostile counts and lengths, with the same exception types as a read past the end

## Build & Packaging

* `Nethereum.Beaconchain`, `Nethereum.Ssz`, `Nethereum.Consensus.Ssz`, `Nethereum.Consensus.LightClient` and `Nethereum.Model.SSZ` take `<Version>` from `$(NethereumVersion)` (was `VersionPrefix`); `Nethereum.Beaconchain` and `Nethereum.Model.SSZ` now pack their README
* READMEs: Clique, LightClient, Merkle.Patricia, Ssz, Consensus.Ssz and Model.SSZ
