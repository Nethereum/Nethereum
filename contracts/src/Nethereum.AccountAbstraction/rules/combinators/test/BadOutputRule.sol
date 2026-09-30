// SPDX-License-Identifier: MIT
pragma solidity ^0.8.23;

import { IRule } from "../../IRule.sol";

/// @title BadOutputRule
/// @notice Test-only `IRule` that always returns malformed (non-32-byte) output, used to prove
///         `ValueCapCombinator.checkAction` reverts `InvalidRuleOutput` instead of failing an
///         unqualified `abi.decode` when a registered rule misbehaves.
contract BadOutputRule is IRule {
    function evaluate(bytes calldata /*input*/ ) external pure returns (bytes memory output) {
        return abi.encode(uint256(1), uint256(2));
    }
}
