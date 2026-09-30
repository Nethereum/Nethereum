# RPC & Web3 — Nethereum 7.0

Nethereum 7.0 brings the core client stack — `Nethereum.JsonRpc.*`, `Nethereum.RPC`, `Nethereum.Web3`, `Nethereum.Accounts` and `Nethereum.Contracts` — up to the post-Prague / Amsterdam JSON-RPC surface. These are the oldest packages in the library, so this note is the 7.0 delta only, not a re-description of the whole API. New this release: typed clients for the post-Prague `eth` methods (`eth_config` EIP-7910, `eth_simulateV1`, `eth_capabilities`, `eth_getBlockAccessList` EIP-7928, and the `txpool_*` family), a spec-checked Engine API DTO set, EIP-7702 authorization-list signing wired through the transaction managers, EIP-7708 ETH-transfer-log querying through the ordinary event machinery, an ENS Universal Resolver with SSRF-hardened CCIP-Read, a Permit2 service moved into `Nethereum.Contracts` from `Nethereum.Uniswap` (a breaking namespace change), emitter-scoped event decoding, and a JSON-RPC client that no longer throws on a string error `code`. The client-flavor packages (`Geth`, `Parity`, `Quorum`, `Besu`, `RSK`, `Optimism`) carry no code change this release beyond documentation.

## Nethereum.RPC — Post-Prague / Amsterdam eth methods

New typed request handlers for the JSON-RPC methods that landed with Prague and Amsterdam, each surfaced on `IEthApiService` / `IEthApiBlockService` so they compose like every other `web3.Eth.*` call.

* `eth_config` (EIP-7910) — `web3.Eth.Config` (`EthConfig` → `ChainConfiguration`) returns the client's `current` / `next` / `last` fork with its `activationTime`, `forkId`, per-fork `blobSchedule`, `precompiles` and `systemContracts`
* `eth_simulateV1` — `web3.Eth.SimulateV1` (`EthSimulateV1`) takes an `EthSimulateInput` of `BlockStateCall`s (each with `BlockOverrides`, address-keyed `AccountOverride` state overrides, and a list of `TransactionInput` calls) and returns `EthSimulateBlockResult` / `EthSimulateCallResult`
* `eth_capabilities` — `web3.Eth.Capabilities` (`EthCapabilities` → `EthCapabilitiesResult`) with per-resource head/effective-resource and delete-strategy shapes
* `eth_getBlockAccessList` (EIP-7928) — `web3.Eth.Blocks.GetBlockAccessList` (`EthGetBlockAccessList` → `List<AccountAccess>`), the block's declared account-access list
* `txpool_content` / `txpool_contentFrom` / `txpool_status` — the `TxPool` service (`ITxPoolApiService`) with `TxPoolContentResponse`, `TxPoolContentFromResponse`, `TxPoolStatusResponse` and `PendingTransactionInfo`, now reachable from `web3.TxPool`
* The `ApiMethods` enum gains these plus `eth_blobBaseFee`, `eth_getStorageValues`, `debug_getRawBlockAccessList`, `debug_traceBlockByNumber` / `debug_traceBlockByHash` (method names, not all with a dedicated typed handler); the `engine_*` V4/V5/V6 wire names are added to the separate `UnsupportedApiMethods` enum (named, not served here)

## Nethereum.RPC — Engine API DTOs

A typed DTO set for the consensus-layer Engine API, checked field-by-field against the official `execution-apis` OpenRPC spec so the shapes stay wire-correct across payload versions.

* Execution payloads `ExecutionPayloadV1`–`V4`, `PayloadAttributesV1`–`V4`, `ForkchoiceStateV1`, `ForkchoiceUpdatedResponseV1`, `PayloadStatusV1`, `GetPayloadV4Response` / `GetPayloadV6Response`, `BlobsBundleV1`, `ClientVersionV1`
* Every DTO's JSON property names are validated against the assembled `execution-apis` OpenRPC component schemas

## Nethereum.RPC — Account Abstraction (ERC-4337) RPC shapes

The `eth_getUserOperationByHash` and `eth_estimateUserOperationGas` client DTOs were brought to the ERC-4337 spec response shape.

