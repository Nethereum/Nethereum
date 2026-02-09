// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {SentinelListLib} from "../lib/SentinelList.sol";
import {IHook} from "../interfaces/IERC7579Module.sol";

// =============================================================================
// SECURITY VALIDATION: ERC-7201 Namespaced Storage
// =============================================================================
// Pattern Source: Nexus (Biconomy), Safe7579 (Rhinestone)
// Spec: https://eips.ethereum.org/EIPS/eip-7201
//
// WHY ERC-7201:
// - Prevents storage collision in UUPS upgradeable proxies
// - Formula: keccak256(keccak256("namespace") - 1) & ~bytes32(uint256(0xff))
// - The -1 and masking ensures slot doesn't overlap with standard storage
//
// SECURITY:
// - Single storage root prevents cross-contract storage contamination
// - All module data isolated in this namespace
// - Safe for proxy upgrades - new fields MUST be appended, never reordered
// =============================================================================

// Storage slot computed per ERC-7201
// keccak256(abi.encode(uint256(keccak256("nethereum.account.storage.v1")) - 1)) & ~bytes32(uint256(0xff))
bytes32 constant ACCOUNT_STORAGE_SLOT = 0x5d0f1b9c8f0d6d9b8a7c6e5f4d3c2b1a9e8f7d6c5b4a39281706554433221100;

/// @notice Call type for fallback handlers
/// @dev Matches ERC-7579 spec for fallback call types
/// @dev Named FallbackCallType to avoid conflict with ModeLib.CallType
enum FallbackCallType {
    CALL,       // 0x00 - Regular call
    STATICCALL, // 0x01 - Read-only call
    DELEGATECALL // 0x02 - Delegatecall (DANGER: shares storage)
}

/// @notice Fallback handler configuration
/// @dev Stores the handler address and call type for a function selector
struct FallbackHandler {
    address handler;         // The fallback module address
    FallbackCallType calltype;  // How to call the handler (CALL, STATICCALL, DELEGATECALL)
}

/// @notice Main account storage structure
/// @dev All fields use ERC-7201 namespaced slot
struct AccountStorage {
    // ==========================================================================
    // VALIDATORS (Module Type 1)
    // ==========================================================================
    // Pattern: SentinelList from Rhinestone/Safe7579
    // Security: O(1) membership check, O(n) iteration
    // Rationale: Validators determine signature validity - must verify installed
    SentinelListLib.SentinelList validators;

    // ==========================================================================
    // EXECUTORS (Module Type 2)
    // ==========================================================================
    // Pattern: SentinelList from Rhinestone/Safe7579
    // Security: O(1) membership check via linked list
    // Rationale: Only installed executors can call executeFromExecutor
    SentinelListLib.SentinelList executors;

    // ==========================================================================
    // HOOK (Module Type 4)
    // ==========================================================================
    // Pattern: Single global hook from Nexus/Safe7579
    // Security: One hook per account prevents hook composition attacks
    // Rationale: Multiple hooks add complexity and reentrancy risks
    // Note: Kernel uses per-validator hooks (more complex)
    address hook;

    // ==========================================================================
    // FALLBACK HANDLERS (Module Type 3)
    // ==========================================================================
    // Pattern: Selector-to-handler mapping from Safe7579/Nexus
    // Security: Each selector maps to exactly one handler
    // Rationale: Extends account with custom functions (e.g., ERC-721 receiver)
    // IMPORTANT: Some selectors are FORBIDDEN (onInstall, onUninstall)
    mapping(bytes4 selector => FallbackHandler) fallbacks;

    // ==========================================================================
    // EMERGENCY UNINSTALL
    // ==========================================================================
    // Pattern: From Safe7579 audit findings
    // Security: Prevents malicious hook from blocking uninstallation
    // Rationale: If hook reverts on uninstall, user is locked out
    // Solution: Time-locked emergency removal (1 day default)
    mapping(address hook => uint256 emergencyUninstallTime) emergencyUninstall;
}

/// @title AccountStorageLib
/// @notice Library for accessing ERC-7201 namespaced storage
/// @dev SECURITY: All storage access MUST go through this library
library AccountStorageLib {
    /// @notice Returns the account storage pointer
    /// @dev Uses assembly to load from the ERC-7201 slot
    /// @return $ The storage pointer
    function getStorage() internal pure returns (AccountStorage storage $) {
        assembly {
            $.slot := ACCOUNT_STORAGE_SLOT
        }
    }

    /// @notice Computes the ERC-7201 storage slot
    /// @dev This function documents how ACCOUNT_STORAGE_SLOT was computed
    /// @param namespace The storage namespace string
    /// @return The computed storage slot
    function computeSlot(string memory namespace) internal pure returns (bytes32) {
        return keccak256(abi.encode(uint256(keccak256(bytes(namespace))) - 1)) & ~bytes32(uint256(0xff));
    }
}
