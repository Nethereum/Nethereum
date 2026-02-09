// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {AccountStorage, AccountStorageLib, FallbackHandler, FallbackCallType, ACCOUNT_STORAGE_SLOT} from "./AccountStorage.sol";
import {SentinelListLib, SENTINEL, ZERO_ADDRESS} from "../lib/SentinelList.sol";
import {IModule, IValidator, IExecutor, IFallback, IHook} from "../interfaces/IERC7579Module.sol";
import {
    MODULE_TYPE_VALIDATOR,
    MODULE_TYPE_EXECUTOR,
    MODULE_TYPE_FALLBACK,
    MODULE_TYPE_HOOK,
    IERC7579ModuleConfig
} from "../interfaces/IERC7579Account.sol";

// =============================================================================
// SECURITY VALIDATION: Module Manager
// =============================================================================
// Pattern Sources:
//   - Nexus (Biconomy): ModuleManager.sol
//   - Safe7579 (Rhinestone): ModuleManager.sol
//   - Kernel v3 (ZeroDev): ValidationManager.sol
//
// SECURITY REQUIREMENTS (from ERC-7579):
//   1. MUST call onInstall() when installing a module
//   2. MUST call onUninstall() when removing a module
//   3. MUST emit ModuleInstalled/ModuleUninstalled events
//   4. MUST differentiate between module types in storage
//   5. MUST sanitize validator selection (verify installed before use)
//
// AUDIT FINDINGS ADDRESSED:
//   - H-01 (Nexus): Missing nonce in enable mode - N/A here (no enable mode)
//   - H-02 (Nexus): Module type not in signature - enforced via separate storage
//   - H-03 (Nexus): Registry before install - registry support ready
//   - Safe7579: Hook emergency uninstall - implemented with timelock
// =============================================================================

