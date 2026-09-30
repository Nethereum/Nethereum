// SPDX-License-Identifier: MIT
pragma solidity ^0.8.23;

/// @title PayableTarget
/// @notice Minimal value-bearing session-action target for R1c-b's end-to-end quorum×cap BDD: a real
///         payable selector so a SmartSession action can carry msg.value for `ValueCapCombinator` to cap.
///         `TestCounter.count()` is value-0, so it can never exceed any cap - this contract exists purely
///         to make the value dimension real. No state beyond the native balance itself is asserted on.
contract PayableTarget {
    function deposit() external payable { }
}
