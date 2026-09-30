# Foundations — Nethereum 7.0

Nethereum 7.0 rebuilds the foundation libraries — the model, encoding, hashing, signing and trie primitives every other package is built on — into a layer that can model an Ethereum block end to end, from any fork's header down to its wire bytes. You can now round-trip a block header under its own fork's rules from Legacy through Amsterdam, build and sign an EIP-4844 blob transaction, encode and validate an EIP-7928 Block Access List, model the full devp2p / snap / LES wire vocabulary, do 256-bit EVM arithmetic with value-type `EvmUInt256`/`EvmInt256` instead of `BigInteger`, hash with Poseidon2 over Goldilocks and BN254 fields, verify a Merkle-Patricia range proof against a snap page, and derive an ECDH shared secret and sign an ENR — the primitives the EVM, CoreChain and DevP2P packages consume. No package here is new since 6.1.0; every one below is an existing package extended in place, and the trivial ones are called out as such.

## Nethereum.Model — The Wire and Consensus Data Model

The biggest delta in the foundation layer. `Nethereum.Model` grew from a transaction/receipt POCO library into the full data model of an Ethereum block and the peer-to-peer protocols that move it — the foundation the EVM, CoreChain and DevP2P packages encode against.

* Per-fork block header codecs — `IBlockHeaderCodec` with `LegacyBlockHeaderCodec`, `LondonBlockHeaderCodec`, `ShanghaiBlockHeaderCodec`, `CancunBlockHeaderCodec`, `PragueBlockHeaderCodec` and `AmsterdamBlockHeaderCodec`, chosen by `BlockHeaderCodecSelector` so a header round-trips under the field set its own fork defines (the Amsterdam codec carries the 23-field header)
* Per-type transaction decoders — `ITransactionDecoder` with `Eip1559TransactionDecoder`, `Eip2930TransactionDecoder`, `Eip4844TransactionDecoder`, `Eip7702TransactionDecoder`, `LegacyOnlyTransactionDecoder` and the `Eip2718ReceiptCodec`/`LegacyReceiptCodec` receipt codecs
* EIP-4844 blob transactions — `Transaction4844`, `Transaction4844Encoder`, `BlobEncoder`, `BlobGasCalculator` (including the EIP-7918 reserve-price branch of `CalculateExcessBlobGas`, via `Eip7918ReservePriceInputs`), the `IBlobKzgProvider` seam with `MockBlobKzgProvider`, and `BlobSidecarNotAllowedInBlockBodyException`
* EIP-7928 block access lists — the `AccountChanges` change-record model (a block access list is a `List<AccountChanges>`, alongside `SlotChanges`/`StorageChange`/`BalanceChange`/`NonceChange`/`CodeChange`) and `BlockAccessListRLPEncoder`
* Block-encoding seam — `IBlockEncodingProvider`, `IBlockHashProvider`, `IBlockRootsProvider` with `RlpBlockEncodingProvider` and `RlpKeccakBlockHashProvider`, so a consumer can hash and encode a block without hard-wiring RLP+Keccak
* Receipt gas accounting — `ReceiptGasAccounting` (`CumulativeGasUsed` reconciliation) and the `Receipt`/`ReceiptEncoder` refresh
* devp2p / snap / LES wire vocabulary — the `P2P/` message model for `eth/68`–`eth/71` (`Eth68StatusMessage`, `Eth69StatusMessage`, block/body/receipt request-and-response messages, `NewPooledTransactionHashesMessage`), `snap/1`–`snap/2` (`Snap/SnapMessages`), and the LES message set (`Les/*`), plus `ForkId`/`ForkIdEncoder` (EIP-2124) and `EnrForkIdEntry`
* ENR record model — `Enr/EnrRecord` (EIP-778 node records)
* Transaction plumbing — `BaseFeeCalculator` (EIP-1559), `TransactionFactory`, `SignedTransactionExtensions`, `LegacyTransactionRlp`, and the EIP-7702 authorisation model (`Authorisation7702Signed`, `AuthorisationListRLPEncoderDecoder`)

## Nethereum.Util — EVM Value Types and Hashing Primitives

