# Account Abstraction — Nethereum 7.1

Nethereum 7.1 brings the ERC-4337 bundler's admission rules in line with EntryPoint v0.9. The bundler turns ERC-7562 validation on by default and bounds its mempool, reputation and receipt caches. Its JSON-RPC host refuses malformed and oversized requests. Gas estimation for account-creating UserOperations now works on Amsterdam, and WebAuthn rejects public keys that are not valid P-256 points.

## Security audit and ERC-7562

The ERC-4337 bundler, its EntryPoint v0.9 validation and its JSON-RPC host were audited over eight rounds before 7.1, and each finding was fixed with a test that fails before the fix. The results change what a default bundler accepts:

* ERC-7562 validation is on by default: a UserOperation whose validation uses a forbidden opcode or reads storage outside what the rules allow its sender and staked entities is refused at admission, so one operation's validation cannot be invalidated by another's state change after it was accepted and the bundler cannot be made to simulate work it will never be paid for. In 7.0 it was off unless enabled
* Account and paymaster validation data are judged separately, as EntryPoint v0.9 does, so a paymaster aggregator or a paymaster signature failure can no longer pass as an account result, and block-number validity windows are honoured next to time windows
* The mempool, the reputation store and the receipt caches are bounded, a full mempool admits a new operation only by evicting a cheaper one from another sender, and the in-memory and RocksDB mempools share one admission rule, so both behave the same under the same load
* The JSON-RPC host refuses oversized bodies and batches before parsing, malformed envelopes and ids, and never returns exception text

## Nethereum.AccountAbstraction.Bundler — Admission

- Account and paymaster validation data are checked separately, as EntryPoint v0.9 does:
  - an account signature failure is `AA24`, and a paymaster signature failure or any paymaster aggregator is `AA34`;
  - a block range outside the next block is `AA27` for the account and `AA37` for the paymaster; a time range is checked as before;
  - a range is a block range when `validAfter` and `validUntil` both carry the `0x800000000000` flag.
- `MempoolEntry.ValidAfterBlock` and `ValidUntilBlock` record a block window next to the time window. Entries whose block window has ended, and the same sender's later entries, are removed before bundle candidates are chosen.
- The prefund check sums a sender's operations across every nonce key.
- `BundlerConfig` and `BundlerRpcServerConfig`:
  - `EnableERC7562Validation` defaults to `true`, `MaxVerificationGas` to 500,000 and `MaxUserOperationSize` to 8,192 bytes;
  - `MaxUnstakedEntityMempoolCount` (10) and `MaxOpsAllowedUnstakedEntity` (10,000) limit unstaked entities;
  - `ReceiptCacheSize` (10,000), `ReceiptNegativeCacheSize` (10,000) and `ReceiptNegativeCacheSeconds` (12) bound the receipt caches.
- A full mempool (`BundlerConfig.MaxMempoolSize`) admits a new operation only by evicting the lowest-priority-fee pending operation of another sender, and only when the new one pays more; otherwise it is `RejectedFull`.
- The in-memory and RocksDB mempools share one admission rule (`MempoolAdmission`, `MempoolAdmissionDecision`) and one set of state transitions. A failed or included operation's sender and nonce can be submitted again, and marking an operation failed never demotes an included one. Both mempools implement `IMempoolEvictionLog`, `IMempoolInFlight` and `IMempoolPendingPool`.

## Nethereum.AccountAbstraction.Bundler.RpcServer — JSON-RPC

- Envelope members are matched case-sensitively. An `id` that is not a string, number or null gets `-32600` with a null id, and the request is not run. Notifications get no response.
- `BundlerRpcServerConfig.MaxBatchSize` (1,000) caps a batch before it is parsed, and `MaxRequestBodyBytes` (5 MiB) caps the request body.
- Invalid parameters are `-32602`, parsed through `UserOperationRpcParser`. `-32603` is always "Internal error", with no exception text.
- Each response in a batch is serialised on its own, so one that cannot be serialised becomes an "Internal error" entry instead of failing the batch.

## Nethereum.AccountAbstraction — UserOperation Hash, EIP-7702 and Gas

- The userOpHash covers `paymasterDataKeccak` (the `PAYMASTER_SIG_MAGIC` suffix) for every EntryPoint version.
- `ValidationResult.Merge` treats a paymaster failure or a paymaster aggregator as a failure.
- The EIP-7702 nonce is read with `getNonce(sender, 0)`, and `UserOperationConverter` round-trips the `"0x7702"` factory. The `AAEIP7702Utils` helpers that build an initCode layout the EntryPoint does not read are obsolete; use `BuildInitCode("0x7702", factoryData)`.
- `TransactionExecutorGasEstimator` simulates in call mode and calls the factory from the EntryPoint's `senderCreator()`. Before this, a simulated call from a contract sender was rejected, and the account-deployment estimate always fell back to a flat 32,000. `EvmEstimationResult` carries the call's `ReturnData`.
- On Amsterdam, the bundler simulates with the EIP-8037 transaction gas cap, so the new account's state gas is measured as it is in the bundle. An account-creating operation's estimate no longer fails bundling with `AA13`.

## Nethereum.AccountAbstraction.Bundler.RocksDB

- `RocksDbUserOpMempool(BundlerRocksDbManager, BundlerRocksDbOptions, ILogger?)` and `RocksDbReputationStore(BundlerRocksDbManager, ReputationConfig?, ILogger?)` take a logger; the 7.0 constructors remain.
- Persisted entries carry the block window as a trailing field; rows written by 7.0 still read.
- Opening the mempool returns every submitted operation to pending, so a restart never leaves one waiting for a receipt nothing will reconcile; the on-chain nonce check drops the ones that were mined.

## Nethereum.WebAuthn

- `WebAuthnResponseParser` accepts a COSE or SPKI public key only if it has 32-byte coordinates of a non-zero point on P-256, and an SPKI key only if it carries the prime256v1 OID.

## Breaking Changes

- `BundlerRpcServerConfig.MaxVerificationGas` defaults to 500,000 (it was 10,000,000), and `BundlerConfig.MaxVerificationGas` to 500,000 (it was 1,500,000). On Amsterdam, an account-creating UserOperation needs about 1.6 million verification gas, so a host that accepts them sets a higher limit.
- `EnableERC7562Validation` defaults to `true` (it was `false`), and so does the `enableErc7562Validation` parameter of `InProcessBundlerHost.StartBundler`, so operations that break the ERC-7562 opcode and storage rules are refused.
- `BundlerRpcServerConfig.CreateAppChainConfig()` turns ERC-7562 validation on and sets `MaxUserOperationSize` 131,072 and `MaxVerificationGas` 10,000,000.
- With the default configuration, the bundler refuses UserOperations over 8,192 bytes, batches over 1,000 requests and request bodies over 5 MiB.
- A JSON-RPC request with a wrongly cased envelope member, or an `id` that is not a string, number or null, is refused.
- For an operation whose `paymasterAndData` ends with the paymaster-signature magic suffix, the userOpHash now covers `paymasterDataKeccak`, so a hash 7.0 computed for such an operation differs.
- `WebAuthnResponseParser` rejects public keys it used to accept.

## Build & Packaging

- `Nethereum.AccountAbstraction.Bundler.RocksDB` packs its README and is strong-name signed with the shared key.
