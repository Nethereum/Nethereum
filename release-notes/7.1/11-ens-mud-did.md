# ENS, MUD, DID & SIWE — Nethereum 7.1

Nethereum 7.1 fixes bugs in these packages. Sign-In with Ethereum parses messages in linear time, uses each nonce once and verifies smart-wallet signatures. The MUD Postgres indexer keeps several worlds apart in one database and no longer skips rows of a block that is still being written. The ethr DID resolver drops revoked delegates and attributes. `Nethereum.ENS` now ships a package README.

## Security audit

Sign-In with Ethereum was audited with the wallet's sign-in path, and its parser as a decoder of untrusted text; each finding was fixed with a test that fails before the fix.

* The regular-expression parser took 36 seconds on a message with a 20 KB URI; it is now a linear line parser with a 64 KiB limit
* A garbled optional field such as `Expiration Time` was dropped silently, leaving a message that never expired
* A validated message could be used again, because its nonce was not consumed
* Contract-wallet signatures went through `ecrecover` and failed, so smart-wallet sign-in did not work, and the contract check did not compare the node's chain id with the message's

## Nethereum.Siwe.Core — Message Parsing and Building

- `SiweMessageParser` parses EIP-4361 messages line by line with no regular expression, and refuses a message longer than `SiweMessageParser.MaxMessageLength` (64 KiB).
- An optional field that is present but malformed now fails the parse, so a garbled `Expiration Time` is no longer dropped silently, leaving the message with no expiry. An empty domain or URI, or content after the last field, also fails. Trailing newlines are accepted.
- `SiweMessage` reads the EIP-4361 time forms, and an RFC 3339 time with more than 7 fractional digits is truncated rather than failing the time-window check.
- `SiweMessageStringBuilder` throws `ArgumentException` when `Domain`, `Statement`, `Uri`, `Request ID` or a resource contains CR or LF.

## Nethereum.Siwe — Nonces and Signatures

- `SiweMessageService.IsValidMessage` consumes the nonce when the message is valid, so a signed message signs in once.
- Only a 65-byte signature goes through ecrecover. Any other signature goes to the ERC-6492 and ERC-1271 checks, so smart-wallet sign-in works.
- A contract-wallet check requires the node's chain id to equal the message's `Chain ID`.
- `InMemorySessionNonceStorage` expires an entry at the message's `Expiration Time`, or after `DefaultSessionLifetime` (1 hour; `InMemorySessionNonceStorage(TimeSpan sessionLifetime)` sets another). Expired entries are pruned when entries are added, and the check-then-act race on a nonce is gone.

## Nethereum.Mud — Resource Registry

- `ResourceRegistry` is safe under concurrent first use: the same resource type resolves to one instance, and concurrent registration no longer corrupts the registry.

## Nethereum.Mud.Repositories.Postgres and EntityFramework — Indexer Fixes

- Record writes and deletes find the stored row by world address as well as table id and key. With two worlds in one database, the second world's write no longer overwrites the first world's record. A delete no longer throws "The property StoredRecord.AddressBytes is part of a key and so cannot be modified".
- The normaliser pages only through the last completed block (`MudPostgresStoreRecordsTableRepository.GetCompletedRecordsAsync(int pageSize, BigInteger startingBlockNumber, long lastProcessedRowId)`). It no longer reads part of a block the indexer is still writing, then misses the rest of it.
- `MudPostgresProcessingHostedService` and `MudPostgresNormaliserBackgroundService` take an `IServiceScopeFactory` and run each attempt in a new scope with a new `DbContext`. A retry after a failure no longer reuses a broken connection or change tracker. The previous constructors remain.
- `BlockProgressRepository(TDbContext context, CancellationToken cancellationToken)` passes the token to every EF call, so a stopping service cancels its progress reads and writes. The one-argument constructor is unchanged.

## Nethereum.DID.EthrDID — Resolution in Chain Order

- `EthrDidResolver` applies the registry events in block and log order and takes the controller from `identityOwner`. For each delegate and each attribute, the latest event wins, and the entry is kept only while its `validTo` is later than the latest block timestamp. That is the rule the registry contract applies, so revoked and expired delegates and attributes are no longer listed. Before this, a controller changed twice could resolve to the old owner.

## Nethereum.ENS — Package README

- `Nethereum.ENS` now packs a README: the contract services, deploying ENS and registering and resolving a name, name hashes, text record keys (`TextDataKey`), ABI content types (`AbiTypeContentType`) and registering a `.eth` name with `EthTLSService`.

## Breaking Changes

- `SiweMessageParser` rejects messages it used to accept: messages over 64 KiB, an address with characters that are not hex, malformed optional fields, an empty domain or URI, and trailing content.
- `SiweMessageStringBuilder` throws for CR or LF in the fields listed above.
- `SiweMessageService.IsValidMessage` returns `true` once per nonce. A caller that validated the same message twice gets `false` the second time, and `InvalidateSession` on that message afterwards throws with `InMemorySessionNonceStorage`, because the entry is already gone.

## Build & Packaging

- The `Nethereum.Siwe` and `Nethereum.Siwe.Core` packages pack their READMEs, and their package tags spell `Nethereum` correctly.
- `Nethereum.Mud.Repositories.Postgres` requires `Newtonsoft.Json` 13.0.4 or later, like every package in 7.1 (see 00).
- `Nethereum.ENS` takes `<Version>$(NethereumVersion)</Version>` and packs its README. The package-tag and version changes of the other packages in this slice are in 00.
