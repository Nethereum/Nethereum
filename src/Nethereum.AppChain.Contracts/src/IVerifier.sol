// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/// @title IVerifier — ZK proof verification interface
/// @notice Implemented by proof system verifiers (MockProofVerifier, Groth16Verifier, etc.).
/// Registered in AppChainAnchor's proof system registry.
interface IVerifier {
    /// @notice Verify a ZK proof against public inputs
    /// @param proof The encoded proof bytes (e.g. 256 bytes for Groth16)
    /// @param publicInputs Array of public input values bound into the proof
    /// @return True if the proof is valid for the given public inputs
    function verify(bytes calldata proof, uint256[] calldata publicInputs) external view returns (bool);
}
