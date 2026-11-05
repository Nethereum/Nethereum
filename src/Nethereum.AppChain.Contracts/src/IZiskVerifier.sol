// SPDX-License-Identifier: AGPL-3.0
pragma solidity ^0.8.20;

/// @title Zisk Verifier Interface
/// @author SilentSig
/// @notice This contract is the interface for the Zisk Verifier.
/// @dev `verifySnarkProof` is the only ZisK-binding entry point. The deployed
/// `ZiskVerifier` also carries `verifyProof`, the inherited raw PLONK verifier -- public
/// because its assembly reads arguments at fixed calldata offsets -- which proves only
/// that some proof satisfies some digest, with no ZisK statement attached. Note the names
/// invert the usual convention: the raw one is the shorter.
interface IZiskVerifier {
    /// @notice Verifies a proof with given public values and vkey.
    /// @dev Both keys MUST be constants of the calling contract, never values taken
    /// from the submitted proof, or verification is self-keyed and authenticates
    /// nothing. For an aggregated proof both are the recurser's verkey, and pinning
    /// them together is what extends that recurser's leaf allow-list over the whole
    /// fold tree.
    /// @param programVK The identity committed by the proof: the ROM root of the
    /// RISC-V program for a plain proof, the recursion domain for an aggregated one.
    /// @param rootCVadcopFinal The recursion root the proof was wrapped under:
    /// `ZiskVerifier.getRootCVadcopFinal()` for a leaf, the recurser's own verkey for
    /// an aggregate. It is not fixed by the verifier because those differ.
    /// @param publicValues The public values encoded as bytes.
    /// @param proofBytes The proof of the program execution the Zisk zkVM encoded as bytes.
    function verifySnarkProof(
        bytes32 programVK,
        bytes32 rootCVadcopFinal,
        bytes calldata publicValues,
        bytes calldata proofBytes
    ) external view;
}
