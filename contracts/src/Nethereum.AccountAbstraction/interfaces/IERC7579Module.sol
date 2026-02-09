// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {PackedUserOperation} from "./PackedUserOperation.sol";

/// @title IERC7579Module
/// @notice ERC-7579 Module Interfaces
/// @dev https://eips.ethereum.org/EIPS/eip-7579

/// @notice Base interface for all ERC-7579 modules
interface IModule {
    /// @notice Called when the module is installed on an account
    /// @param data Initialization data from the account
    function onInstall(bytes calldata data) external;

    /// @notice Called when the module is uninstalled from an account
    /// @param data De-initialization data from the account
    function onUninstall(bytes calldata data) external;

    /// @notice Checks if the module is of a specific type
    /// @param moduleTypeId The module type to check
    /// @return True if the module is of the specified type
    function isModuleType(uint256 moduleTypeId) external view returns (bool);

    /// @notice Checks if the module is initialized for a specific account
    /// @param smartAccount The account to check
    /// @return True if initialized
    function isInitialized(address smartAccount) external view returns (bool);
}

/// @notice Validator module interface (Type 1)
/// @dev Validators determine if a UserOperation or signature is valid
interface IValidator is IModule {
    /// @notice Validates a UserOperation during ERC-4337 validation phase
    /// @param userOp The packed user operation
    /// @param userOpHash The hash of the user operation
    /// @return validationData Packed validation data:
    ///         - Bit 0: 0 = valid, 1 = invalid
    ///         - Bits 1-48: validUntil (0 = infinite)
    ///         - Bits 49-96: validAfter
    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) external returns (uint256 validationData);

    /// @notice Validates an ERC-1271 signature with sender context
    /// @param sender The address that initiated the signature check
    /// @param hash The hash that was signed
    /// @param signature The signature to validate
    /// @return magicValue 0x1626ba7e if valid, 0xffffffff otherwise
    function isValidSignatureWithSender(
        address sender,
        bytes32 hash,
        bytes calldata signature
    ) external view returns (bytes4 magicValue);
}

/// @notice Executor module interface (Type 2)
/// @dev Executors can trigger executions on the account via executeFromExecutor
interface IExecutor is IModule {
    // Executors call account.executeFromExecutor() to perform actions
    // No additional interface required beyond IModule
}

/// @notice Fallback handler module interface (Type 3)
/// @dev Fallback handlers extend the account's supported function selectors
interface IFallback is IModule {
    // Fallback handlers receive calls via the account's fallback function
    // The account appends the original msg.sender (20 bytes) to calldata
    // No additional interface required beyond IModule
}

/// @notice Hook module interface (Type 4)
/// @dev Hooks execute before and after account executions
interface IHook is IModule {
    /// @notice Called before execution
    /// @param msgSender The original caller
    /// @param msgValue The ETH value sent
    /// @param msgData The calldata
    /// @return hookData Arbitrary data to pass to postCheck
    function preCheck(
        address msgSender,
        uint256 msgValue,
        bytes calldata msgData
    ) external returns (bytes memory hookData);

    /// @notice Called after execution
    /// @param hookData Data returned from preCheck
    function postCheck(bytes calldata hookData) external;
}
