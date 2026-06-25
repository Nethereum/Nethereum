// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IVerifier} from "./IVerifier.sol";

/// @title StarkBlobCommitmentVerifier — Validates STARK proof commitment hash
/// @notice ProofSystem 2: SubmitStarkProofHashWithBlobReference
/// Proof bytes: 32-byte commitment hash of STARK proof stored in EIP-4844 blobs.
/// On-chain: validates hash is present and non-zero.
/// Off-chain: download blob, verify STARK, check SHA256(proof) matches commitment.
contract StarkBlobCommitmentVerifier is IVerifier {
    function verify(bytes calldata proof, uint256[] calldata publicInputs) external pure returns (bool) {
        if (proof.length != 32) return false;

        bytes32 commitment;
        assembly { commitment := calldataload(proof.offset) }
        if (commitment == bytes32(0)) return false;

        if (publicInputs.length < 6) return false;
        if (publicInputs[0] == 0) return false;

        return true;
    }
}
