// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IVerifier} from "./IVerifier.sol";

/// @title CalldataStarkVerifier — Validates combined STARK hash + compressed block data
/// @notice ProofSystem 5: SubmitCompressedBlocksWithStarkProofHash
/// Proof bytes: [stark_hash:32][version:1][compression:1][compressed_data:variable]
/// On-chain: validates both the hash and the envelope structure.
/// Off-chain: verify STARK via blob + decompress and verify block headers.
contract CalldataStarkVerifier is IVerifier {
    uint8 constant ENVELOPE_VERSION = 1;
    uint8 constant MAX_COMPRESSION_ALGO = 2;

    function verify(bytes calldata proof, uint256[] calldata publicInputs) external pure returns (bool) {
        if (proof.length < 35) return false; // 32 hash + 2 envelope header + 1 data minimum

        bytes32 starkHash;
        assembly { starkHash := calldataload(proof.offset) }
        if (starkHash == bytes32(0)) return false;

        uint8 version = uint8(proof[32]);
        if (version != ENVELOPE_VERSION) return false;

        uint8 compression = uint8(proof[33]);
        if (compression > MAX_COMPRESSION_ALGO) return false;

        if (publicInputs.length < 6) return false;
        if (publicInputs[0] == 0) return false;

        return true;
    }
}
