# Foundations — Nethereum 7.1

Nethereum 7.1 makes the foundation libraries safe to point at bytes you do not control. Every decoder in `Nethereum.RLP`, `Nethereum.Model` and `Nethereum.ABI` now reads canonical, bounded structures and allocates in proportion to the input, not to the counts the input declares, so a transaction, header, receipt, ABI return value, peer message or ENR record from an untrusted source is either decoded exactly or refused. You can read RLP field by field with the new `RlpReader`, decode a peer's transaction with `TransactionFactory.ReadTransaction`, cap what a keystore may cost to open with `KeyStoreKdfLimits`, sign EIP-712 typed data with the reference hashes (recursive types, every value form), keep a list-valued ENR entry verifiable, ask BLS12-381 and alt_bn128 for an outcome instead of an exception, and warm the secp256k1 contexts at startup. No package here is new or removed since 7.0, no public member was removed or renamed, and the behaviour changes you will notice (stricter decoding, `null` instead of an exception on a key that does not recover, validated mnemonics) are listed under Breaking Changes.

## Security audit

The foundation libraries were audited as the code every untrusted byte passes through: decoders reachable from untrusted input (six rounds), key storage (six rounds), and signing and EIP-712. Each finding was fixed with a test that fails before the fix. The ones a caller would have met:

* A frame of nested RLP lists overflowed the stack and killed the process, and a nested decode retained up to 64 copies of its input
* An ABI array declaring 2^26 elements in 160 bytes of return data allocated up to 29 GB, aliased head offsets decoded n^depth values, a signature-modifier regex backtracked quadratically, and a self-referencing or mutually recursive EIP-712 type overflowed the stack
* A keystore with scrypt `N = 2^25, r = 1` wrapped a size computation and wrote past its buffer, killing the process; any keystore could demand unbounded scrypt memory or PBKDF2 work; the MAC compare was not constant time
* A `TypedData<TDomain>` signed for its first domain kept that domain after `Domain` was reassigned, and encoding wrote to the caller's instance, so a shared instance could sign another message
* Nearly every package resolved `Newtonsoft.Json` 11.0.2 for a consumer without its own reference, a version with a deeply-nested-JSON stack exhaustion advisory (GHSA-5crp-9r3c-p9vr); the floor is now 13.0.4

## Nethereum.RLP — Canonical, Bounded Decoding and a Streaming Reader

`RLP.Decode` and `DecodeCollection` validate structure and canonical form before building anything, and a new forward-only `RlpReader` lets decoders read a structure field by field without a tree.

* `RLP.MaxDecodeDepth` (64) bounds list nesting; deeper input throws the invalid-RLP exception instead of overflowing the stack
* `RLP.Decode(byte[] msgData, int maxElements)` and `RLP.DecodeCollection(byte[] msgData, int maxElements)` count elements in an allocation-free pre-scan and reject a payload over the budget; `RLP.MinUntrustedElementBudget` (1024) and `RLP.UntrustedElementBudget(int inputLength)` give the budget used for bytes from a peer
* non-canonical input is refused: a single byte under `0x80` with a string prefix, a long-form length under 56, a length with a leading zero, a length past the input; `RLP.Decode`, which reads exactly one item, also refuses bytes after the top-level value, while `DecodeCollection` reads a sequence of items
* `RLPCollection.RLPData` of a nested list is a slice of the input, copied on first access
* `RlpReader` (constructors over `byte[]` or a slice) with `Position`, `IsAtEnd()`, `IsAtEnd(RlpListScope)`, `PeekKind()` returning `RlpKind` (`Byte`, `String`, `List`), `PeekSize()`, `ReadListHeader()` returning an `RlpListScope` (`Start`, `End`, `Length`), `EnsureEnd(RlpListScope)`, `EnsureEnd()`, `CountItems(RlpListScope)`, `ReadBytes()`, `ReadFixed(int)`, `ReadUInt64()`, `ReadBigInteger()`, `ReadBool()`, `Skip()`, `ReadRawSegment()` returning an `RlpSegment` (`Offset`, `Length`), `ReadRaw()` and `ReadData()`
* `ConvertorForRLPEncodingExtensions.ToLongBitsFromRLPDecoded(this byte[])` reads a full `uint64` into a `long` without overflow

## Nethereum.Model — Strict Decoders and Peer-Message Limits