* `EthGetUserOperationByHash` returns a `UserOperationByHashResult` wrapper (`userOperation` + `entryPoint` / `blockNumber` / `blockHash` / `transactionHash`, null for a still-pending op) rather than a bare UserOperation
* `EthEstimateUserOperationGas` / `UserOperationGasEstimate` carry the paymaster gas fields and accept an address-keyed state-override set

## Nethereum.RPC — DTO, mapper & chain-feature additions

Supporting DTOs and mappers for the new methods, plus a correctness fix to native-currency lookup.

* `AccountOverride` and `BlockOverrides` — the state/block override shapes shared by `eth_call`, `eth_estimateGas` and `eth_simulateV1`
* New mappers `ReceiptRPCMapper`, `WithdrawalRPCMapper`, `SignatureRpcMapper`, and Amsterdam fields threaded through `Block`, `Transaction`, `TransactionReceipt`, `FilterLog` and `TransactionInput`
* `ChainDefaultFeaturesServicesRepository.GetDefaultChainFeature(chainId)` returns a chain's own native currency and is `null` for an unknown chain rather than assuming ether
* `eth_simulateV1` results carry a typed `EthSimulateCallError` (`Message`, `Code`, `Data`); `CallInput` accepts the spec name `input` as an alias for `data`; `FeeHistoryResult` carries `BaseFeePerBlobGas` and `BlobGasUsedRatio`; `Block.BaseFeePerGas` is omitted for pre-London blocks
* `EtherTransferService.CalculateTotalAmountToTransferWholeBalanceInEtherAsync(address, toAddress, …)` overloads price the transfer to a specific recipient

## Nethereum.JsonRpc.Client — error-code tolerance

A JSON-RPC error object whose `code` is a string (as some nodes and ERC-4337 bundlers return, e.g. `{"code":"INVALID_ARGUMENT"}`) no longer throws a parse exception; the `message` is always surfaced. Integer codes keep deserializing byte-identically, on both the Newtonsoft and `System.Text.Json` paths.

* `RpcError.Code` deserializes a string code to a numeric value where possible and a sentinel `0` for a non-numeric one, never throwing

## Nethereum.Accounts — EIP-7702 signing & AccountAbstractionAccount

The signing transaction managers can now assemble and sign the EIP-7702 authorization list, and a read-only account type carries an injected signing service for smart-account (ERC-4337) use.

* `AccountSignerTransactionManager` signs each unsigned `Authorisation` in a transaction's `AuthorisationList` (and drains a `NextRequest7022Authorisations` queue) after the nonce is resolved, via `SignAuthorisationAsync` / `Authorisation7702Signer`
* `AccountAbstractionAccount(address, IAccountSigningService)` — a `ViewOnlyAccount` that cannot send a raw transaction (no tx key) but carries a live `SignTypedDataV4` for signing UserOperations, and rejects a null signing service

## Nethereum.Web3 & Nethereum.RPC — ether-transfer gas resolution

`EtherTransferService.TransferEtherAsync` now uses the node's gas estimate when it can (EIP-2780's 21,000 no longer covers an account that must be created, which EIP-8037 prices higher), and falls back to the default only when the node cannot estimate — instead of a fixed default that survives a successful estimate. `web3.TxPool` is wired onto `Web3`.

## Nethereum.Contracts — EIP-7708 ETH transfer logs & emitter-scoped events

