// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import "../DataTypes.sol";
import { ISmartSession } from "../ISmartSession.sol";
import { IPolicy, IActionPolicy, I1271Policy } from "../interfaces/IPolicy.sol";

import { Execution, ExecutionLib as ExecutionLib } from "./ExecutionLib.sol";
import { ValidationDataLib } from "./ValidationDataLib.sol";
import { IdLib } from "./IdLib.sol";
import { EncodeLib } from "./EncodeLib.sol";
import { EnumerableSet } from "../utils/EnumerableSet4337.sol";

import { PackedUserOperation } from "modulekit/external/ERC4337.sol";
import { IERC7579Account } from "erc7579/interfaces/IERC7579Account.sol";
import { ExcessivelySafeCall } from "excessively-safe-call/ExcessivelySafeCall.sol";

library PolicyLib {
    using EnumerableSet for EnumerableSet.AddressSet;
    using ExecutionLib for *;
    using IdLib for *;
    using PolicyLib for *;
    using EncodeLib for *;
    using ValidationDataLib for ValidationData;
    using ExcessivelySafeCall for address;

    /**
     * Helper function to evaluate the ValidationData result of a policy check.
     * To prevent Policies from returning a packed aggregator value, we use this bitmask.
     */
    function isFailed(ValidationData packedData) internal pure returns (bool sigFailed) {
        sigFailed = (
            ValidationData.unwrap(packedData) & 0x000000000000000000000000ffffffffffffffffffffffffffffffffffffffff
        ) != 0;
    }

    /**
     * Multi-purpose helper function that interacts with external Policy Contracts.
     * This function can be used to check different types of IPolicy functions.
     * The specific function to be called on the policy is determined by the callDataOnIPolicy parameter.
     *
     * @dev This function iterates through all policies associated with the given permissionId and account,
     *      calls each policy with the provided calldata, and intersects the resulting validation data.
     *      It will revert if any policy check fails or if there are fewer policies than the specified minimum.
     *
     * @param $self The Policy storage struct containing the list of policies.
     * @param permissionId The identifier for the permission being checked.
     * @param callOnIPolicy The encoded function call data to be executed on each policy contract.
     * @param minPolicies The minimum number of policies that must be present and checked.
     *
     * @return vd The intersected ValidationData result from all policy checks.
     */
    function check(
        Policy storage $self,
        PermissionId permissionId,
        bytes memory callOnIPolicy,
        uint256 minPolicies
    )
        internal
        returns (ValidationData vd)
    {
        // Get the list of policies for the given permissionId and account
        address[] memory policies = $self.policyList[permissionId].values({ account: msg.sender });
        uint256 length = policies.length;

        // Ensure the minimum number of policies is met.
        // Revert otherwise. Current minPolicies for userOp policies is 0.
        // Current minPolicies for action policies is 1.
        // This ensures sudo (open) permissions can be created only by explicitly setting SudoPolicy/YesPolicy
        // as the only action policies
        if (minPolicies > length) revert ISmartSession.NoPoliciesSet(permissionId);

        // Iterate over all policies and intersect the validation data
        for (uint256 i; i < length; i++) {
            // Intersect the validation data from this policy with the accumulated result
            vd = vd.intersect(policies[i].callPolicy(permissionId, callOnIPolicy));
        }
    }

    /**
     * Same as check but will not revert if minimum number of policies is not met.
     * This allows a second check with the FALLBACK_ACTIONID.
     *
     * @param $self The Policy storage struct containing the list of policies.
     * @param permissionId The identifier for the permission being checked.
     * @param callOnIPolicy The encoded function call data to be executed on each policy contract.
     * @param minPolicies The minimum number of policies that must be present and checked.
     *
     * @return vd This value could either be:
     *             - RETRY_WITH_FALLBACK if no adequate policies were set for this permissionId. Signaling the caller,
     * that the Policy fallback procedure SHOULD be used
     *             - Intersected Validation data of policies.
     */
    function tryCheck(
        Policy storage $self,
        PermissionId permissionId,
        bytes memory callOnIPolicy,
        uint256 minPolicies
    )
        internal
        returns (ValidationData vd)
    {
        // Get the list of policies for the given permissionId and account
        address[] memory policies = $self.policyList[permissionId].values({ account: msg.sender });
        uint256 length = policies.length;

        // Ensure the minimum number of policies is met. I.e. there are enough policies configured for given ActionId
        // Current minPolicies is 1 for action policies. That means, if there is no policies at all confgured
        // for a given ActionId (ActionId was not enabled), execution proceeds to the fallback flow.
        // There can be any amount of fallback action policies configured, and those will be applied to all actionIds,
        // that were not configured explicitly.
        if (minPolicies > length) {
            return RETRY_WITH_FALLBACK;
        }

        // Iterate over all policies and intersect the validation data
        for (uint256 i; i < length; i++) {
            // Intersect the validation data from this policy with the accumulated result
            vd = vd.intersect(policies[i].callPolicy(permissionId, callOnIPolicy));
        }

        // Make sure policies can't alter the control flow
        if (vd == RETRY_WITH_FALLBACK) revert ISmartSession.ForbiddenValidationData();
    }

    function callPolicy(
        address policy,
        PermissionId permissionId,
        bytes memory callOnIPolicy
    )
        internal
        returns (ValidationData _vd)
    {
        // Call the policy contract with the provided calldata
        (bool success, bytes memory returnDataFromPolicy) = policy.excessivelySafeCall({
            // To better align with the ERC-4337 validation rules, we replaced gasleft() with type(uint256).max.
            // This will accomplish the same result of forwarding all remaining gas.
            // Note that there is no error for attempting to use more gas than is currently available, as this has been
            // allowed since https://eips.ethereum.org/EIPS/eip-150#specification
            _gas: type(uint256).max,
            _value: 0,
            _maxCopy: 32,
            _calldata: callOnIPolicy
        });
        uint256 validationDataFromPolicy;
        assembly {
            //if (!success) revert PolicyCheckReverted(bytes32);
            if iszero(success) {
                mstore(0, 0xf4270752) // `PolicyCheckReverted(bytes32)`
                mstore(0x20, mload(add(returnDataFromPolicy, 0x20)))
                revert(0x1c, 0x24)
            }
            validationDataFromPolicy := mload(add(returnDataFromPolicy, 0x20))
        }
        _vd = ValidationData.wrap(validationDataFromPolicy);
        // Prevent a malfunctioning policy, to return the magic value RETRY_WITH_FALLBACK and change control flow
        if (_vd.isFailed()) revert ISmartSession.PolicyViolation(permissionId, policy);
    }

    /**
     * Checks policies for a single ERC7579 execution within a user operation.
     * This function validates the execution against relevant action policies.
     *
     * @dev This function prevents potential bypass of policy checks through nested executions
     *      by disallowing self-calls to the execute function.
     *
     * @param $policies The storage mapping of action policies.
     * @param permissionId The identifier for the permission being checked.
     * @param target The target address of the execution.
     * @param value The ETH value being sent with the execution.
     * @param callData The call data of the execution.
     * @param minPolicies The minimum number of policies that must be checked.
     *
     * @return vd The validation data resulting from the policy checks.
     */
    function checkSingle7579Exec(
        mapping(ActionId => Policy) storage $policies,
        PermissionId permissionId,
        address target,
        uint256 value,
        bytes calldata callData,
        uint256 minPolicies
    )
        internal
        returns (ValidationData vd)
    {
        // Extract the function selector from the call data
        bytes4 targetSig;
        if (callData.length < 4) {
            targetSig = IdLib.VALUE_SELECTOR;
        } else {
            targetSig = bytes4(callData[0:4]);
        }

        // Prevent potential bypass of policy checks through nested self executions
        if (targetSig == IERC7579Account.execute.selector && target == msg.sender) {
            revert ISmartSession.InvalidSelfCall();
        }

        // Prevent fallback action from being used directly
        if (target == FALLBACK_TARGET_FLAG) revert ISmartSession.InvalidTarget();

        // malloc for actionId
        ActionId actionId;

        // should the target of this call be the smart session module itself, we will use the designated sentinel
        // actionId for smartsession calls. The user has to explicitly set the smartsession call policy to allow this.
        // @dev this is a special case, as a session key should normally not be utilized to configure other sessions
        if (target == address(this)) {
            actionId = FALLBACK_ACTIONID_SMARTSESSION_CALL;
        }
        // proceed with the normal flow
        else {
            // Generate the action ID based on the target and function selector
            actionId = target.toActionId(targetSig);
            // Check the relevant action policy
            vd = $policies[actionId].tryCheck({
                permissionId: permissionId,
                callOnIPolicy: abi.encodeCall(
                    IActionPolicy.checkAction, (permissionId.toConfigId(actionId), msg.sender, target, value, callData)
                ),
                minPolicies: minPolicies
            });
            // If tryCheck returns RETRY_WITH_FALLBACK magic value, that means not enough policies were configured
            // for the actionId. Proceed with checking fallback action policies ($policies[FALLBACK_ACTIONID]).
            if (vd == RETRY_WITH_FALLBACK) actionId = FALLBACK_ACTIONID;
            // otherwise return the validation data
            else return vd;
        }
        // call the fallback policy for either FALLBACK_ACTIONID or FALLBACK_ACTIONID_SMARTSESSION_CALL
        // If no policies were configured for FALLBACK_ACTIONID or FALLBACK_ACTIONID_SMARTSESSION_CALL this call will
        // revert
        vd = $policies[actionId].check({
            permissionId: permissionId,
            callOnIPolicy: abi.encodeCall(
                IActionPolicy.checkAction, (permissionId.toConfigId(actionId), msg.sender, target, value, callData)
            ),
            minPolicies: minPolicies
        });
        return vd;
    }

    /**
     * Checks policies for a batch of ERC7579 executions within a user operation.
     * This function iterates through each execution in the batch and validates them against relevant action policies.
     *
     * @dev This function decodes the batch of executions from the user operation's call data,
     *      then applies policy checks to each execution individually.
     *      The validation results are intersected to ensure all executions pass the policy checks.
     *
     * @param $policies The storage mapping of action policies.
     * @param userOp The packed user operation being validated.
     * @param permissionId The identifier for the permission being checked.
     * @param minPolicies The minimum number of policies that must be checked for each execution.
     *
     * @return vd The final validation data resulting from intersecting all policy checks.
     */
    function checkBatch7579Exec(
        mapping(ActionId => Policy) storage $policies,
        PackedUserOperation calldata userOp,
        PermissionId permissionId,
        uint256 minPolicies
    )
        internal
        returns (ValidationData vd)
    {
        // Decode the batch of 7579 executions from the user operation's call data
        Execution[] calldata executions = userOp.callData.decodeUserOpCallData().decodeBatch();
        uint256 length = executions.length;
        // Revert if there are no executions in the batch
        if (length == 0) revert ISmartSession.NoExecutionsInBatch();

        // Iterate through each execution in the batch
        for (uint256 i; i < length; i++) {
            Execution calldata execution = executions[i];

            // Check policies for the current execution and intersect the result with previous checks
            ValidationData _vd = checkSingle7579Exec({
                $policies: $policies,
                permissionId: permissionId,
                target: execution.target,
                value: execution.value,
                callData: execution.callData,
                minPolicies: minPolicies
            });

            vd = vd.intersect(_vd);
        }
    }

    /**
     * Checks the validity of an ERC1271 signature against all relevant policies.
     *
     * @dev This function iterates through all policies for the given permission and checks
     *      the signature validity using each policy's check1271SignedAction function.
     *
     * @param $self The storage reference to the Policy struct.
     * @param account The address of the account associated with the signature.
     * @param requestSender The address of the entity requesting the signature check.
     * @param hash The hash of the signed data.
     * @param signature The signature to be validated.
     * @param permissionId The identifier of the permission being checked.
     * @param configId The configuration identifier.
     * @param minPoliciesToEnforce at least this number of policies should be enforced.
     *
     * @return valid Returns true if the signature is valid according to all policies, false otherwise.
     */
    function checkERC1271(
        Policy storage $self,
        address account,
        address requestSender,
        bytes32 hash,
        bytes calldata signature,
        PermissionId permissionId,
        ConfigId configId,
        uint256 minPoliciesToEnforce
    )
        internal
        view
        returns (bool valid)
    {
        address[] memory policies = $self.policyList[permissionId].values({ account: account });
        uint256 length = policies.length;
        if (minPoliciesToEnforce > length) revert ISmartSession.NoPoliciesSet(permissionId);

        // iterate over all policies and intersect the validation data
        for (uint256 i; i < length; i++) {
            valid = I1271Policy(policies[i]).check1271SignedAction({
                id: configId,
                requestSender: requestSender,
                account: account,
                hash: hash,
                signature: signature
            });
            // If any policy check fails, return false immediately
            if (!valid) return valid;
        }
    }

    /**
     * Checks if the specified policies are enabled for a given permission and smart account.
     *
     * @dev This function verifies that all specified policies are both present in the policy list
     *      and initialized for the given smart account and config.
     *
     * @param $policies The storage reference to the Policy struct.
     * @param permissionId The identifier of the permission being checked.
     * @param smartAccount The address of the smart account.
     * @param policyDatas An array of PolicyData structs representing the policies to check.
     *
     * @return enabled Returns true if all policies are enabled, false if none are enabled.
     *              Reverts if policies are partially enabled.
     */
    function areEnabled(
        Policy storage $policies,
        PermissionId permissionId,
        address smartAccount,
        PolicyData[] calldata policyDatas
    )
        internal
        view
        returns (bool enabled)
    {
        uint256 length = policyDatas.length;
        enabled = true;
        if (length == 0) return enabled; // 0 policies are always enabled lol
        for (uint256 i; i < length; i++) {
            PolicyData memory policyData = policyDatas[i];
            IPolicy policy = IPolicy(policyData.policy);
            // check if policy is enabled
            if (!$policies.policyList[permissionId].contains(smartAccount, address(policy))) {
                return false;
            }
        }
    }

    /**
     * Checks if the specified action policies are enabled for a given permission and smart account.
     *
     * @dev This function verifies that all specified action policies are enabled.
     *
     * @param $self The storage reference to the EnumerableActionPolicy struct.
     * @param permissionId The identifier of the permission being checked.
     * @param smartAccount The address of the smart account.
     * @param actionPolicyDatas An array of ActionData structs representing the action policies to check.
     *
     * @return enabled Returns true if all action policies are enabled, false if none are enabled.
     *              Reverts if action policies are partially enabled.
     */
    function areEnabled(
        EnumerableActionPolicy storage $self,
        PermissionId permissionId,
        address smartAccount,
        ActionData[] calldata actionPolicyDatas
    )
        internal
        view
        returns (bool enabled)
    {
        uint256 length = actionPolicyDatas.length;
        enabled = true;
        if (length == 0) return enabled; // 0 actions are always enabled
        for (uint256 i; i < length; i++) {
            ActionData calldata actionPolicyData = actionPolicyDatas[i];
            ActionId actionId = actionPolicyData.actionTarget.toActionId(actionPolicyData.actionTargetSelector);
            // Check if the action policy is enabled
            if (!$self.actionPolicies[actionId].areEnabled(permissionId, smartAccount, actionPolicyData.actionPolicies))
            {
                return false;
            }
        }
    }
}
