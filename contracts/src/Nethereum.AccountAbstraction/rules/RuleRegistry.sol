// SPDX-License-Identifier: MIT
pragma solidity ^0.8.23;

import { IRule } from "./IRule.sol";

/// @title RuleRegistry
/// @notice The per-deployment trusted set of rules. Rule ids are registered by the owner and, once set,
///         are immutable; registration can be permanently closed so the trusted set becomes fixed for the
///         lifetime of the instance. A registry INSTANCE is the version: a new set of rules (or a change to
///         an already-set id) is shipped as a new registry deployment, never a mutation of this one.
/// @dev    `execute` is the dispatch entrypoint used by combinators/validators: it looks up the rule by id
///         and forwards the abi-encoded input, returning the abi-encoded output unchanged.
contract RuleRegistry {
    error NotOwner();
    error RegistrationIsClosed();
    error ZeroRule();
    error RuleAlreadySet();
    error UnknownRule();

    event RuleRegistered(bytes32 indexed ruleId, address rule);
    event RegistrationClosed();

    address public owner;
    bool public registrationClosed;
    mapping(bytes32 => address) public ruleOf;

    constructor() {
        owner = msg.sender;
    }

    /// @notice Registers a rule under `ruleId`. Reverts if the id is already set - registered ids are
    ///         immutable for the lifetime of this registry instance.
    function registerRule(bytes32 ruleId, address rule) external {
        if (msg.sender != owner) revert NotOwner();
        if (registrationClosed) revert RegistrationIsClosed();
        if (rule == address(0)) revert ZeroRule();
        if (ruleOf[ruleId] != address(0)) revert RuleAlreadySet();

        ruleOf[ruleId] = rule;
        emit RuleRegistered(ruleId, rule);
    }

    /// @notice Permanently closes registration. Irreversible - there is no reopen path.
    function closeRegistration() external {
        if (msg.sender != owner) revert NotOwner();

        registrationClosed = true;
        emit RegistrationClosed();
    }

    /// @notice Dispatches `input` to the rule registered under `ruleId` and returns its output unchanged.
    /// @dev    General-purpose read-only dispatch helper (view/STATICCALL), not part of the ERC-4337
    ///         validation hot path: combinators (e.g. `ValueCapCombinator`) evaluate their resolved rule
    ///         directly during `checkAction` and never call `execute`. It therefore does not carry the
    ///         same gas/associated-storage constraints as a policy's `checkAction`.
    function execute(bytes32 ruleId, bytes calldata input) external view returns (bytes memory output) {
        address rule = ruleOf[ruleId];
        if (rule == address(0)) revert UnknownRule();

        return IRule(rule).evaluate(input);
    }
}
