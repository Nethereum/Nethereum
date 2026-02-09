// SPDX-License-Identifier: LGPL-3.0-only
pragma solidity ^0.8.23;

import { PackedUserOperation, _packValidationData } from "modulekit/external/ERC4337.sol";
import { IModule as IERC7579Module, VALIDATION_SUCCESS, VALIDATION_FAILED } from "erc7579/interfaces/IERC7579Module.sol";
import "../DataTypes.sol";
import "forge-std/interfaces/IERC165.sol";

/**
 * IPolicy are external contracts that enforce policies / permission on 4337/7579 executions
 * Since it's not the account calling into this contract, and check functions are called during the ERC4337 validation
 * phase, IPolicy implementations MUST follow ERC4337 storage and opcode restructions
 * A recommend storage layout to store policy related data:
 *      mapping(id   =>   msg.sender   =>   userOp.sender(account) => state)
 *                        ^ smartSession    ^ smart account (associated storage)
 */
interface IPolicy is IERC165 {
    event PolicySet(ConfigId id, address multiplexer, address account);
    /**
     * This function may be called by the multiplexer (SmartSessions) without deinitializing first.
     * Policies MUST overwrite the current state when this happens
     * @notice ATTENTION: This method is called during permission installation as part of the enabling policies flow.
     * A secure policy would minimize external calls from this method (ideally, to 0) to prevent passing control flow to
     * external contracts.
     */

    function initializeWithMultiplexer(address account, ConfigId configId, bytes calldata initData) external;
}

/**
 * IUserOpPolicy is a policy that enforces restrictions on user operations. It is called during the validation phase
 * of the ERC4337 execution.
 * Use this policy to enforce restrictions on user operations (userOp.gas, Time based restrictions).
 * The checkUserOpPolicy function should return a uint256 value that represents the policy's decision.
 * The policy's decision should be one of the following:
 * - VALIDATION_SUCCESS: The user operation is allowed.
 * - VALIDATION_FAILED: The user operation is not allowed.
 * - While it is possible to return values that pack validUntil and validAfter timestamps,
 *   SmartSession Policies can not utilize aggregator addresses. (PolicyLib.isFailed() will prevent this)
 */
interface IUserOpPolicy is IPolicy {
    function checkUserOpPolicy(ConfigId id, PackedUserOperation calldata userOp) external returns (uint256);
}

/**
 * IActionPolicy is a policy that enforces restrictions on actions. It is called during the validation phase
 * of the ERC4337 execution.
 * ERC7579 accounts natively support batched executions. So in one userOp, multiple actions can be executed.
 * SmartSession will destruct the execution batch, and call the policy for each action, if the policy is installed for
 * the actionId for the account.
 * Use this policy to enforce restrictions on individual actions (i.e. transfers, approvals, etc).
 * The checkAction function should return a uint256 value that represents the policy's decision.
 * The policy's decision should be one of the following:
 * - VALIDATION_SUCCESS: The action is allowed.
 * - VALIDATION_FAILED: The action is not allowed.
 */
interface IActionPolicy is IPolicy {
    function checkAction(
        ConfigId id,
        address account,
        address target,
        uint256 value,
        bytes calldata data
    )
        external
        returns (uint256);
}

/**
 * I1271Policy is a policy that enforces restrictions on 1271 signed actions. It is called during an ERC1271 signature
 * validation
 */
interface I1271Policy is IPolicy {
    // request sender is probably protocol, so can introduce policies based on it.
    function check1271SignedAction(
        ConfigId id,
        address requestSender,
        address account,
        bytes32 hash,
        bytes calldata signature
    )
        external
        view
        returns (bool);
}