The utility layer gained the value types the reworked EVM runs on and a full Poseidon2 hashing stack. The headline is `EvmUInt256`: a 256-bit unsigned integer as a value type (no `BigInteger` allocation on the hot path), with a signed `EvmInt256` companion and an `EvmAddress` value type.

* `EvmUInt256` / `EvmInt256` — value-type 256-bit arithmetic (`+ - * / % `, comparisons, bit ops, shifts, exponentiation) with `BigInteger`, hex and RLP conversions (`EvmUInt256.BigInteger.cs`, `EvmUInt256HexExtensions`, `EvmUInt256RLPExtensions`) and conversion to and from the BN254 scalar field (`BN254FieldElement`)
* `EvmAddress` — a value-type 20-byte address (`Zero` value, `From`/`FromHex`, equality operators)
* Poseidon2 hashing — `GoldilocksPoseidon2HashProvider`, `Poseidon2Core`, `Poseidon2GoldilocksConstants`, the `IPoseidonFieldOps` field abstraction with `GoldilocksField`, `BN254FieldElement`/`BN254PoseidonCore`/`BN254PoseidonField`, `BigIntegerPoseidonField` and `EvmUInt256PoseidonField`, plus `PoseidonEvmHasher`, `BN254PoseidonPairHashProvider` and precomputed constants/presets (extends the `CircomT1`/`CircomT2` presets shipped in 6.1.0)
* Keccak reuse — a reusable `Sha3Keccack`/`KeccakDigest` path that avoids re-allocating the digest per hash
* Utility additions — `Base64UrlConvert`, `Crc32Ieee`, `BoundedCacheEviction`, expanded `BigIntegerExtensions` and `ByteUtil`, the EIP-7708 native-transfer-log emitter helper, and the `[NethereumDocExample]` attribute (`NethereumDocExampleAttribute`, moved into `Nethereum.Util` from the `Nethereum.Documentation` project; namespace unchanged) used across the docs-traceability gates

## Nethereum.Merkle.Patricia — Range Proofs and Pluggable Node Storage

`Nethereum.Merkle.Patricia` was reorganised (nodes into `Nodes/`, RLP into `Nodes/Rlp/`, proofs into `ProofVerification/` and `Proofs/`) and grew the two capabilities snap sync needs: range-proof verification and a pluggable node store.

* Merkle-Patricia range proofs — `PatriciaRangeProofVerifier`, `PatriciaRangeIterator`, `PatriciaRangeProofGenerator` and `PatriciaPathWalker`, so a contiguous range of leaves streamed over snap can be verified against the trie root
* Front-door proof verification — `ProofVerification` unifying `AccountProofVerification`, `StorageProofVerification`, `TransactionProofVerification` and `TrieNodeVerification`
* Pluggable node storage — `ITrieNodeStore` with `InMemoryContentNodeStore` (content-addressed) and `InMemoryPathNodeStore` (path-based), `ContentAddressedNodeStore`, `TrieNodeSet`, `ITrieTracer` tombstone tracing, and `TransientFlushUnavailableException`
* Node RLP round-trip — the `Nodes/Rlp/` serializers (`BranchNodeRlpSerializer`, `ExtendedNodeRlpSerializer`, `LeafNodeRlpSerializer`, …) behind a unified `NodeRlpDecoder`
* `PatriciaTrie` save/collapse/reload with an inlining invariant preserved across reload

## Nethereum.Merkle.Binary — EIP-7864 State Diffs and Checkpoint Storage

The EIP-7864 binary trie shipped in 6.1.0; 7.0 adds the state-diff and persistence machinery a stateless execution client needs.

* Binary-trie state diffs — `BinaryTrieStateDiff`, `BinaryTrieStateDiffEncoder`, `BinaryTrieStateDiffProducer`, `BinaryTrieStateDiffVerifier` and `StemDiff`
* Pluggable node storage — `IBinaryTrieNodeStore` with `InMemoryBinaryTrieNodeStore` and `BinaryTrieCheckpointSerializer`
* `CachedValuesMerkleizer` for repeated stem merkleization

## Nethereum.Signer — Crypto for Networking and New Transaction Types

`Nethereum.Signer` gained the crypto the networking and post-Prague transaction work depends on, without changing its shipped signing API surface (two behaviour changes are called out below).