Transactions, receipts, headers, bodies, logs, withdrawals, access lists, authorisations and every devp2p / snap message decode through one set of `RlpReader`-based decoders that accept and refuse exactly what the reference client's decoders do, checked by the reference-decoder differential in CI.

* `TransactionFactory.ReadTransaction(RlpReader reader, bool allowBlobNetworkWrapper = true)` is the peer-wire entry point; it also refuses an unsigned typed transaction. `CreateTransaction` and the `Transaction*Encoder.Decode` methods apply the same field rules and still accept unsigned typed transactions for signers
* `TransactionNonceOverflowException`, `TransactionRlpLengthException`, `TransactionFieldFormatException` (with `Field`) and `AuthorisationFormatException` name why a transaction was refused; `LegacyTransactionField` gained `ChainId`, `MaxPriorityFeePerGas`, `MaxFeePerGas`, `MaxFeePerBlobGas`, `SignatureV`, `SignatureR` and `SignatureS`; `ScalarWiderThanItsFieldException` gained a constructor taking `fieldBytes`
* an EIP-7702 authorisation must be a list of exactly six canonical fields; a transaction must be exactly one RLP item with a nonce below 2^64; `to` is 0 or 20 bytes (20 for blob and set-code transactions); access-list addresses are 20 bytes and keys 32
* header decoders accept the 15, 16, 17, 20, 21 and 23 field shapes only, refuse a hash, bloom, coinbase or nonce of the wrong width, and read `GasLimit`, `GasUsed` and `Timestamp` across the full unsigned 64-bit range
* `BlockHeaderEncoder.Decode(RlpReader)`, `LogEncoder.Decode(RlpReader)` / `DecodeList(RlpReader)`, `ReceiptEncoder.Decode(RlpReader)`, `WithdrawalEncoder.Decode(RlpReader)`, `BlockBodiesMessageEncoder.ReadBody` / `DecodeBody`, `NewBlockMessageEncoder.DecodeBlock`, `ForkIdEncoder.Read` and `ReceiptsMessageEth69Encoder.ReadBlockReceipts` / `ReadSingleReceipt`
* `BlobSidecar.WrapperVersion` carries the EIP-7594 network wrapper version; `Transaction4844Encoder` decodes and emits `rlp([tx_payload_body, wrapper_version, blobs, commitments, cell_proofs])`; blob, commitment and proof element sizes are checked at decode
* `TransactionsMessageEncoder.Decode` refuses a `Transactions` message of more than 5000 transactions, one that repeats a transaction hash, one that carries any EIP-4844 blob transaction and one that carries an unsigned typed transaction
* `BlockRangeValidator.Violation(earliest, latest, latestHash)`, `PeerResponseBounds.MaxTransactionAnnouncements` (5000) and `SnapServingLimits.MaxTrieNodeLookups` (1024) / `MaxResponseProofNodes` (128)
* `EnrRecord.SetListValue(key, encodedList)` and `IsListValue(key)` keep a list-valued entry (the `eth` fork id) as its exact bytes, so a real execution-layer ENR verifies after a round trip; `EnrRecordEncoder.Decode` rejects unsorted or duplicate keys, a sequence over 64 bits or with a leading zero, and a record over `EnrRecord.MaxEncodedSize`

## Nethereum.Util — Division, Slices, Backoff and Prefix Scans

* `Backoff(int initialMs)` with `Current`, `Advance(int capMs)` and `Reset()` doubles a wait to a cap
* `ByteUtil.PrefixSuccessor(byte[] prefix)` returns the exclusive upper bound of a prefix scan, and `ByteArrayComparer` is now a null-safe FNV-1a equality comparer with length-first equality, usable as a `Dictionary<byte[], T>` key comparer
* `ContractUtils.CalculateCreate2Address(string address, byte[] salt, byte[] byteCode)` computes the EIP-1014 address on bytes
* `EvmUInt256.FromBigEndian(byte[] source, int offset, int length)` reads a slice; `EvmAddress.FromHex` parses a 40-digit address straight into its 20 bytes; `Sha3Keccack.CalculateHash(byte[] input, int offset, int length, byte[] output, int outputOffset)` hashes a slice into a caller buffer
* `DIV`, `MOD`, `ADDMOD` and `MULMOD` of `EvmUInt256` reduce through one 64-bit limb long division (`EvmUInt256.Division.cs`); Keccak-256 is a one-shot unrolled keccak-f[1600] and `ToHex` on a byte array builds the string in place on .NET 6 and later

