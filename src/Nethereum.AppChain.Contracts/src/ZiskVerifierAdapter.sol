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

    uint256 private constant COMMITMENT_WORDS = 8;
    uint256 private constant PUBLIC_VALUE_WORDS = 64;
    uint256 private constant ANCHOR_INPUTS = 11;
    uint256 private constant BLOCK_HASH_SLOT = COMMITMENT_WORDS + 3;
    uint256 private constant STATE_ROOT_SLOT = COMMITMENT_WORDS + 11;
    uint256 private constant PRE_STATE_ROOT_SLOT = COMMITMENT_WORDS + 36;
    uint256 private constant BLOCK_NUMBER_SLOT = COMMITMENT_WORDS + 44;
    uint256 private constant PARENT_HASH_SLOT = COMMITMENT_WORDS + 48;

    function _word(bytes calldata publicValues, uint256 slot) private pure returns (uint32) {
        uint256 offset = slot * 8;
        require(bytes4(publicValues[offset + 4:offset + 8]) == bytes4(0), "Non-canonical public value");
        return uint32(uint8(publicValues[offset]))
            | (uint32(uint8(publicValues[offset + 1])) << 8)
            | (uint32(uint8(publicValues[offset + 2])) << 16)
            | (uint32(uint8(publicValues[offset + 3])) << 24);
    }

    function _bytes32At(bytes calldata publicValues, uint256 firstSlot) private pure returns (bytes32) {
        uint256 value;
        for (uint256 i = 0; i < 8; i++) {
            value = (value << 32) | _word(publicValues, firstSlot + i);
        }
        return bytes32(value);
    }

    function _requireBoundToExecutedBlock(uint256[] calldata inputs, bytes calldata publicValues) private pure {
        uint256 blockNumber = uint256(_word(publicValues, BLOCK_NUMBER_SLOT))
            | (uint256(_word(publicValues, BLOCK_NUMBER_SLOT + 1)) << 32);
        require(inputs[3] == blockNumber && inputs[4] == blockNumber, "Anchor block not executed block");
        require(inputs[5] == uint256(_bytes32At(publicValues, PRE_STATE_ROOT_SLOT)), "Anchor pre-state not executed");
        require(inputs[6] == uint256(_bytes32At(publicValues, STATE_ROOT_SLOT)), "Anchor state root not executed");
        require(inputs[7] == uint256(_bytes32At(publicValues, PARENT_HASH_SLOT)), "Anchor parent hash not executed");
        require(inputs[8] == uint256(_bytes32At(publicValues, BLOCK_HASH_SLOT)), "Anchor block hash not executed");
    }

    function _pack(uint64[4] memory limbs) private pure returns (bytes32) {
        return bytes32(abi.encodePacked(limbs[0], limbs[1], limbs[2], limbs[3]));
    }

    /// @inheritdoc IVerifier
    /// @notice Verifies a Zisk SNARK proof via the adapter
    /// @dev The proof parameter contains packed [proofLength(4 bytes)][proofBytes][publicValuesBytes].
    /// Public inputs from the anchor contract are bound to the proof by hashing them into
    /// a commitment (keccak256 of the abi.encodePacked inputs) that the guest writes into the first eight public values, each a 32-bit word held in a 64-bit public.
    function verify(bytes calldata proof, uint256[] calldata publicInputs) external view returns (bool) {
        require(publicInputs.length == ANCHOR_INPUTS, "Bad anchor input count");
        if (proof.length < 4) return false;

        uint32 proofLen = uint32(bytes4(proof[0:4]));
        if (proof.length < 4 + proofLen) return false;

        bytes calldata ziskProof = proof[4:4 + proofLen];
        bytes calldata publicValues = proof[4 + proofLen:];

        require(publicValues.length == PUBLIC_VALUE_WORDS * 8, "Bad public values length");
        bytes32 inputsCommitment = keccak256(abi.encodePacked(publicInputs));
        for (uint256 i = 0; i < COMMITMENT_WORDS; i++) {
            if (bytes4(publicValues[i * 8 + 4:i * 8 + 8]) != bytes4(0)) return false;
        }
        if (inputsCommitment != _bytes32At(publicValues, 0)) return false;
        _requireBoundToExecutedBlock(publicInputs, publicValues);

        try ziskVerifier.verifySnarkProof(_pack(programVK), _pack(rootCVadcopFinal), publicValues, ziskProof) {
            return true;
        } catch {
            return false;
        }
    }
}
