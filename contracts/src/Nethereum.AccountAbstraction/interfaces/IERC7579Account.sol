// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {ModeCode} from "../lib/ModeLib.sol";

// ERC-7579 Minimal Modular Smart Account Interface
// https://eips.ethereum.org/EIPS/eip-7579

// Module type constants
uint256 constant MODULE_TYPE_VALIDATOR = 1;
uint256 constant MODULE_TYPE_EXECUTOR = 2;
uint256 constant MODULE_TYPE_FALLBACK = 3;
uint256 constant MODULE_TYPE_HOOK = 4;

// ERC-1271 magic values
bytes4 constant ERC1271_VALID = 0x1626ba7e;
bytes4 constant ERC1271_INVALID = 0xffffffff;

/// @title IERC7579Execution
/// @notice Execution interface for ERC-7579 accounts
interface IERC7579Execution {
    /// @notice Executes a transaction from EntryPoint or self
    /// @param mode The encoded execution mode (CallType + ExecType + selector + payload)
    /// @param executionCalldata The encoded execution data
    function execute(ModeCode mode, bytes calldata executionCalldata) external payable;

    /// @notice Executes a transaction from an installed Executor module
    /// @param mode The encoded execution mode
    /// @param executionCalldata The encoded execution data
    /// @return returnData Array of return data from each execution
    function executeFromExecutor(
        ModeCode mode,
        bytes calldata executionCalldata
    ) external payable returns (bytes[] memory returnData);
}

/// @title IERC7579ModuleConfig
/// @notice Module configuration interface
interface IERC7579ModuleConfig {
    /// @notice Emitted when a module is installed
    event ModuleInstalled(uint256 moduleTypeId, address module);

    /// @notice Emitted when a module is uninstalled
    event ModuleUninstalled(uint256 moduleTypeId, address module);

    /// @notice Installs a module on the account
    /// @param moduleTypeId The type of module (1=Validator, 2=Executor, 3=Fallback, 4=Hook)
    /// @param module The module address
    /// @param initData Data for module initialization
    function installModule(
        uint256 moduleTypeId,
        address module,
        bytes calldata initData
    ) external payable;

    /// @notice Uninstalls a module from the account
    /// @param moduleTypeId The type of module
    /// @param module The module address
    /// @param deInitData Data for module de-initialization
    function uninstallModule(
        uint256 moduleTypeId,
        address module,
        bytes calldata deInitData
    ) external payable;

    /// @notice Checks if a module is installed
    /// @param moduleTypeId The type of module
    /// @param module The module address
    /// @param additionalContext Additional context for the check
    /// @return True if installed
    function isModuleInstalled(
        uint256 moduleTypeId,
        address module,
        bytes calldata additionalContext
    ) external view returns (bool);
}

/// @title IERC7579AccountConfig
/// @notice Account configuration interface
interface IERC7579AccountConfig {
    /// @notice Returns the account implementation identifier
    /// @return The account ID in format "vendorname.accountname.semver"
    function accountId() external view returns (string memory);

    /// @notice Checks if the account supports a specific execution mode
    /// @param encodedMode The encoded execution mode
    /// @return True if supported
    function supportsExecutionMode(ModeCode encodedMode) external view returns (bool);

    /// @notice Checks if the account supports a specific module type
    /// @param moduleTypeId The module type ID
    /// @return True if supported
    function supportsModule(uint256 moduleTypeId) external view returns (bool);
}

/// @title IERC7579Account
/// @notice Combined ERC-7579 account interface
interface IERC7579Account is IERC7579Execution, IERC7579ModuleConfig, IERC7579AccountConfig {
    /// @notice ERC-1271 signature validation
    /// @param hash The hash to validate
    /// @param signature The signature data (validator address + signature)
    /// @return magicValue ERC1271_VALID if valid, ERC1271_INVALID otherwise
    function isValidSignature(
        bytes32 hash,
        bytes calldata signature
    ) external view returns (bytes4 magicValue);
}
