# Anchor Submission Strategies & Verifier Contracts

## Architecture

```
submitAnchor(anchor, proof)
  → contract checks: proofSystems[anchor.proofSystem]
  → if requiresProof: IVerifier(verifier).verify(proof, publicInputs)
  → stores: latestAnchor, blockHashesRoot
  → if proofSystem > 0: stores anchorCommitment
```

The `IVerifier` interface:

```solidity
interface IVerifier {
    function verify(bytes calldata proof, uint256[] calldata publicInputs) external view returns (bool);
}
```

## On-Chain Proof Systems

The `AppChainAnchor` contract has 3 on-chain proof systems, matching the Solidity `ProofSystem` enum and the C# `AnchoringOnChainProofSystem` enum:

| ID | Enum Name | Verifier | requiresProof | Verification |
|---|---|---|---|---|
| 0 | NoProof | address(0) | false | Trust operator |
| 1 | StarkHashOffChain | optional | false | Off-chain STARK verification |
| 2 | SnarkOnChain | required | **true** | On-chain Groth16 verification |

## C# Strategies → On-Chain Proof System

7 C# strategies map to 3 proof system IDs. The **Data Availability** dimension (None/Calldata/BlobReference) is encoded in the `proof` bytes — the contract only cares about the `proofSystem` field in the `AggregatedAnchor` struct.

| C# Strategy | DA | Proof Mode | proofSystem | Verifier |
|---|---|---|---|---|
| `NoDA_NoProof_CommitmentOnly` | None | None | 0 | address(0) |
| `Calldata_NoProof_SyncOnly` | Calldata | None | 0 | address(0) |
| `NoDA_StarkHash_OffChainVerifiable` | None | StarkHash | 1 | StarkBlobCommitmentVerifier |
| `Calldata_StarkHash_SyncAndOffChainVerifiable` | Calldata | StarkHash | 1 | CalldataStarkVerifier |
| `NoDA_SnarkOnChain_TrustlessVerification` | None | SnarkOnChain | 2 | Groth16Verifier |
| `Calldata_SnarkOnChain_SyncAndTrustlessVerification` | Calldata | SnarkOnChain | 2 | Groth16Verifier |
| `BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA` | BlobReference | SnarkOnChain | 2 | Groth16Verifier |

## Verifier Contracts

### CalldataFormatVerifier

Validates compressed block header calldata format. Used with Calldata DA strategies.

Checks:
- Proof length >= 3 bytes (version + compression + data)
- Version byte is 1
- Compression algorithm byte is valid (0=None, 1=Zlib, 2=Brotli)

### StarkBlobCommitmentVerifier

Validates STARK proof hash with EIP-4844 blob commitment reference.

Checks:
- Proof is exactly 32 bytes (a hash)
- Hash is non-zero

### CalldataStarkVerifier

Combined calldata + STARK hash verification.

Checks:
- Proof length >= 35 bytes (32 hash + 2 envelope header + 1 data minimum)
- First 32 bytes are non-zero (STARK proof hash)
- Remaining bytes follow compressed envelope format (version=1, compression 0-2)

### PipelinePayloadVerifier

Validates multi-section encoded pipeline payloads.

Checks:
- Version byte is 1
- Section count > 0
- Total decoded length matches proof length
- Required section types present

### MockProofVerifier

Always returns true. For testing only.

### ZiskVerifierAdapter

Adapts the ZisK zkVM Groth16 verifier (`IZiskVerifier`) to the standard `IVerifier` interface.

## SNARK Public Inputs

When `proofSystem=2` (SnarkOnChain), the contract builds 11 public inputs for on-chain verification:

```
[0]  chainId
[1]  anchorVersion
[2]  proofSystem
[3]  startBlock
[4]  endBlock
[5]  preStateRoot (previous anchor's postStateRoot)
[6]  postStateRoot
[7]  startBlockHash (previous anchor's endBlockHash)
[8]  endBlockHash
[9]  blockHashesRoot
[10] manifestHash
```

## Graduation Path

Chains upgrade their minimum proof system over time (one-way — `raiseMinimumProofSystem` can only increase):

```
Stage 1: minimumProofSystem=0 (NoProof)
  → Dev/testing, trust operator
Stage 2: minimumProofSystem=1 (StarkHashOffChain)
  → Cryptographic proof available, off-chain verification
Stage 3: minimumProofSystem=2 (SnarkOnChain)
  → Full on-chain trustless Groth16 verification
```
