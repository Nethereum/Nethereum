// SPDX-License-Identifier: MIT
pragma solidity ^0.8.23;

import { RuleRegistry } from "../RuleRegistry.sol";
import "../../modules/smartsessions/DataTypes.sol";
import { IActionPolicy, IPolicy, VALIDATION_SUCCESS, VALIDATION_FAILED } from "../../modules/smartsessions/interfaces/IPolicy.sol";
import { IERC165 } from "forge-std/interfaces/IERC165.sol";

/// @title CombinatorBase
/// @notice Reusable plumbing for hand-crafted combinators that turn one or more RuleRegistry-registered
///         rules into a SmartSession `IActionPolicy`. A combinator is bound to exactly one registry
///         instance (the trusted rule set for its deployment).
/// @dev    Resolving a rule id through the registry (`_resolveRule`) is a plain SLOAD of
///         `registry.ruleOf[ruleId]` - a slot keyed by `ruleId`, NOT by the account address, so it is
///         NOT ERC-7562 sender-associated: an unstaked entity reading it during validation is rejected
///         by a 7562-enforcing bundler. Concretes MUST only call `_resolveRule` from
///         `initializeWithMultiplexer`, and cache the resolved rule address in their own per-account,
///         sender-associated config; `checkAction` (VALIDATION phase) then reads only that cached config
///         and never calls `_resolveRule` or touches the registry.
/// @dev    ERC-7562 SAFETY IS CONDITIONAL ON *WHEN* `initializeWithMultiplexer` RUNS. It is only safe
///         when the session/policy is enabled through an EXECUTION-phase path - a SmartSession module
///         `onInstall`, or a prior enabling transaction - so that the one-time registry read happens in
///         execution, not validation. It is NOT safe to enable a registry-backed combinator lazily via
///         `SmartSession.validateUserOp` ENABLE / UNSAFE_ENABLE mode: that routes
///         `initializeWithMultiplexer` (and its registry SLOAD) into the VALIDATION frame, which a
///         7562-enforcing bundler will reject for an unstaked sender. The AppChain enterprise flows
///         satisfy this by enabling sessions at `onInstall` (execution) and paying only in USE mode
///         (`mode || permissionId || blob`), whose validation reads solely the cached `checkAction`
///         config - proven unstaked-safe under a 7562-enabled bundler (see R1c-c). A caller that needs
///         validateUserOp-ENABLE-mode installs of a registry-backed combinator must stake the sender.
abstract contract CombinatorBase is IActionPolicy {
    error NotInitialized(ConfigId id, address mxer, address account);
    error UnknownRule(bytes32 ruleId);

    /// @notice The per-deployment trusted rule set this combinator resolves rule ids against. Immutable:
    ///         a different rule set ships as a new combinator deployment, never a mutation of this one.
    RuleRegistry public immutable registry;

    constructor(RuleRegistry _registry) {
        registry = _registry;
    }

    function supportsInterface(bytes4 interfaceID) external pure override returns (bool) {
        return (
            interfaceID == type(IERC165).interfaceId || interfaceID == type(IPolicy).interfaceId
                || interfaceID == type(IActionPolicy).interfaceId
        );
    }

    /// @dev Resolves `ruleId` to its registered rule address. Reverts `UnknownRule` if unregistered.
    ///      EXECUTION-phase only - see the contract-level dev note.
    function _resolveRule(bytes32 ruleId) internal view returns (address) {
        address r = registry.ruleOf(ruleId);
        if (r == address(0)) revert UnknownRule(ruleId);
        return r;
    }
}
