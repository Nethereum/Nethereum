# ENS, MUD & DID — Nethereum 7.0

Nethereum 7.0 adds decentralized-identity support to the client-extension stack: a new `Nethereum.DID` package implementing the W3C DID Core document model, a DID URL parser and dual (Newtonsoft + System.Text.Json) serialization, and a new `Nethereum.DID.EthrDID` package that resolves `did:ethr` identifiers to W3C DID Documents from the on-chain ERC-1056 registry. These two packages are the whole code delta in this area. The remaining client-extension packages here — ENS, the MUD framework (`Nethereum.Mud*`), SIWE (`Nethereum.Siwe*`), Gnosis Safe and GSN — all pre-date 6.1.0 and carry no shipped behaviour change in this range beyond a documentation pass, with one small MUD decoding correction (empty string, not null, for an empty `string` field). This is a 7.0 delta note: it describes only what changed since the `6.1.0` tag, not the whole of each long-standing package.

## Nethereum.DID — W3C DID Core model, URL parser & serialization

New package: the W3C Decentralized Identifiers (DID) Core v1.0 document model in .NET, a DID URL parser, and JSON serialization on both the Newtonsoft and (net6.0+) System.Text.Json paths. No node or network — this is the pure data model and codec that `Nethereum.DID.EthrDID` and any other DID method build on. Targets net451 through net10.0.

* The document model — `DidDocument` carries `@context`, `id`, `alsoKnownAs`, `controller`, `verificationMethod`, the five verification relationships (`authentication`, `assertionMethod`, `keyAgreement`, `capabilityInvocation`, `capabilityDelegation`), `service`, and a JSON-extension-data bag for unknown properties; `DidDocument.CreateDefault(id)` seeds a document with the DID v1 context
* `VerificationMethod` — the key material shapes (`publicKeyMultibase`, `publicKeyJwk`, `publicKeyHex`, `publicKeyBase58`, `publicKeyBase64`, `blockchainAccountId`, `ethereumAddress`), and `Service` for a service endpoint
* `VerificationRelationship` — the DID Core "either a reference or an embedded verification method" union, with `IsReference` / `IsEmbedded` discriminators and an `Id` that resolves through both forms
* DID URL parsing — `DidUrlParser.Parse` / `DidUrlParser.TryParse` split a `did:method:id;params/path?query#fragment` string into a `DidUrl` (`Method`, `Id`, `Path`, `Query`, `Fragment`, `Params`), preserving the original URL, honouring percent-encoding and colon-segmented ids, and rejecting a malformed DID with a `FormatException` (empty/null with an `ArgumentException`)
* Shape-faithful JSON — the `@context` and `controller` fields serialize as a bare string when single and an array when multiple (`ContextConverter` / `SingleOrArrayConverter` and their System.Text.Json twins `ContextSystemTextJsonConverter` / `SingleOrArraySystemTextJsonConverter`), a verification relationship serializes as either a reference string or an embedded object (`VerificationRelationshipConverter`), null collections are omitted, and unknown properties round-trip through extension data
* `DidConstants` — the standard context URIs and verification-method type names (`EcdsaSecp256k1VerificationKey2019`, `EcdsaSecp256k1RecoveryMethod2020`, `Ed25519VerificationKey2018/2020`, `JsonWebKey2020`, …)

## Nethereum.DID.EthrDID — did:ethr resolver over ERC-1056

New package: the `did:ethr` method, resolving an Ethereum-address DID to a W3C DID Document by reading the ERC-1056 `EthereumDIDRegistry` contract on-chain. It ships the typed registry contract service and a resolver that walks the registry's change-log events backwards to build the DID document.

* `EthrDidResolver.ResolveAsync(did)` — takes a `did:ethr:<address>` (or `did:ethr:<chainId>:<address>`; the chain segment is parsed but does not select the registry — pass the registry address to the `EthrDidResolver` constructor, default mainnet) identifier, reads the current identity owner, walks the `DIDOwnerChanged` / `DIDDelegateChanged` / `DIDAttributeChanged` events from the last-changed block backwards via each event's `previousChange` link, and builds a `DidDocument` with the controller verification method, delegate verification methods (`veriKey` → assertion, `sigAuth` → authentication), attribute-declared public keys (`did/pub/...`, hex/base64/base58) and service endpoints (`did/svc/...`)
* `EthereumDIDRegistryService` — the generated typed contract service for ERC-1056 (owner management, delegates, attributes, and their signed variants), deployable and queryable like any other Nethereum contract service
* `EthrDidConstants` — the method name, well-known registry addresses per chain (mainnet / Sepolia / Polygon, via `GetDefaultRegistryAddresses()`), the delegate-type and attribute-prefix constants; a resolver defaults to the mainnet registry when no address is supplied

## Nethereum.Mud — empty-string decoding fix (+ docs)

The MUD framework packages pre-date 6.1.0 and carry no behavioural change in this range other than one decoding correction in `Nethereum.Mud`, plus a from-source documentation rework across the four packages.

* `ValueEncoderDecoder` now decodes an empty `string` field to `string.Empty` rather than the ABI type's generic default — an empty dynamic field round-trips as `""` instead of `null` (`GetDefaultValue` special-cases `StringType`)
* Documentation only, no code: `Nethereum.Mud.Contracts`, `Nethereum.Mud.Repositories.EntityFramework`, `Nethereum.Mud.Repositories.Postgres` (README rework)

## ENS, SIWE, Gnosis Safe & GSN — documentation only

These packages pre-date 6.1.0 and ship no code change in this range; their READMEs were refreshed for 7.0.

* `Nethereum.ENS` — no source or README change in-range. The ENS 7.0 delta (the Universal Resolver service and hardened CCIP-Read) lives in `Nethereum.Contracts` and is written up in the RPC/Web3 release note
* `Nethereum.Siwe` — README only; `Nethereum.Siwe.Core` — no change at all
* `Nethereum.GnosisSafe` — README only. The Uniswap Permit2 service (sometimes reached alongside Safe workflows) also ships in `Nethereum.Contracts` and is written up in the RPC/Web3 release note
* `Nethereum.GSN` — README only
