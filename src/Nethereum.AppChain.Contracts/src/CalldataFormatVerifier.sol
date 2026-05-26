// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IVerifier} from "./IVerifier.sol";

/// @title CalldataFormatVerifier — Validates compressed block data envelope format
/// @notice ProofSystem 1: SubmitCompressedBlocksAsCalldata
/// Proof bytes: [version:1][compression:1][compressed_data:variable]
/// On-chain: validates envelope structure only (decompression is off-chain).
/// Off-chain: decompress, verify block headers match anchor block range.
contract CalldataFormatVerifier is IVerifier {
    uint8 constant VERSION = 1;
    uint8 constant MAX_COMPRESSION_ALGO = 2; // 0=None, 1=Zlib, 2=Brotli

    function verify(bytes calldata proof, uint256[] calldata publicInputs) external pure returns (bool) {
        if (proof.length < 3) return false;

        uint8 version = uint8(proof[0]);
        if (version != VERSION) return false;

        uint8 compression = uint8(proof[1]);
        if (compression > MAX_COMPRESSION_ALGO) return false;

        if (publicInputs.length < 6) return false;
        if (publicInputs[0] == 0) return false;

        return true;
    }
}
