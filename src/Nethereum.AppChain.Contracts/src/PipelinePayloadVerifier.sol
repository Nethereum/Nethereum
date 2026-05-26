// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IVerifier} from "./IVerifier.sol";

/// @title PipelinePayloadVerifier — Validates encoded pipeline payload format
/// @notice ProofSystem 3: SubmitEncodedPipelinePayload
/// Proof bytes: [version:1][stateModel:1][anchorKind:1][sectionCount:1][sections...]
/// On-chain: validates header structure and minimum section count.
/// Off-chain: decode all sections, validate state roots and commitments.
contract PipelinePayloadVerifier is IVerifier {
    uint8 constant CURRENT_VERSION = 1;
    uint8 constant SECTION_HEADER_SIZE = 6; // type(2) + length(4)

    function verify(bytes calldata proof, uint256[] calldata publicInputs) external pure returns (bool) {
        if (proof.length < 4) return false;

        uint8 version = uint8(proof[0]);
        if (version != CURRENT_VERSION) return false;

        uint8 sectionCount = uint8(proof[3]);
        if (sectionCount == 0) return false;

        uint256 offset = 4;
        for (uint8 i = 0; i < sectionCount; i++) {
            if (offset + SECTION_HEADER_SIZE > proof.length) return false;

            uint32 sectionLength = uint32(bytes4(proof[offset + 2:offset + 6]));
            offset += SECTION_HEADER_SIZE + sectionLength;
        }

        if (offset != proof.length) return false;

        if (publicInputs.length < 6) return false;
        if (publicInputs[0] == 0) return false;

        return true;
    }
}
