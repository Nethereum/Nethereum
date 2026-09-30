// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IVerifier} from "./IVerifier.sol";

/// @title MockProofVerifier — Test verifier that validates proof format without cryptography
/// @notice Accepts both batch proofs (11 public inputs) and per-block proofs (6 public inputs).
/// Validates: 256-byte proof size, non-zero commitment, non-zero chain identity.
/// Does NOT verify elliptic curve pairing — replace with real Groth16Verifier for production.
/// @dev Same interface as production verifiers. Swap at deployment time via proof system registry.
contract MockProofVerifier is IVerifier {
    /// @inheritdoc IVerifier
    function verify(bytes calldata proof, uint256[] calldata publicInputs) external pure returns (bool) {
        if (proof.length != 256) return false;
        if (publicInputs.length < 6) return false;

        bytes32 piA;
        assembly { piA := calldataload(proof.offset) }
        if (piA == bytes32(0)) return false;

        if (publicInputs[0] == 0) return false;

        if (publicInputs.length == 11) {
            if (publicInputs[3] > publicInputs[4]) return false;
        }

        return true;
    }
}
