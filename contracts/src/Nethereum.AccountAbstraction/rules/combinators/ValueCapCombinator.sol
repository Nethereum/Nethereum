// SPDX-License-Identifier: MIT
pragma solidity ^0.8.23;

import { CombinatorBase } from "./CombinatorBase.sol";
import { RuleRegistry } from "../RuleRegistry.sol";
import { IRule } from "../IRule.sol";
import "../../modules/smartsessions/DataTypes.sol";
import { IPolicy, VALIDATION_SUCCESS, VALIDATION_FAILED } from "../../modules/smartsessions/interfaces/IPolicy.sol";

/// @title ValueCapCombinator
/// @notice Wires ONE registry-registered cap rule (e.g. `ValueCapRule`) into a SmartSession
///         `IActionPolicy`. The action's `value` is the global validation context; the resolved rule
///         address and the cap are per-account configuration, evaluated directly (no registry dispatch)
///         during validation.
/// @dev    `initializeWithMultiplexer` decodes `abi.encode(bytes32 capRuleId, uint256 cap)` - same
///         initData layout as before - resolves `capRuleId` through the registry ONCE (which reads the
///         registry, so it must run in EXECUTION phase - enable via `onInstall` / a prior tx, never via
///         validateUserOp ENABLE mode for an unstaked sender; see CombinatorBase's conditional-safety
///         dev note) and caches the resolved rule address alongside the cap in a fixed-layout
///         `CapConfig` struct, stored directly (no dynamic `bytes` indirection) at
///         `_config[id][msg.sender][account]`. Storing a struct - rather than an abi-encoded `bytes`
///         blob - directly at that mapping slot keeps every field within the ERC-7562
///         associated-storage window of the (configId, msg.sender, account)-keyed slot, so `checkAction`
///         can read it during validation without staking anything: it never touches the registry.
/// @dev    SECURITY: `cap` is a PER-ACTION ceiling, not a cumulative spending budget. `checkAction` checks
///         only the single action `value` passed to it and keeps no running total, so a userOp that
///         batches N executions each individually within the cap can move up to N x cap in one operation,
///         and the same is true again on the next userOp. For a cumulative/session spending limit, use a
///         spending-limit-style rule that tracks a persistent `used` amount per account (see
///         `ERC20SpendingLimitPolicy`), not this combinator.
contract ValueCapCombinator is CombinatorBase {
    error InvalidRuleOutput();

    struct CapConfig {
        address rule;
        uint256 cap;
    }

    mapping(ConfigId id => mapping(address msgSender => mapping(address account => CapConfig))) internal _config;

    constructor(RuleRegistry _registry) CombinatorBase(_registry) { }

    /// @notice Resolves `capRuleId` through the registry and stores the resolved rule address + cap
    ///         under `(configId, msg.sender, account)`. Overwrites any prior configuration for that key,
    ///         matching `UniActionPolicy`'s overwrite-on-reinstall behaviour. Reverts `UnknownRule` if
    ///         `capRuleId` is not registered.
    function initializeWithMultiplexer(address account, ConfigId configId, bytes calldata initData) external {
        (bytes32 capRuleId, uint256 cap) = abi.decode(initData, (bytes32, uint256));
        address rule = _resolveRule(capRuleId);

        _config[configId][msg.sender][account] = CapConfig(rule, cap);
        emit IPolicy.PolicySet(configId, msg.sender, account);
    }

    /// @notice Whether `(id, account)` has been configured through the caller (`msg.sender`) multiplexer.
    function isInitialized(ConfigId id, address account) public view returns (bool) {
        return _config[id][msg.sender][account].rule != address(0);
    }

    /// @notice Checks `value` against the per-account cap by evaluating the resolved rule directly.
    function checkAction(
        ConfigId id,
        address account,
        address, /*target*/
        uint256 value,
        bytes calldata /*data*/
    )
        external
        view
        override
        returns (uint256)
    {
        CapConfig storage c = _config[id][msg.sender][account];
        if (c.rule == address(0)) revert NotInitialized(id, msg.sender, account);

        bytes memory out = IRule(c.rule).evaluate(abi.encode(value, c.cap));
        if (out.length != 32) revert InvalidRuleOutput();
        bool withinCap = abi.decode(out, (bool));

        return withinCap ? VALIDATION_SUCCESS : VALIDATION_FAILED;
    }
}
