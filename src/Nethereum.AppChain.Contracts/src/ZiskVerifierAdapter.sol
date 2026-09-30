// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IVerifier} from "./IVerifier.sol";
import {IZiskVerifier} from "./IZiskVerifier.sol";

/// @title ZiskVerifierAdapter — Bridges Zisk's native verifier to AppChainAnchor's IVerifier
/// @notice Adapts IZiskVerifier.verifySnarkProof() to IVerifier.verify(proof, publicInputs).
/// Each adapter instance is bound to a specific EVM binary (programVK + rootCVadcopFinal).
/// @dev The adapter extracts Zisk-formatted public values from our public inputs array,
/// packs them into the format IZiskVerifier expects, and delegates verification.
///
/// Public inputs mapping (batch proof — 11 values):
///   [0] chainId, [1] anchorVersion, [2] proofSystem, [3] startBlock, [4] endBlock,
///   [5] preStateRoot, [6] postStateRoot, [7] startBlockHash, [8] endBlockHash,
///   [9] blockHashesRoot, [10] manifestHash
///
/// Public inputs mapping (per-block proof — 6 values):
///   [0] chainId, [1] blockNumber, [2] blockHash, [3] preStateRoot,
///   [4] postStateRoot, [5] proofSystem
///
/// The proof bytes contain: [ziskProofBytes][publicValuesBytes]
/// The adapter splits them and passes to IZiskVerifier.
contract ZiskVerifierAdapter is IVerifier {

    IZiskVerifier public immutable ziskVerifier;
    uint64[4] public programVK;
    uint64[4] public rootCVadcopFinal;

    /// @param _ziskVerifier Address of the deployed Zisk SNARK verifier
    /// @param _programVK Verification key for the Nethereum EVM RISC-V binary
    /// @param _rootCVadcopFinal VADCOP final root for this proving configuration
    constructor(
        address _ziskVerifier,
        uint64[4] memory _programVK,
        uint64[4] memory _rootCVadcopFinal
    ) {
        ziskVerifier = IZiskVerifier(_ziskVerifier);
        programVK = _programVK;
        rootCVadcopFinal = _rootCVadcopFinal;
    }

    /// @inheritdoc IVerifier
    /// @notice Verifies a Zisk SNARK proof via the adapter
    /// @dev The proof parameter contains packed [proofLength(4 bytes)][proofBytes][publicValuesBytes].
    /// Public inputs from the anchor contract are bound to the proof by hashing them into
    /// a commitment that must appear as the first 32 bytes of publicValues.
    function verify(bytes calldata proof, uint256[] calldata publicInputs) external view returns (bool) {
        if (publicInputs.length < 6) return false;
        if (proof.length < 4) return false;

        uint32 proofLen = uint32(bytes4(proof[0:4]));
        if (proof.length < 4 + proofLen) return false;

        bytes calldata ziskProof = proof[4:4 + proofLen];
        bytes calldata publicValues = proof[4 + proofLen:];

        if (publicValues.length < 32) return false;
        bytes32 inputsCommitment = keccak256(abi.encodePacked(publicInputs));
        bytes32 embeddedCommitment;
        assembly { embeddedCommitment := calldataload(publicValues.offset) }
        if (inputsCommitment != embeddedCommitment) return false;

        try ziskVerifier.verifySnarkProof(programVK, rootCVadcopFinal, publicValues, ziskProof) {
            return true;
        } catch {
            return false;
        }
    }
}
