# Nethereum.AppChain.Contracts

Solidity smart contracts for the Nethereum AppChain anchoring and messaging system. Built with [Foundry](https://getfoundry.sh/).

## Contracts

### Core

| Contract | Purpose |
|---|---|
| `AppChainAnchor.sol` | Anchor registry — stores block anchors per AppChain with pluggable proof verification. `AggregatedAnchor` struct with 3 proof systems (NoProof, StarkHashOffChain, SnarkOnChain) and extensible registry. |
| `AppChainHub.sol` | Public AppChain registry and messaging hub. Chain ID reservation (prevents squatting via registration fee), cross-chain messaging with authorized senders, anchor storage, message acknowledgment with Merkle root checkpoints. Optional — AppChains deploy their own `AppChainAnchor` independently; the Hub provides discovery, coordination, and a standardised messaging layer. |
| `AppChainPolicy.sol` | Membership and policy governance — Merkle-root membership management (writers/admins/blacklist), configurable policy parameters (calldata limits, log limits, gas limits, sequencer), and epoch-based tree rebuilding. |
| `AppChainProofManager.sol` | Proof registry — manages per-block proof submissions from provers, tracks proof system upgrades. |

### Authority & Access Control

| Contract | Purpose |
|---|---|
| `SimpleAuthority.sol` | Authority contract with operator + per-chain authorized provers. Implements `IAuthority`. Supports operator validation, prover authorization/revocation, and ownership transfer. |
| `IAuthority.sol` | Interface for pluggable authority contracts. |

### Verification

| Contract | Purpose |
|---|---|
| `IVerifier.sol` | Standard verifier interface: `verify(bytes proof, uint256[] publicInputs) → bool`. All verifiers implement this. |
| `CalldataFormatVerifier.sol` | Validates compressed block header calldata format (version byte + Brotli/Zlib envelope). |
| `StarkBlobCommitmentVerifier.sol` | Validates STARK proof hash with EIP-4844 blob commitment reference. |
| `CalldataStarkVerifier.sol` | Combined calldata + STARK hash verification. |
| `PipelinePayloadVerifier.sol` | Validates multi-section encoded pipeline payloads (headers + proofs + DA reference). |
| `MockProofVerifier.sol` | Always-true verifier for testing. |
| `ZiskVerifierAdapter.sol` | Adapter wrapping the ZisK zkVM SNARK verifier (`IZiskVerifier`) to the `IVerifier` interface. |
| `IZiskVerifier.sol` | ZisK-specific verifier interface for Groth16 proofs. |

## Proof System Registry

The `AppChainAnchor` contract maps proof system IDs to verifier contracts via `registerProofSystem(uint8 id, address verifier, bool requiresProof)`:

| ID | Enum Name | Verifier | requiresProof | Verification |
|---|---|---|---|---|
| 0 | NoProof | address(0) | false | Trust operator |
| 1 | StarkHashOffChain | optional | false | Off-chain STARK verification |
| 2 | SnarkOnChain | required | **true** | Full on-chain Groth16 verification |

The registry is extensible — additional proof systems can be registered by the authority. 7 C# submission strategies (DA × Proof mode combinations) map to these 3 on-chain proof system IDs.

See [STRATEGY_VERIFIERS.md](STRATEGY_VERIFIERS.md) for the full strategy-to-verifier mapping.

## Build

```bash
forge build
```

## Test

```bash
forge test -vvv
```

## C# Code Generation

Generated Nethereum contract services live in:
- `src/Nethereum.AppChain.Anchoring/AppChainAnchor/` — AnchorService, definitions, extensions
- `src/Nethereum.AppChain.Anchoring/SimpleAuthority/`
- `src/Nethereum.AppChain.Anchoring/AppChainProofManager/`
- `src/Nethereum.AppChain.Anchoring/MockProofVerifier/`
- `src/Nethereum.AppChain.Anchoring/ZiskVerifierAdapter/`
- `src/Nethereum.AppChain.Anchoring/Hub/Contracts/AppChainHub/`
- `src/Nethereum.AppChain.Policy/Contracts/AppChainPolicy/`

Regenerate with:
```bash
dotnet nethereum generate from-abi --config .nethereum-gen.multisettings
```

## Security Model

> **PREVIEW** — Contracts are audited internally. External audit recommended before mainnet deployment.

**AppChainAnchor:**
- Authority-gated anchor submission — each chain registers its own `IAuthority` contract (e.g. `SimpleAuthority`)
- Proof system registry with `requiresProof` validation — verifier address required when proof is mandatory
- One-way `raiseMinimumProofSystem` prevents downgrade attacks
- `MockProofVerifier` provided for testing only — must not be registered on production chains

**AppChainHub:**
- ReentrancyGuard on all ETH-transferring functions (registration, messaging, withdrawals)
- EIP-2 compliant ECDSA signature verification (s-value check prevents malleability)
- Pull-payment pattern using `call{value}` for all withdrawals and refunds
- CEI (Checks-Effects-Interactions) ordering enforced — state updates before external calls
- Per-chain authorized sender allowlist for message submission
- Pluggable `IProofVerifier` per chain for anchor proof verification

**AppChainPolicy:**
- Merkle-root based membership (writers, admins, blacklist)
- Blacklist verification via proof in `invite()` — prevents re-adding banned addresses
- Epoch-based tree rebuilding for membership rotation
- Admin-gated policy parameter changes
