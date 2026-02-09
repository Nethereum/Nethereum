// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {IValidator, IModule} from "../interfaces/IERC7579Module.sol";
import {PackedUserOperation} from "../interfaces/PackedUserOperation.sol";
import {MODULE_TYPE_VALIDATOR, ERC1271_VALID, ERC1271_INVALID} from "../interfaces/IERC7579Account.sol";
import {ECDSA} from "@openzeppelin/contracts/utils/cryptography/ECDSA.sol";
import {MessageHashUtils} from "@openzeppelin/contracts/utils/cryptography/MessageHashUtils.sol";

/// @title ECDSAValidator
/// @notice ERC-7579 validator module using ECDSA signatures
/// @dev Stores one owner per smart account, validates signatures against that owner
contract ECDSAValidator is IValidator {
    using ECDSA for bytes32;
    using MessageHashUtils for bytes32;

    // ============ Storage ============

    /// @notice Owner address per smart account
    mapping(address smartAccount => address owner) public owners;

    // ============ Events ============

    event OwnerSet(address indexed smartAccount, address indexed owner);

    // ============ Errors ============

    error InvalidOwner();
    error NotInitialized();
    error AlreadyInitialized();

    // ============ IModule Implementation ============

    /// @inheritdoc IModule
    function onInstall(bytes calldata data) external override {
        if (owners[msg.sender] != address(0)) revert AlreadyInitialized();

        address owner = address(bytes20(data[:20]));
        if (owner == address(0)) revert InvalidOwner();

        owners[msg.sender] = owner;
        emit OwnerSet(msg.sender, owner);
    }

    /// @inheritdoc IModule
    function onUninstall(bytes calldata) external override {
        delete owners[msg.sender];
    }

    /// @inheritdoc IModule
    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == MODULE_TYPE_VALIDATOR;
    }

    /// @inheritdoc IModule
    function isInitialized(address smartAccount) external view override returns (bool) {
        return owners[smartAccount] != address(0);
    }

    // ============ IValidator Implementation ============

    /// @inheritdoc IValidator
    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) external view override returns (uint256 validationData) {
        address owner = owners[userOp.sender];
        if (owner == address(0)) return 1; // SIG_VALIDATION_FAILED

        // Recover signer from signature
        // Note: userOpHash is already EIP-712 encoded by EntryPoint
        address signer = userOpHash.recover(userOp.signature);

        if (signer == owner) {
            return 0; // SIG_VALIDATION_SUCCESS
        }

        return 1; // SIG_VALIDATION_FAILED
    }

    /// @inheritdoc IValidator
    function isValidSignatureWithSender(
        address,
        bytes32 hash,
        bytes calldata signature
    ) external view override returns (bytes4) {
        address owner = owners[msg.sender];
        if (owner == address(0)) return ERC1271_INVALID;

        // For ERC-1271, use eth_sign message hash format
        bytes32 ethSignedHash = hash.toEthSignedMessageHash();
        address signer = ethSignedHash.recover(signature);

        if (signer == owner) {
            return ERC1271_VALID;
        }

        return ERC1271_INVALID;
    }

    // ============ Owner Management ============

    /// @notice Gets the owner for a smart account
    function getOwner(address smartAccount) external view returns (address) {
        return owners[smartAccount];
    }

    /// @notice Transfers ownership to a new address
    /// @dev Can only be called by the smart account itself
    function transferOwnership(address newOwner) external {
        if (newOwner == address(0)) revert InvalidOwner();
        if (owners[msg.sender] == address(0)) revert NotInitialized();

        owners[msg.sender] = newOwner;
        emit OwnerSet(msg.sender, newOwner);
    }
}
