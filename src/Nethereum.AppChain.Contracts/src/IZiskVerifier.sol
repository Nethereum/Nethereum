// SPDX-License-Identifier: AGPL-3.0
pragma solidity ^0.8.20;

/// @title IZiskVerifier — Zisk zkVM native verifier interface
/// @notice From Zisk provingKeySnark. Verifies SNARK proofs with program-specific VK.
/// @dev This is Zisk's native interface. Use ZiskVerifierAdapter to bridge to IVerifier.
interface IZiskVerifier {
    /// @param programVK Verification key for the RISC-V program (4 x uint64)
    /// @param rootCVadcopFinal VADCOP final root commitment (4 x uint64)
    /// @param publicValues Encoded public values (68 values: 4 rom_root + 64 inputs)
    /// @param proofBytes The SNARK proof bytes
    function verifySnarkProof(
        uint64[4] calldata programVK,
        uint64[4] calldata rootCVadcopFinal,
        bytes calldata publicValues,
        bytes calldata proofBytes
    ) external view;
}
