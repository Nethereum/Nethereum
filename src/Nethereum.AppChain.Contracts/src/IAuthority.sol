// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/// @title IAuthority — Per-chain governance interface for AppChain anchoring
/// @notice Each AppChain references an IAuthority that controls who can submit anchors,
/// prove blocks, and manage chain configuration. Swap the implementation to upgrade
/// governance (SimpleAuthority → MultisigAuthority → ValidatorAuthority).
interface IAuthority {
    /// @notice Can this caller submit anchors for this chain?
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param caller The address attempting to submit
    function canSubmitAnchor(uint64 chainId, address caller) external view returns (bool);

    /// @notice Can this caller submit per-block proofs for this chain?
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param caller The address attempting to prove
    function canProve(uint64 chainId, address caller) external view returns (bool);

    /// @notice Can this caller manage this chain's configuration (bonds, provers, authority upgrade)?
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param caller The address attempting to manage
    function canManageChain(uint64 chainId, address caller) external view returns (bool);
}
