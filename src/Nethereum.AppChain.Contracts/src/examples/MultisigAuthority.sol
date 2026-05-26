// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IAuthority} from "../IAuthority.sol";

/// @title MultisigAuthority — EXAMPLE: N-of-M threshold authority
/// @notice MOCK IMPLEMENTATION FOR REFERENCE ONLY — NOT AUDITED FOR PRODUCTION
/// @dev Demonstrates how a multisig threshold integrates with IAuthority.
/// In production, use a battle-tested multisig (Gnosis Safe) or add proper
/// nonce tracking, expiration, and EIP-712 typed signatures.
///
/// Usage:
///   1. Deploy with signers and threshold
///   2. Call hub.setAuthority(chainId, address(this))
///   3. Collect threshold signatures off-chain, then any signer submits the anchor
contract MultisigAuthority is IAuthority {

    uint256 public threshold;
    mapping(address => bool) public isSigner;
    address[] public signers;

    mapping(bytes32 => uint256) public approvals;
    mapping(bytes32 => mapping(address => bool)) public hasApproved;

    event Approved(bytes32 indexed operationHash, address indexed signer, uint256 count);
    event Executed(bytes32 indexed operationHash);

    constructor(address[] memory _signers, uint256 _threshold) {
        require(_threshold > 0 && _threshold <= _signers.length, "Invalid threshold");
        for (uint256 i = 0; i < _signers.length; i++) {
            require(_signers[i] != address(0), "Invalid signer");
            require(!isSigner[_signers[i]], "Duplicate signer");
            isSigner[_signers[i]] = true;
        }
        signers = _signers;
        threshold = _threshold;
    }

    function approve(bytes32 operationHash) external {
        require(isSigner[msg.sender], "Not a signer");
        require(!hasApproved[operationHash][msg.sender], "Already approved");
        hasApproved[operationHash][msg.sender] = true;
        approvals[operationHash]++;
        emit Approved(operationHash, msg.sender, approvals[operationHash]);
    }

    function isApproved(bytes32 operationHash) public view returns (bool) {
        return approvals[operationHash] >= threshold;
    }

    function canSubmitAnchor(uint64 chainId, address caller) external view returns (bool) {
        if (!isSigner[caller]) return false;
        bytes32 opHash = keccak256(abi.encodePacked("anchor", chainId, caller, block.number / 100));
        return isApproved(opHash);
    }

    function canProve(uint64 chainId, address caller) external view returns (bool) {
        if (!isSigner[caller]) return false;
        bytes32 opHash = keccak256(abi.encodePacked("prove", chainId, caller));
        return isApproved(opHash);
    }

    function canManageChain(uint64 chainId, address caller) external view returns (bool) {
        if (!isSigner[caller]) return false;
        bytes32 opHash = keccak256(abi.encodePacked("manage", chainId, caller));
        return isApproved(opHash);
    }

    function signerCount() external view returns (uint256) {
        return signers.length;
    }
}