EIP-7708 (Amsterdam) makes an ETH movement emit the same `Transfer(address,address,uint256)` event an ERC-20 token does — identical topic0, indexed count and data layout, differing only by the emitting address (the protocol's system address). Since every existing "is this an ERC-20 transfer" check answered from topic0 alone, that check now answers yes for every ETH send on an Amsterdam chain, so the library gains an address-scoped decode path.

* `web3.Eth.EthTransfers` (`EthTransferService`, EIP-7708) and `EthTransferLogExtensions` — query and decode ETH-movement transfer logs through the ordinary event machinery, discriminating token-vs-ETH by emitter
* `Erc20TransferEventTopic.Prefixed` / `.Unprefixed` (in `Nethereum.Model`) — the canonical Transfer topic, verified equal to the ABI-derived signature
* Emitter-scoped event decoding — `IsLogForEventEmittedBy` / `IsLogForEventEmittedByAny`, `DecodeEventEmittedBy`, `DecodeAllEventsEmittedBy` / `DecodeAllEventsEmittedByAny` (`EventExtensions`) match and decode a log only when it comes from a given contract (or set of contracts); an event handler bound to a contract answers the same question with `EventBase.IsLogForEventEmittedByThisContract(log)`
* `ContractRevertExceptionHandler.HandleContractRevertException(string encodedErrorData)` — a new overload that decodes revert data you already hold, alongside the existing `RpcResponseException` overload

## Nethereum.Contracts — ENS Universal Resolver & hardened CCIP-Read

A typed ENS Universal Resolver service (forward/reverse/record/text resolution through the single on-chain UniversalResolver), and the CCIP-Read (EIP-3668) off-chain gateway path hardened against SSRF and extended with ENSIP-21 local batch-gateway handling.

* `web3.Eth.GetEnsUniversalResolverService()` (`ENSUniversalResolverService`, generated `UniversalResolverService` + `Multicallable`) — resolve an address, a record, multiple records, or texts via the Universal Resolver, with `EnsRecord` results; `ENSService` can default to it
* CCIP-Read SSRF hardening (`EnsCCIPService.ValidateCcipUrl`) — the gateway URL comes from an untrusted resolver, so only `https` is allowed and hosts resolving to loopback / private / link-local / reserved / CGNAT destinations are refused (`CCIPReadUrlValidationException`)
* ENSIP-21 local batch gateway (`BatchGatewayRequest`, from the generated batch-gateway contract definition) — a batched off-chain lookup is decoded and each inner EIP-3668 read runs through the same SSRF-validated per-URL fetch, so batching cannot bypass the protection

## Nethereum.Contracts — Permit2 (breaking: moved out of Nethereum.Uniswap)

The Permit2 contract service and EIP-712 signing types that 6.1.0 shipped in `Nethereum.Uniswap` are removed from that package and now live in the core packages, consumed like the other built-in standards.

* `web3.Eth.GetPermit2Service()` returns `Nethereum.Contracts.Standards.Permit2.Permit2Service` (constructor `(IEthApiContractService, contractAddress = canonical Permit2 address)`), with `PermitTransferFrom`, `PermitBatchTransferFrom`, `SignatureTransferDetails`, `AllowanceTransferDetails` and `TokenSpenderPair` in the same namespace
* The signer is `PermitSigner` in `Nethereum.Signer.EIP712.Permit2`; the message types `PermitSingle`, `PermitBatch`, `PermitDetails`, `TokenPermissions`, `PermitTransferFromWithSpender` and `PermitBatchTransferFromWithSpender` are in `Nethereum.ABI.EIP712.Permit2`. `PermitSigner.SignPermitTransferFrom` / `SignPermitBatchTransferFrom` take the `…WithSpender` types
* **No replacement** for the 6.1.0 `Permit2Service.GetSinglePermitWithSignatureAsync` / `GetBatchPermitWithSignatureAsync` helpers or `SignedPermit2<T>`: read the nonce with `Permit2Service.AllowanceQueryAsync(owner, token, spender)` and sign with `PermitSigner.SignPermitSingle` / `SignPermitBatch`
* Update `using` directives from `Nethereum.Uniswap.Permit2`, `Nethereum.Uniswap.Permit2.ContractDefinition` and `Nethereum.Uniswap.Core.Permit2.ContractDefinition` to the namespaces above. The Universal Router commands `Permit2PermitCommand`, `Permit2PermitBatchCommand`, `Permit2TransferFromCommand` and `Permit2TransferFromBatchCommand` keep their shape but use the relocated types

## Client-flavor packages — documentation only

`Nethereum.Geth`, `Nethereum.Parity`, `Nethereum.Quorum`, `Nethereum.Besu`, `Nethereum.RSK` and `Nethereum.Optimism` have no shipped code change since 6.1.0; their READMEs were refreshed for 7.0 (`Nethereum.RSK` and `Nethereum.Parity` unchanged entirely). Their post-Prague/Amsterdam behaviour comes through the shared `Nethereum.RPC` / `Nethereum.Web3` surface above.
