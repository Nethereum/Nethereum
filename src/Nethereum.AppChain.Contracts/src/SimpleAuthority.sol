// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IAuthority} from "./IAuthority.sol";

/// @title SimpleAuthority — V1 per-chain governance for AppChain anchoring
/// @notice Single operator per chain with optional authorized provers. Owner can override.
/// Implements IAuthority so it can be referenced by AppChainAnchor and AppChainProofManager.
/// Upgrade to MultisigAuthority or ValidatorAuthority by deploying a new contract
/// and calling AppChainAnchor.setChainAuthority().
contract SimpleAuthority is IAuthority {

    address public owner;
    mapping(uint64 => address) public operators;
    mapping(uint64 => mapping(address => bool)) public authorizedProvers;

    event OperatorSet(uint64 indexed chainId, address indexed oldOperator, address indexed newOperator);
    event ProverAuthorized(uint64 indexed chainId, address indexed prover);
    event ProverRevoked(uint64 indexed chainId, address indexed prover);
    event OwnershipTransferred(address indexed oldOwner, address indexed newOwner);

    modifier onlyOwner() {
        require(msg.sender == owner, "Only owner");
        _;
    }

    /// @param _owner The initial owner address (typically the deployer)
    constructor(address _owner) {
        require(_owner != address(0), "Invalid owner");
        owner = _owner;
    }

    // ═══════════════════════════════════════════
    //  IAuthority implementation
    // ═══════════════════════════════════════════

    /// @inheritdoc IAuthority
    function canSubmitAnchor(uint64 chainId, address caller) external view returns (bool) {
        return caller == operators[chainId];
    }

    /// @inheritdoc IAuthority
    function canProve(uint64 chainId, address caller) external view returns (bool) {
        return caller == operators[chainId] || authorizedProvers[chainId][caller];
    }

    /// @inheritdoc IAuthority
    function canManageChain(uint64 chainId, address caller) external view returns (bool) {
        return caller == operators[chainId] || caller == owner;
    }

    // ═══════════════════════════════════════════
    //  Operator management
    // ═══════════════════════════════════════════

    /// @notice Set or transfer the operator for a chain
    /// @dev Callable by current operator (self-transfer) or owner (override)
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param newOperator The new operator address
    function setOperator(uint64 chainId, address newOperator) external {
        require(msg.sender == operators[chainId] || msg.sender == owner, "Not authorized");
        require(newOperator != address(0), "Invalid operator");
        address old = operators[chainId];
        operators[chainId] = newOperator;
        emit OperatorSet(chainId, old, newOperator);
    }

    // ═══════════════════════════════════════════
    //  Prover management
    // ═══════════════════════════════════════════

    /// @notice Authorize an address to submit per-block proofs for a chain
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param prover The address to authorize
    function authorizeProver(uint64 chainId, address prover) external {
        require(msg.sender == operators[chainId] || msg.sender == owner, "Not authorized");
        require(prover != address(0), "Invalid prover");
        authorizedProvers[chainId][prover] = true;
        emit ProverAuthorized(chainId, prover);
    }

    /// @notice Revoke an address's proving authorization for a chain
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param prover The address to revoke
    function revokeProver(uint64 chainId, address prover) external {
        require(msg.sender == operators[chainId] || msg.sender == owner, "Not authorized");
        authorizedProvers[chainId][prover] = false;
        emit ProverRevoked(chainId, prover);
    }

    // ═══════════════════════════════════════════
    //  Admin
    // ═══════════════════════════════════════════

    /// @notice Transfer ownership of this authority contract
    /// @param newOwner The new owner address
    function transferOwnership(address newOwner) external onlyOwner {
        require(newOwner != address(0), "Invalid owner");
        address old = owner;
        owner = newOwner;
        emit OwnershipTransferred(old, newOwner);
    }
}