* ECDH + ECIES — `EthECKey.CalculateEcdhSharedPointCompressed` and `EciesEncryption`, the shared-secret and encryption primitives behind the RLPx handshake (exercised end to end by the DevP2P stack)
* ENR signing — `Enr/EnrRecordSigner`, which signs and verifies the EIP-778 node records used by discovery
* EIP-4844 blob signing — `Transaction4844Signer` for signing blob transactions and their sidecars
* EIP-7702 authorisation recovery — `EthECKeyBuilderFromSignedAuthorisation` recovers the authority key from a signed 7702 authorisation
* BN128 hardening — a Montgomery-form `Fp`/`Fp2` rework in `Crypto/BN128/*` validated by differential test against the prior implementation
* The private key is always the 32-byte scalar — `EthECKey.GetPrivateKeyAsBytes()` left-pads a key whose leading bytes are zero to 32 bytes (6.1.0 returned it shorter, with the leading zeros dropped), and `GetPrivateKey()` follows, so such a key round-trips to the hex it was created from and signs on both the BouncyCastle and the NBitcoin.Secp256k1 (`EthECKey.SignRecoverable`) backends
* NBitcoin.Secp256k1 signs and recovers by default on .NET 8 and later — `EthECKey.SignRecoverable` now defaults to `true` in the `net8.0`, `net9.0` and `net10.0` builds (in 6.1.0 it defaulted to `false`), so on those targets `SignAndCalculateV`, `SignAndCalculateYParityV` and public-key recovery (`EthECKey.RecoverFromSignature`, `RecoverFromParityYSignature`) run on NBitcoin.Secp256k1 instead of BouncyCastle, while `Sign`, `Verify`, `VerifyAllowingOnlyLowS`, key generation and ECDH stay on BouncyCastle. A differential test checks that both backends produce the same `(r, s, v)` and recover the same address. Set `EthECKey.SignRecoverable = false` to return to BouncyCastle; `AddMainnetChainServer` and `AppChainComposition.ComposeAsync` set it to `true` when they compose a node, so in those hosts opt out after composition. The `net6.0` build keeps the `false` default, and the `netstandard2.0` and .NET Framework builds have no such switch and always use BouncyCastle

## Nethereum.RLP — Canonical Scalar Encoding

* `RlpScalar` — canonical-scalar validation (`IsCanonical`, `FitsA256BitField`); the legacy transaction decoder that consumes it rejects a non-canonically encoded (leading-zero) scalar rather than silently re-normalising

## Nethereum.Model.SSZ — Block, Receipt and Withdrawal Encoders

The `Nethereum.Model.SSZ` library shipped in 6.1.0 (with header/receipt/access-list/log encoders); 7.0 extends it with a block-level encoding provider and a root calculator.

* `SszBlockEncodingProvider` — an SSZ implementation of the `IBlockEncodingProvider` seam
* `SszRootCalculator` and `SszBlockRootsProvider` / `SszSha256BlockHashProvider` — SSZ tree-hash roots for a block
* `SszReceiptEncoder` and `SszWithdrawalEncoder`, plus the EIP-4844 path in the expanded `SszTransactionEncoder`

## Nethereum.ABI — Permit2 EIP-712 Types

* Permit2 EIP-712 message types (`Permit2TypedData`, `PermitSingle`, `PermitBatch`, `PermitDetails`, `TokenPermissions`, `PermitTransferFromWithSpender`) now live in `Nethereum.ABI.EIP712.Permit2`, and `PermitSigner` in `Nethereum.Signer.EIP712.Permit2`. In 6.1.0 `Permit2TypedData`, `PermitSingle`, `PermitBatch`, `PermitDetails`, `TokenPermissions` and `PermitSigner` were part of `Nethereum.Uniswap` (`PermitTransferFromWithSpender` is new in 7.0); that namespace is removed, so update your `using` directives (see the breaking-change entry in the RPC/Web3 release note)

## Documentation-only packages (no shipped code delta)

For completeness: `Nethereum.Hex`, `Nethereum.KeyStore`, `Nethereum.HDWallet` and the core `Nethereum.Merkle` library changed only their READMEs and a handful of no-op cleanups (one dead line removed in `Nethereum.Hex`, interface-comment/attribute tidy in `Nethereum.Merkle`) between 6.1.0 and 7.0. They carry no new capability in 7.0.
