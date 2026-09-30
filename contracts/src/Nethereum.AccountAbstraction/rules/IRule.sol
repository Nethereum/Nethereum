// SPDX-License-Identifier: MIT
pragma solidity ^0.8.23;

/// @title IRule
/// @notice A self-contained authorization rule: a pure/view evaluation over caller-supplied inputs that
///         returns an abi-encoded output. Rules are registered in a RuleRegistry and composed by
///         hand-crafted combinators; the authority (N-of-M) is enforced separately at the session-validator
///         layer, not here.
/// @dev    A rule runs inside ERC-4337 validation, so it MUST obey ERC-7562 storage rules: no flat
///         global-keyed storage reads during validation. Configuration is passed via `input` (calldata);
///         any per-account state a rule keeps MUST use associated storage keyed by the account address.
/// @dev    SECURITY: `evaluate()` MUST be gas-bounded independent of the values carried in `input` (e.g.
///         the action `value`). The rule address is owner-controlled, but `input` is not necessarily -
///         validation runs under a single, limited `MaxVerificationGas` budget shared with the rest of the
///         account's validation, so an unbounded-cost evaluation over attacker-influenced `input` can
///         exhaust that budget.
interface IRule {
    /// @param input abi-encoded, rule-specific inputs (configuration values and/or context).
    /// @return output abi-encoded, rule-specific result (e.g. a bool decision or a derived value).
    function evaluate(bytes calldata input) external view returns (bytes memory output);
}
