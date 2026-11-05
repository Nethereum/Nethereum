# RPC & Web3 — Nethereum 7.1

Nethereum 7.1 hardens the core client stack rather than widening it. A transaction the node rejects no longer leaves a hole in the nonce sequence, `Nethereum.Accounts` validates BIP-39 mnemonics and bounds HD derivation paths before deriving a key, and `Nethereum.Util.Rest` refuses response bodies larger than 32 MiB on every verb that reads one. The client-flavor packages (`Nethereum.Parity`, `Nethereum.Parity.Reactive`, `Nethereum.Rsk`, `Nethereum.StandardTokenEIP20`, `Nethereum.StandardNonFungibleTokenERC721`) gain README coverage with every public symbol traced to a tagged test, and the client-flavor packages pack their README.

## Nethereum.Accounts — Nonce released when the node rejects a send

`AccountSignerTransactionManager` now resets the account's `NonceService` when the node answers a raw send with an `RpcResponseException`, so the next transaction takes the nonce the rejected one never used instead of waiting behind a gap.

* Applies to `SendTransactionAsync` and `SendBlobTransactionAsync` when the nonce came from `Account.NonceService`; a transaction with an explicit `Nonce` is left alone (tested for `SendTransactionAsync`, and the blob path is tested for the reset)
* The exception is rethrown unchanged

## Nethereum.Accounts — BIP-39 validation and bounded HD derivation

`Bip39` and `MinimalHDWallet` refuse malformed mnemonics, oversized inputs and hostile derivation paths before any key is derived.

* `Bip39.MaxInputLength` (4096) caps mnemonic and passphrase length when validating; `validateMnemonic: false` skips the check
* `Bip39.IsValidMnemonic`, `ValidateMnemonic` and `CanonicalMnemonic` check word count (12/15/18/21/24), English word-list membership and checksum; `NormalizeMnemonic` only normalizes (NFKD, lower-cased, whitespace collapsed) and bounds the length
* The seed is derived from the canonical phrase, so a phrase with capitals or stray whitespace gives the same keys as the clean phrase
* `Bip39.IsLegacyUppercaseWordMnemonic` and `IsNonStandardMnemonicForm` detect inputs whose normalization changes the seed
* `Bip39.MnemonicToSeed(mnemonic, password, validateMnemonic)` and `MinimalHDWallet(mnemonic, password, validateMnemonic)` derive from a non-standard phrase on request, with the 7.0 raw-NFKD derivation; the existing overloads validate
* `MinimalHDWallet.MaxDepth` (255); `GetKeyFromPath` rejects null paths (`ArgumentNullException`, was `NullReferenceException`), deeper paths, and segments that are empty, zero-padded, over 10 digits, non-numeric or at or above 2^31

## Nethereum.Util.Rest — Response size cap

`GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync` and `PostMultipartAsync` send with `ResponseHeadersRead` and read each body, success or error, through one capped path and decode it with the response `Content-Type` charset (UTF-8 when none is declared).

* `RestHttpHelper.DefaultMaxResponseBytes` (32 MiB) and `RestHttpHelper.MaxResponseBytes { get; set; }`
* A declared `Content-Length` over the cap, or a body that grows past it while streaming, throws `HttpRequestException`; this includes the body of a non-success response, which is read through the same cap before the status exception is thrown
* A non-success status still throws `Exception` with `Error: {StatusCode}, {ResponseBody}`; `GetAsync` appends `, {response.Headers}`, as in 7.0
* The public `SendAsync` returns the raw `HttpResponseMessage`, so its caller owns the body and the cap does not apply

## Client-flavor and standard-token packages — README coverage

`Nethereum.Parity`, `Nethereum.Parity.Reactive`, `Nethereum.Rsk`, `Nethereum.StandardTokenEIP20` and `Nethereum.StandardNonFungibleTokenERC721` now ship a `README.md`; their public symbols carry `[NethereumDocExample]` so the README signatures are drift-checked. No behaviour change.

## Bug Fixes

* A rejected `eth_sendRawTransaction` no longer strands the `InMemoryNonceService` nonce
* Mnemonics with a bad checksum or unknown words, and derivation paths with leading zeros, overflowing indices or unbounded depth, no longer derive keys
* An unbounded or hostile HTTP response can no longer exhaust memory through the `RestHttpHelper` verbs, including the body of a non-success response

## Breaking Changes

* `new MinimalHDWallet(mnemonic)` and `Bip39.MnemonicToSeed(mnemonic, password)` now throw `ArgumentException` for an invalid BIP-39 mnemonic; pass `validateMnemonic: false` to derive from a non-standard phrase
* `new MinimalHDWallet(mnemonic[, password])` and `Bip39.MnemonicToSeed(mnemonic[, password])` derive the seed from the canonical phrase (NFKD, lower-cased, whitespace collapsed), so a 7.0 phrase with capitals or extra whitespace now gives different keys and addresses; `Bip39.MnemonicToSeed(mnemonic, password, validateMnemonic: false)` keeps the 7.0 raw-NFKD derivation
* `MinimalHDWallet.GetKeyFromPath(null)` throws `ArgumentNullException` instead of `NullReferenceException`
* `RestHttpHelper.GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync` and `PostMultipartAsync` throw `HttpRequestException` for a body over 32 MiB, whether the response succeeded or not (`DeleteAsync` did not read a body before, and an error body was read uncapped); raise `MaxResponseBytes` if larger bodies are expected

## Build & Packaging

* 19 libraries of the slice move from `VersionPrefix` to `Version` (Besu, Circles, Contracts, Geth, JsonRpc.Client, JsonRpc.IpcClient, JsonRpc.RpcClient, JsonRpc.SystemTextJsonRpcClient, JsonRpc.WebSocketClient, Optimism, Parity, Parity.Reactive, Quorum, RPC, RPC.Extensions, RPC.Reactive, Rsk, StandardNonFungibleTokenERC721, StandardTokenEIP20); `Nethereum.Parity`, `Nethereum.Parity.Reactive`, `Nethereum.Rsk`, `Nethereum.StandardTokenEIP20`, `Nethereum.StandardNonFungibleTokenERC721`, `Nethereum.Quorum` and `Nethereum.Web3Lite` gain `PackageReadmeFile` and a packed `README.md`, and the `Netherum` typo is corrected in the package tags (Accounts, Besu, Contracts, Geth, Optimism, Parity, Quorum, RPC, RPC.Extensions, Rsk, StandardNonFungibleTokenERC721, StandardTokenEIP20, Util.Rest, Web3, Web3Lite) and in the Parity, Rsk, Besu and Geth descriptions; the `Nethereum.Quorum` `AssemblyTitle` is corrected to `Nethereum.Quorum`
* `Nethereum.Web3` gains a `Directory.Build.props` that isolates its intermediate output per project