## Nethereum.Util.Rest — Bounded Response Bodies

* `RestHttpHelper.MaxResponseBytes` (default `DefaultMaxResponseBytes`, 32 MiB) caps the body of every verb that reads one (`GetAsync`, `PostAsync` and the other typed calls); a larger body, declared or streamed, throws `HttpRequestException`. The raw `SendAsync` returns the response unread and is not capped. The Util.Rest package is owned by the slice 02 note, which carries its README

## Nethereum.ABI — Safe Decoding and Exact EIP-712

* declared array sizes are checked in 64-bit arithmetic against the remaining bytes, head offsets that are out of range or repeated are refused, and each dynamic element is sliced to the next greater distinct offset, so decoding is linear in the input
* signature strings (parentheses) and array types (brackets) are limited to 32 levels of nesting, and a JSON ABI document to 68 JSON levels (`2 * 32 + 4`); `ABIStringSignatureDeserialiser` and `ByteCodeSwarmExtractor` are linear scans with no regular expressions
* `Eip712TypedDataEncoder` produces the reference hashes (checked against the reference implementation's `TypedDataAndHash`): dependencies are collected with a visited set so mutually recursive types encode, a type is a struct if and only if it is a key in `types`, unknown member types and direct self-reference are refused, and values follow the declared type (addresses with or without `0x`, exact `bytesN` lengths, range-checked integers from decimal, hex or CLR numbers). Undeclared message and domain keys are ignored and missing declared keys are refused
* encoding no longer writes to the `TypedData<TDomain>` argument, and the domain is rebuilt from `Domain` on every encode, so a domain changed after the first encode is the domain signed
* `TypedDataRaw.GetChainIdFromDomain()` reads `long`, `int`, `BigInteger` and decimal or hex string chain ids

## Nethereum.ABI.ABIInfoSqliteStorage — Async and Batch Lookups

* `SqliteABIInfoStorage` implements the eight async members `IABIInfoStorage` declares (in 7.0 it implemented none of them, so the project did not compile): `GetABIInfoAsync(long, string)`, `FindFunctionABIAsync`, `FindFunctionABIFromInputDataAsync`, `FindEventABIAsync`, `FindErrorABIAsync`, `FindFunctionABIsBatchAsync`, `FindEventABIsBatchAsync` and `FindABIsBatchAsync`; the package now ships a README

## Nethereum.Signer — Recovery Outcomes, Context Warm-up and BN128 Outcomes

* `EthECKey.RecoverFromSignature` (all overloads) and `RecoverFromParityYSignature` return `null` when no key recovers from the signature; they stay lenient otherwise, and `RecoverFromParityYSignature` recovers with `V` as given (the y-parity and canonical-signature check lives in `VerifyTransaction`, below)
* `Secp256k1Contexts.EnsureInitialised()` and `IsInitialised` build the NBitcoin.Secp256k1 sign and recovery contexts once, at startup, instead of on the first block
* `BN128Curve.TryAdd`, `TryMul` and `TryPairing` report invalid alt_bn128 input as `false`; `Add`, `Mul` and `Pairing` still throw `ArgumentException`
* `BN128Curve.Add`, `Mul`, `TryAdd` and `TryMul` run on the managed G1 implementation (internal types under `Crypto/BN128/Managed`, moved from `Nethereum.EVM.Precompiles`, so the EVM precompile and the library share one implementation); `Pairing` and `TryPairing` still read G1 through BouncyCastle
* `EnrRecordSigner.Verify` rejects a high-`s` signature
* `MessageSigner.EcRecover` throws `ArgumentException`, and `TransactionVerificationAndRecoveryImp.GetSenderAddress` / `GetPublicKey` throw `InvalidOperationException`, for a signature that recovers no key; `VerifyTransaction` returns `false`. `VerifyTransaction` also returns `false` for a typed (non-legacy) transaction whose y-parity is above 1 or whose `r` and `s` are not canonical (`IsCanonical`); `GetSenderAddress` and `GetPublicKey` apply no such check

## Nethereum.Signer.EIP712 — Signing Follows the Corrected Encoder

* `Eip712TypedDataSigner` signs through the rewritten encoder: the same inputs hash as the reference implementation and MetaMask, a shared `TypedData<TDomain>` is not modified by signing, and `RecoverFromSignatureV4` recovers from the same encoding it signs

## Nethereum.Signer.Bls and Nethereum.Signer.Bls.Herumi — Outcome Operations

* `IBls12381OutcomeOperations` (`TryG1Add`, `TryG1Msm`, `TryG2Add`, `TryG2Msm`, `TryPairing`, `TryMapFpToG1`, `TryMapFp2ToG2`) reports malformed EIP-2537 input as `false`; `Bls12381Operations` implements it alongside `IBls12381Operations` and adds `TryG1Mul` and `TryG2Mul`. The throwing methods are the `Try` forms wrapped and throw `ArgumentException` exactly when the `Try` form returns `false`

## Nethereum.KeyStore — Bounded and Validated Decryption

* `KeyStoreKdfLimits` (`MaxScryptMemoryBytes` 512 MiB, `MaxScryptP` 16, `MaxPbkdf2Iterations` 10,000,000, the constants `DefaultMaxScryptMemoryBytes`, `DefaultMaxScryptP` and `DefaultMaxPbkdf2Iterations`, and `Default`) caps what a keystore may cost; `KeyStoreService.KdfLimits`, `KeyStoreServiceBase<T>.KdfLimits`, `KeyStoreService.DecryptKeyStoreFromJson(password, json, limits)`, `KeyStoreServiceBase<T>.DecryptKeyStoreFromJson(password, json, limits)` / `DecryptKeyStore(password, keyStore, limits)` and the `KeyStoreCrypto` overloads taking limits apply them; a derived keystore service overrides the new protected virtual `KeyStoreServiceBase<T>.DecryptFromCryptoInfo(password, cryptoInfo, limits)`
* every scrypt and PBKDF2 parameter is validated before any allocation (N a power of two above 1, `r * p` below 2^30, dklen 32, cipher `aes-128-ctr`, version 3, `hmac-sha256`, hex fields of the right shape); a hostile keystore fails with `ArgumentException`. The scrypt sizes are computed in 64 bits, so `N = 2^25, r = 1` no longer writes past the buffer and kills the process
* the MAC is compared in constant time, and a well-formed `address` field must be the address of the decrypted key or `DecryptionException` is thrown
* `KeyStoreKdfChecker.GetKeyStoreKdfType` throws `ArgumentException("Invalid KeyStore json")` for text that is not a keystore document, including JSON with no `crypto` or no `kdf` field

## Nethereum.HDWallet — Validated Mnemonics, Own Randomness, NBitcoin 10

* `Wallet(string words, string seedPassword, ...)` throws `ArgumentException` unless the mnemonic is 12, 15, 18, 21 or 24 BIP-39 English words with a valid checksum and each input is at most 4096 characters; `Wallet(string words, string seedPassword, string path, IRandom random, bool validateMnemonic)` with `false` keeps the lenient behaviour
* a wallet generated from a word list and count draws its entropy from the `IRandom` it was given and no longer replaces NBitcoin's process-wide `RandomUtils.Random`
* NBitcoin moves to 10.0.16; the package targets `netstandard2.0` and `net461`
* whitespace in a phrase (a double space, a tab, leading or trailing blanks) gives the same seed as the clean phrase in strict mode and with `validateMnemonic: false`, and capital letters throw `ArgumentException` in strict mode; the canonical-seed derivation of the `Nethereum.Accounts` `Bip39` helper is a separate change, described in the slice 02 (Accounts) note

## Nethereum.Merkle and Nethereum.Merkle.Binary — Proof Verification Never Throws

* `MerkleTree<T>.VerifyProof(proof, rootHash, itemHash, hashProvider, pairingConcatType)` returns `false` for a null proof, root, item hash or proof element; the README documents that `MerkleTree<T>` and `AbiStructMerkleTree<T>` share one hash for leaves and nodes and points new designs at `OpenZeppelinStandardMerkleTree<T>`
* `BinaryTrieProofVerifier.VerifyProof` skips a proof node it cannot decode, so a malformed proof returns `null` (or the honest value) and never throws

## Nethereum.Hex

* `ToHex` on a byte array builds the string in place on .NET 6 and later; no API change

## Bug Fixes

* Signer: on .NET 8 and 9, under tiered dynamic PGO, concurrent `BN128Curve.Pairing` / `TryPairing` (and so the EVM pairing precompile) refused valid input: about 30 of 400,000 parallel pairings over the 14 reference `bn256Pairing` vectors threw `ArgumentException`, 2 threads were enough, and the 7.0.0 tag fails the same way. Every refusal came from `TwistPoint.IsInCorrectSubgroup`. The 22 methods on that path (`TwistPoint` `Add`, `Double`, `ScalarMul`, `IsInCorrectSubgroup`; `Fp2` `Add`, `Sub`, `Mul`, `Square`; the `Fp` field and limb arithmetic) now carry `[MethodImpl(MethodImplOptions.AggressiveOptimization)]` under `#if NET8_0 || NET9_0`, which compiles them without tiering or dynamic PGO; no logic changed and other target frameworks compile as before. After it the commit reports 0 wrong answers in 400,000 parallel pairings on .NET 8.0.31, 8.0.28, 9.0.20 and 10.0.12
* ABIInfoSqliteStorage: `SqliteABIInfoStorage` did not implement the eight async members of `IABIInfoStorage` and did not compile; it now implements them
* RLP: nested-list flood, deep nesting and non-canonical encodings no longer overflow the stack or amplify allocation (decode retained up to 64 copies of the input and about 95 times a peer payload before); a non-canonical slot number is now refused
* Model: a header with a leading-zero-stripped hash or nonce no longer decodes and hashes as itself; `gasLimit`, `gasUsed` and `timestamp` at or above 2^63 decode instead of throwing `OverflowException` (31 valid withdrawal fixture blocks previously threw); the EIP-7594 blob transaction network wrapper decodes (it failed with an index error); malformed EIP-7702 authorisations and a nonce at or above 2^64 are refused (EEST transaction tests 124 of 124, from 50)
* Model: an `EnrRecord` with a list-valued entry re-encoded it as a byte string, so every real execution-layer ENR failed signature verification
* ABI: an array declaring 2^26 elements in 160 bytes allocated up to 29 GB, aliased head offsets decoded n^depth values, a modifier regex backtracked quadratically, and a self-referencing or mutually recursive EIP-712 type overflowed the stack
* ABI / Signer.EIP712: a `TypedData<TDomain>` signed for its first domain kept that domain after `Domain` was reassigned
* Signer: `ECKey.RecoverFromSignature` raised a `NullReferenceException` for a signature that recovers no key (it ignored the `TryRecover` result), so ECRECOVER reached its empty result through an exception
* KeyStore: a keystore with `N = 2^25, r = 1` killed the process (`128 * r * N` wrapped in `int` and the unsafe SMix wrote past the buffer); a non-hex or wrong-length field raised an arbitrary exception
* HDWallet: a wallet constructor replaced NBitcoin's process-wide random for the whole process

## Breaking Changes

No public member was removed or renamed. These behaviours changed:

* RLP decoding is strict: input `RLP.Decode` previously accepted (non-canonical lengths, a prefixed single byte under `0x80`, bytes after the top-level value, nesting over 64) now throws
* Model decoding is strict: a transaction with a `to` that is not 0 or 20 bytes, a signature scalar over 32 bytes, a gas limit over 64 bits, a nonce at or above 2^64, a list where a string belongs or trailing bytes, a header with a wrong-width field or an 18/19/22 field shape, a receipt status other than empty, `0x01` or a 32-byte root, and a typed receipt outside types 1 to 4 are refused. `TransactionFactory.ReadTransaction` also refuses an unsigned typed transaction
* `EthECKey.RecoverFromSignature*` and `RecoverFromParityYSignature` return `null` where they previously threw; `MessageSigner.EcRecover` throws `ArgumentException` and `GetSenderAddress` / `GetPublicKey` throw `InvalidOperationException` where they previously threw `NullReferenceException`; `VerifyTransaction` returns `false` where it previously threw, and for a typed transaction with a y-parity above 1 or a non-canonical signature
* EIP-712 encoding refuses input the types do not describe (a value that does not match its declared type, an unknown member type, a missing declared key) and a struct whose name ends in digits or contains a primitive name (`Point1`, `Mybytes4`) is now encoded as a struct
* `new Wallet(words, seedPassword)` throws `ArgumentException` for a mnemonic that is not valid BIP-39 English; use `new Wallet(words, seedPassword, Wallet.DEFAULT_PATH, null, false)` to keep the old behaviour. Stored 7.0 vault phrases are not affected by this package
* a keystore whose scrypt memory cost exceeds 512 MiB, whose scrypt `p` exceeds 16 or whose PBKDF2 count exceeds 10,000,000 is refused on decryption until `KeyStoreKdfLimits` is raised; the 1 GiB "high security" scrypt cost in the README needs a raised `MaxScryptMemoryBytes`. A keystore whose `address` does not match its key, with a cipher other than `aes-128-ctr`, a version other than 3 or a non-32-byte dklen is refused
* `KeyStoreKdfChecker.GetKeyStoreKdfType` throws `ArgumentException` for text that is not a keystore document; an unknown `kdf` still throws `Exception("Invalid KeyStore json")` wrapping `InvalidKdfException`
* `KeyStoreCrypto.GeneratePbkdf2Sha256DerivedKey(password, salt, count, dklen)` throws `ArgumentNullException` for a null password or salt and `ArgumentException` for `count` outside 1 to 10,000,000 or `dklen` below 1; pass a raised `KeyStoreKdfLimits` to the new overload `GeneratePbkdf2Sha256DerivedKey(password, salt, count, dklen, limits)` for a higher count. The `DecryptScrypt` and `DecryptPbkdf2Sha256` overloads without limits apply `KeyStoreKdfLimits.Default`
* `RestHttpHelper` throws `HttpRequestException` for a typed-call response over 32 MiB unless `MaxResponseBytes` is raised (slice 02 owns this package)
* `Nethereum.HdWallet` no longer targets `net452` (NBitcoin 10 has no asset it can use)
* `Bls12381Operations` throwing methods report malformed input as `ArgumentException` with one message per operation (`"Invalid G1ADD input"`, `"Invalid PAIRING input"`, `"Invalid MAP_FP_TO_G1 input"` and so on); the per-cause messages of 7.0 (`"Failed to map Fp to G1"`, `"Invalid G1 point: not on curve"`) are gone. The exception type is unchanged
* `BN128Curve.Add`, `Mul` and `Pairing` report invalid input with one `ArgumentException` message per operation (`"Invalid BN128 ADD input: coordinates not in the field or point not on the curve"`, the same for `MUL`, and `"Invalid BN128 pairing input: length, coordinates, curve membership or subgroup"`); the per-cause messages of 7.0 (`"Invalid BN128 coordinates"`, `"Point is not on the BN128 curve"`, `"G2 point not in correct subgroup in pairing input"` and the others) are gone. The exception type is unchanged
* `SCrypt.CryptoScrypt` validates its parameters before allocating: `ArgumentNullException` for a null password or salt, and `ArgumentException` when `N` is not a power of two above 1, `r` or `p` is below 1, `r * p` is 2^30 or more, `dkLen` is below 1 or the memory size exceeds what an array can hold. In 7.0 it validated nothing, so encrypting with custom scrypt parameters outside these bounds now throws before any work is done
* an EIP-4844 sidecar whose wrapper version is not 1 is refused on decode with `InvalidOperationException`; `TransactionsMessageEncoder.Decode` now refuses oversized, duplicate, blob and unsigned typed broadcasts
* `EnrRecordSigner.Verify` returns `false` for a high-`s` signature, and `EnrRecordEncoder.Decode` throws `FormatException` for unsorted or duplicate keys

## Build & Packaging

* every library of this slice packs with `Version` (not `VersionPrefix`) from `NethereumVersion`; package tags read `Nethereum` (was `Netherum`); Ledger, Trezor, AWS Key Management and Azure Key Vault descriptions are corrected the same way; `Nethereum.Signer.Ledger` and `Nethereum.Signer.Trezor` are now strong-name signed with `NethereumKey.snk`, like the other Nethereum assemblies
* `Newtonsoft.Json` is required at 13.0.4 or later in every package that references it (the net35 and Unity AOT builds are excluded) (`[11.0.2,14)` became `[13.0.4,14)`): NuGet resolved 11.0.2 for anyone without their own reference, and that version has GHSA-5crp-9r3c-p9vr (deeply nested JSON exhausts the stack)
* `Nethereum.HdWallet` references NBitcoin 10.0.16; the `net461` build stays unsigned because NBitcoin is not strong-named, and the other libraries are strong-name signed with `NethereumKey.snk`
* `Nethereum.ABI.ABIInfoSqliteStorage` packs its README; `Nethereum.RLP` and `Nethereum.Merkle` have descriptions that say what they contain
