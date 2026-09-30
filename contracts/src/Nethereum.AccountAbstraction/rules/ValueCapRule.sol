// SPDX-License-Identifier: MIT
pragma solidity ^0.8.23;

import { IRule } from "./IRule.sol";

/// @title ValueCapRule
/// @notice Passes when a value is within a cap. A pure rule — all inputs are supplied by the caller, no
///         storage is read, so it is trivially ERC-7562 safe during validation.
/// @dev    input  = abi.encode(uint256 value, uint256 cap)
///         output = abi.encode(bool withinCap)  // true iff value <= cap
/// @dev    SECURITY: `cap` is a PER-ACTION ceiling, not a cumulative spending budget. This rule keeps no
///         running total, so a userOp that batches N executions each individually within the cap can move
///         up to N x cap in a single operation, and the same is true again on the next userOp. For a
///         cumulative/session spending limit, use a spending-limit-style rule that tracks a persistent
///         `used` amount per account (see `ERC20SpendingLimitPolicy`), not this rule.
contract ValueCapRule is IRule {
    function evaluate(bytes calldata input) external pure returns (bytes memory output) {
        (uint256 value, uint256 cap) = abi.decode(input, (uint256, uint256));
        return abi.encode(value <= cap);
    }
}
