// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IAuthority} from "../IAuthority.sol";

/// @title CliqueAuthority — EXAMPLE: Round-robin validator rotation
/// @notice MOCK IMPLEMENTATION FOR REFERENCE ONLY — NOT AUDITED FOR PRODUCTION
/// @dev Demonstrates how a Clique-style rotating validator set integrates with IAuthority.
/// In production, add slashing, epoch management, and proper stake handling.
///
/// Usage:
///   1. Deploy with initial signers
///   2. Call hub.setAuthority(chainId, address(this))
///   3. Validators take turns submitting anchors based on block number
contract CliqueAuthority is IAuthority {

    address public owner;
    mapping(uint64 => address[]) public signers;
    mapping(uint64 => mapping(address => bool)) public isSigner;

    event SignerAdded(uint64 indexed chainId, address indexed signer);
    event SignerRemoved(uint64 indexed chainId, address indexed signer);

    constructor() { owner = msg.sender; }

    modifier onlyOwner() {
        require(msg.sender == owner, "Only owner");
        _;
    }

    function addSigner(uint64 chainId, address signer) external onlyOwner {
        require(!isSigner[chainId][signer], "Already a signer");
        signers[chainId].push(signer);
        isSigner[chainId][signer] = true;
        emit SignerAdded(chainId, signer);
    }

    function removeSigner(uint64 chainId, address signer) external onlyOwner {
        require(isSigner[chainId][signer], "Not a signer");
        isSigner[chainId][signer] = false;
        address[] storage s = signers[chainId];
        for (uint256 i = 0; i < s.length; i++) {
            if (s[i] == signer) {
                s[i] = s[s.length - 1];
                s.pop();
                break;
            }
        }
        emit SignerRemoved(chainId, signer);
    }

    function canSubmitAnchor(uint64 chainId, address caller) external view returns (bool) {
        address[] storage s = signers[chainId];
        if (s.length == 0) return false;
        uint256 turn = block.number % s.length;
        return s[turn] == caller;
    }

    function canProve(uint64 chainId, address caller) external view returns (bool) {
        return isSigner[chainId][caller];
    }

    function canManageChain(uint64, address caller) external view returns (bool) {
        return caller == owner;
    }

    function signerCount(uint64 chainId) external view returns (uint256) {
        return signers[chainId].length;
    }
}
