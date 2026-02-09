// SPDX-License-Identifier: GPL-3.0-only
pragma solidity ^0.8.28;

import "@account-abstraction/contracts/core/BasePaymaster.sol";
import "@account-abstraction/contracts/core/Helpers.sol";

/**
 * Test paymaster that accepts all UserOperations without any validation.
 * Compatible with ERC-4337 v0.9 EntryPoint.
 */
contract TestPaymasterAcceptAll is BasePaymaster {
    constructor(IEntryPoint _entryPoint, address _owner) BasePaymaster(_entryPoint, _owner) {
    }

    function _validatePaymasterUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash,
        uint256 maxCost
    ) internal virtual override view returns (bytes memory context, uint256 validationData) {
        (userOp, userOpHash, maxCost);
        return ("", SIG_VALIDATION_SUCCESS);
    }
}