/// @title ModuleManager
/// @notice Manages installation/uninstallation of ERC-7579 modules
/// @dev Abstract base contract - NethereumAccount inherits this
abstract contract ModuleManager is IERC7579ModuleConfig {
    using SentinelListLib for SentinelListLib.SentinelList;

    // =========================================================================
    // ERRORS
    // =========================================================================

    error ModuleAlreadyInstalled(address module);
    error ModuleNotInstalled(address module);
    error InvalidModuleType(uint256 moduleTypeId);
    error InvalidModule(address module);
    error CannotRemoveLastValidator();
    error FallbackSelectorAlreadyInstalled(bytes4 selector);
    error FallbackSelectorNotInstalled(bytes4 selector);
    error ForbiddenSelector(bytes4 selector);
    error HookAlreadyInstalled(address hook);
    error EmergencyTimelockActive(uint256 unlockTime);
    error EmergencyTimelockNotReady(uint256 unlockTime);

    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// @notice Emergency uninstall timelock duration
    /// @dev Pattern from Safe7579 - prevents malicious hook lockout
    /// @dev SECURITY: 1 day gives user time to react to malicious hook
    uint256 public constant EMERGENCY_UNINSTALL_DELAY = 1 days;

    /// @notice Forbidden selectors that cannot be used for fallback handlers
    /// @dev SECURITY (Nexus audit): Prevents module lifecycle hijacking
    bytes4 private constant SELECTOR_ON_INSTALL = 0x6d61fe70;     // onInstall(bytes)
    bytes4 private constant SELECTOR_ON_UNINSTALL = 0x8a91b0e3;   // onUninstall(bytes)

    // =========================================================================
    // INTERNAL STORAGE ACCESS
    // =========================================================================

    /// @notice Gets the account storage
    /// @dev SECURITY: Single point of storage access
    function _getAccountStorage() internal pure virtual returns (AccountStorage storage $) {
        return AccountStorageLib.getStorage();
    }

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    /// @notice Initializes the module storage
    /// @dev MUST be called once during account initialization
    /// @dev SECURITY: SentinelList requires init before use
    function _initModuleManager() internal {
        AccountStorage storage $ = _getAccountStorage();
        $.validators.init();
        $.executors.init();
    }

    // =========================================================================
    // INSTALL MODULE - INTERNAL
    // =========================================================================

    /// @notice Internal module installation router
    /// @dev Routes to type-specific installation function
    /// @param moduleTypeId The module type (1-4)
    /// @param module The module address
    /// @param initData Initialization data passed to module.onInstall()
    function _installModule(
        uint256 moduleTypeId,
        address module,
        bytes calldata initData
    ) internal {
        // SECURITY: Validate module address
        if (module == address(0) || module == SENTINEL) {
            revert InvalidModule(module);
        }

        // Route to type-specific installer
        if (moduleTypeId == MODULE_TYPE_VALIDATOR) {
            _installValidator(module, initData);
        } else if (moduleTypeId == MODULE_TYPE_EXECUTOR) {
            _installExecutor(module, initData);
        } else if (moduleTypeId == MODULE_TYPE_FALLBACK) {
            _installFallback(module, initData);
        } else if (moduleTypeId == MODULE_TYPE_HOOK) {
            _installHook(module, initData);
        } else {
            revert InvalidModuleType(moduleTypeId);
        }

        // SPEC REQUIREMENT: MUST emit event
        emit ModuleInstalled(moduleTypeId, module);
    }

    // =========================================================================
    // UNINSTALL MODULE - INTERNAL
    // =========================================================================

    /// @notice Internal module uninstallation router
    /// @param moduleTypeId The module type (1-4)
    /// @param module The module address
    /// @param deInitData De-initialization data passed to module.onUninstall()
    function _uninstallModule(
        uint256 moduleTypeId,
        address module,
        bytes calldata deInitData
    ) internal {
        // Route to type-specific uninstaller
        if (moduleTypeId == MODULE_TYPE_VALIDATOR) {
            _uninstallValidator(module, deInitData);
        } else if (moduleTypeId == MODULE_TYPE_EXECUTOR) {
            _uninstallExecutor(module, deInitData);
        } else if (moduleTypeId == MODULE_TYPE_FALLBACK) {
            _uninstallFallback(module, deInitData);
        } else if (moduleTypeId == MODULE_TYPE_HOOK) {
            _uninstallHook(module, deInitData);
        } else {
            revert InvalidModuleType(moduleTypeId);
        }

        // SPEC REQUIREMENT: MUST emit event
        emit ModuleUninstalled(moduleTypeId, module);
    }

    // =========================================================================
    // VALIDATOR MANAGEMENT (Type 1)
    // =========================================================================
    // SECURITY: Validators determine signature validity
    // - MUST verify validator is installed before delegating validation
    // - MUST have at least one validator (prevents lockout)
    // - Storage: SentinelList for O(1) membership check
    // =========================================================================

    function _installValidator(address validator, bytes calldata initData) internal {
        AccountStorage storage $ = _getAccountStorage();

        // SECURITY: Check not already installed (SentinelList handles this)
        // SentinelList.push reverts if entry exists
        $.validators.push(validator);

        // SPEC REQUIREMENT: MUST call onInstall
        IValidator(validator).onInstall(initData);
    }

    function _uninstallValidator(address prevValidator, address validator, bytes calldata deInitData) internal {
        AccountStorage storage $ = _getAccountStorage();

        // SECURITY: Cannot remove last validator (prevents lockout)
        // Check if there's more than one validator
        address first = $.validators.entries[SENTINEL];
        address second = $.validators.entries[first];
        if (second == SENTINEL) {
            revert CannotRemoveLastValidator();
        }

        // Remove from SentinelList
        $.validators.pop(prevValidator, validator);

        // SPEC REQUIREMENT: MUST call onUninstall
        IValidator(validator).onUninstall(deInitData);
    }

    /// @notice Overload for uninstallValidator without prevValidator
    /// @dev Finds prevValidator automatically (less gas efficient)
    function _uninstallValidator(address validator, bytes calldata deInitData) internal {
        AccountStorage storage $ = _getAccountStorage();

        // Find previous entry in linked list
        address prev = _findPrevEntry($.validators, validator);
        _uninstallValidator(prev, validator, deInitData);
    }

    function _isValidatorInstalled(address validator) internal view returns (bool) {
        return _getAccountStorage().validators.contains(validator);
    }

    // =========================================================================
    // EXECUTOR MANAGEMENT (Type 2)
    // =========================================================================
    // SECURITY: Executors can trigger executions via executeFromExecutor
    // - MUST verify executor is installed before allowing execution
    // - Pattern: Same as validators (SentinelList)
    // =========================================================================

    function _installExecutor(address executor, bytes calldata initData) internal {
        AccountStorage storage $ = _getAccountStorage();

        // Add to SentinelList (reverts if exists)
        $.executors.push(executor);

        // SPEC REQUIREMENT: MUST call onInstall
        IExecutor(executor).onInstall(initData);
    }

    function _uninstallExecutor(address prevExecutor, address executor, bytes calldata deInitData) internal {
        AccountStorage storage $ = _getAccountStorage();

        // Remove from SentinelList
        $.executors.pop(prevExecutor, executor);

        // SPEC REQUIREMENT: MUST call onUninstall
        IExecutor(executor).onUninstall(deInitData);
    }

    function _uninstallExecutor(address executor, bytes calldata deInitData) internal {
        AccountStorage storage $ = _getAccountStorage();
        address prev = _findPrevEntry($.executors, executor);
        _uninstallExecutor(prev, executor, deInitData);
    }

    function _isExecutorInstalled(address executor) internal view returns (bool) {
        return _getAccountStorage().executors.contains(executor);
    }

    // =========================================================================
    // FALLBACK MANAGEMENT (Type 3)
    // =========================================================================
    // SECURITY: Fallback handlers extend account functionality
    // - Each selector maps to ONE handler (no collisions)
    // - FORBIDDEN selectors: onInstall, onUninstall (prevents hijacking)
    // - InitData format: selector (4 bytes) + callType (1 byte) + handlerInitData
    // =========================================================================

    function _installFallback(address handler, bytes calldata initData) internal {
        // SECURITY: InitData MUST contain selector and callType
        if (initData.length < 5) {
            revert InvalidModule(handler);
        }

        bytes4 selector = bytes4(initData[:4]);
        FallbackCallType calltype = FallbackCallType(uint8(initData[4]));
        bytes calldata handlerInitData = initData[5:];

        // SECURITY (Nexus audit): Forbid module lifecycle selectors
        if (selector == SELECTOR_ON_INSTALL || selector == SELECTOR_ON_UNINSTALL) {
            revert ForbiddenSelector(selector);
        }

        AccountStorage storage $ = _getAccountStorage();

        // SECURITY: One handler per selector
        if ($.fallbacks[selector].handler != address(0)) {
            revert FallbackSelectorAlreadyInstalled(selector);
        }

        // Store handler configuration
        $.fallbacks[selector] = FallbackHandler({
            handler: handler,
            calltype: calltype
        });

        // SPEC REQUIREMENT: MUST call onInstall
        IFallback(handler).onInstall(handlerInitData);
    }

    function _uninstallFallback(address handler, bytes calldata deInitData) internal {
        // DeInitData MUST contain selector
        if (deInitData.length < 4) {
            revert InvalidModule(handler);
        }

        bytes4 selector = bytes4(deInitData[:4]);
        bytes calldata handlerDeInitData = deInitData[4:];

        AccountStorage storage $ = _getAccountStorage();

        // Verify handler matches
        if ($.fallbacks[selector].handler != handler) {
            revert FallbackSelectorNotInstalled(selector);
        }

        // Remove handler
        delete $.fallbacks[selector];

        // SPEC REQUIREMENT: MUST call onUninstall
        IFallback(handler).onUninstall(handlerDeInitData);
    }

    function _isFallbackInstalled(bytes4 selector, address handler) internal view returns (bool) {
        return _getAccountStorage().fallbacks[selector].handler == handler;
    }

    function _getFallbackHandler(bytes4 selector) internal view returns (FallbackHandler storage) {
        return _getAccountStorage().fallbacks[selector];
    }

    // =========================================================================
    // HOOK MANAGEMENT (Type 4)
    // =========================================================================
    // SECURITY: Single global hook for pre/post execution checks
    // - Only ONE hook can be active (prevents composition complexity)
    // - Emergency uninstall with timelock (prevents malicious hook lockout)
    // - Pattern: From Safe7579/Nexus
    // =========================================================================

    function _installHook(address hook, bytes calldata initData) internal {
        AccountStorage storage $ = _getAccountStorage();

        // SECURITY: Only one hook at a time
        if ($.hook != address(0)) {
            revert HookAlreadyInstalled($.hook);
        }

        $.hook = hook;

        // SPEC REQUIREMENT: MUST call onInstall
        IHook(hook).onInstall(initData);
    }

    function _uninstallHook(address hook, bytes calldata deInitData) internal {
        AccountStorage storage $ = _getAccountStorage();

        if ($.hook != hook) {
            revert ModuleNotInstalled(hook);
        }

        $.hook = address(0);

        // SPEC REQUIREMENT: MUST call onUninstall
        // Note: If hook reverts here, use emergencyUninstallHook
        IHook(hook).onUninstall(deInitData);
    }

    /// @notice Initiates emergency hook uninstallation
    /// @dev SECURITY (Safe7579 audit): Prevents malicious hook lockout
    /// @dev If hook's onUninstall reverts, user would be locked out
    /// @dev Solution: Time-locked bypass that doesn't call onUninstall
    function _initiateEmergencyHookUninstall(address hook) internal {
        AccountStorage storage $ = _getAccountStorage();

        if ($.hook != hook) {
            revert ModuleNotInstalled(hook);
        }

        // Check not already initiated
        if ($.emergencyUninstall[hook] != 0) {
            revert EmergencyTimelockActive($.emergencyUninstall[hook]);
        }

        // Set timelock
        $.emergencyUninstall[hook] = block.timestamp + EMERGENCY_UNINSTALL_DELAY;
    }

    /// @notice Executes emergency hook uninstallation after timelock
    /// @dev SECURITY: Does NOT call onUninstall (hook might be malicious)
    function _executeEmergencyHookUninstall(address hook) internal {
        AccountStorage storage $ = _getAccountStorage();

        uint256 unlockTime = $.emergencyUninstall[hook];
        if (unlockTime == 0 || block.timestamp < unlockTime) {
            revert EmergencyTimelockNotReady(unlockTime);
        }

        // Clear hook without calling onUninstall
        $.hook = address(0);
        delete $.emergencyUninstall[hook];

        emit ModuleUninstalled(MODULE_TYPE_HOOK, hook);
    }

    function _isHookInstalled(address hook) internal view returns (bool) {
        return _getAccountStorage().hook == hook;
    }

    function _getHook() internal view returns (address) {
        return _getAccountStorage().hook;
    }

    // =========================================================================
    // HELPER FUNCTIONS
    // =========================================================================

    /// @notice Finds the previous entry in a SentinelList
    /// @dev O(n) operation - use sparingly
    function _findPrevEntry(
        SentinelListLib.SentinelList storage list,
        address entry
    ) internal view returns (address prev) {
        prev = SENTINEL;
        address current = list.entries[SENTINEL];

        while (current != SENTINEL && current != ZERO_ADDRESS) {
            if (current == entry) {
                return prev;
            }
            prev = current;
            current = list.entries[current];
        }

        revert ModuleNotInstalled(entry);
    }

    // =========================================================================
    // PUBLIC VIEW FUNCTIONS
    // =========================================================================

    function _isModuleInstalled(
        uint256 moduleTypeId,
        address module,
        bytes calldata additionalContext
    ) internal view returns (bool) {
        if (moduleTypeId == MODULE_TYPE_VALIDATOR) {
            return _isValidatorInstalled(module);
        } else if (moduleTypeId == MODULE_TYPE_EXECUTOR) {
            return _isExecutorInstalled(module);
        } else if (moduleTypeId == MODULE_TYPE_FALLBACK) {
            // additionalContext = selector for fallback
            if (additionalContext.length >= 4) {
                bytes4 selector = bytes4(additionalContext[:4]);
                return _isFallbackInstalled(selector, module);
            }
            return false;
        } else if (moduleTypeId == MODULE_TYPE_HOOK) {
            return _isHookInstalled(module);
        }
        return false;
    }

    /// @notice Gets validators with pagination
    /// @dev Pattern from Safe7579/Nexus - prevents OOG on large lists
    function getValidatorsPaginated(
        address start,
        uint256 pageSize
    ) external view returns (address[] memory validators, address nextCursor) {
        return _getAccountStorage().validators.getEntriesPaginated(start, pageSize);
    }

    /// @notice Gets executors with pagination
    function getExecutorsPaginated(
        address start,
        uint256 pageSize
    ) external view returns (address[] memory executors, address nextCursor) {
        return _getAccountStorage().executors.getEntriesPaginated(start, pageSize);
    }
}
