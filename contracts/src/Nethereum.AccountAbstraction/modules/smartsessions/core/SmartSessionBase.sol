// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import "../DataTypes.sol";
import { ISmartSession } from "../ISmartSession.sol";
import { EnumerableSet } from "../utils/EnumerableSet4337.sol";
import { EnumerableMap } from "../utils/EnumerableMap4337.sol";
import { ConfigLib } from "../lib/ConfigLib.sol";
import { EncodeLib } from "../lib/EncodeLib.sol";
import { PolicyLib } from "../lib/PolicyLib.sol";
import { IdLib } from "../lib/IdLib.sol";
import { HashLib } from "../lib/HashLib.sol";
import { SmartSessionModeLib } from "../lib/SmartSessionModeLib.sol";
import { NonceManager } from "./NonceManager.sol";
import { FlatBytesLib } from "flatbytes/BytesLib.sol";

abstract contract SmartSessionBase is ISmartSession, NonceManager {
    using EnumerableMap for EnumerableMap.Bytes32ToBytes32Map;
    using EnumerableSet for EnumerableSet.Bytes32Set;
    using EnumerableSet for EnumerableSet.AddressSet;
    using SmartSessionModeLib for SmartSessionMode;
    using HashLib for *;
    using PolicyLib for *;
    using ConfigLib for *;
    using EncodeLib for *;
    using IdLib for *;
    using ConfigLib for Policy;
    using ConfigLib for EnumerableActionPolicy;
    using FlatBytesLib for FlatBytesLib.Bytes;

    /*´:°•.°+.*•´.*:˚.°*.˚•´.°:°•.°•.*•´.*:˚.°*.˚•´.°:°•.°+.*•´.*:*/
    /*                    SmartSession Storage                    */
    /*.•°:°.´+˚.*°.˚:*.´•*.+°.•°:´*.´•*.•°.•°:°.´:•˚°.*°.˚:*.´+°.•*/

    /**
     * In order to comply with ERC-4337 storage restrictions, every storage in smart session is using associated
     * storage.
     */
    Policy internal $userOpPolicies;
    Policy internal $erc1271Policies;
    EnumerableActionPolicy internal $actionPolicies;
    EnumerableSet.Bytes32Set internal $enabledSessions;
    EnumerableERC7739Config internal $enabledERC7739;
    mapping(PermissionId permissionId => mapping(address smartAccount => SignerConf conf)) internal $sessionValidators;
    mapping(PermissionId permissionId => mapping(address smartAccount => bool permitERC4337Paymaster)) internal
        $permitERC4337Paymaster;

    /**
     * Before enabling policies, we need to check if the session is enabled for the caller and the given permission
     * after enabling policies, we need to check if the session is still enabled for the caller and the given
     * permission. This is to ensure that the session is still enabled after the operation and no re-entrancy is
     * possible
     */
    modifier enableWithPermissionId(PermissionId permissionId) {
        // Check if the session is enabled for the caller and the given permission before enabling policies on it
        $enabledSessions.requirePermissionIdEnabled(permissionId);
        _;
        // Check if the session is enabled for the caller and the given permission after enabling policies on it
        // this is to ensure that the session is still enabled after the operation and no re-entrancy is possible
        $enabledSessions.requirePermissionIdEnabled(permissionId);
    }

    /**
     * Before disabling policies, we need to check if the session is enabled for the caller and the given permission
     */
    modifier disableWithPermissionId(PermissionId permissionId) {
        // Check if the session is enabled for the caller and the given permission before enabling policies on it
        $enabledSessions.requirePermissionIdEnabled(permissionId);
        _;
    }

    function getPermissionIDs(address account) external view returns (PermissionId[] memory permissionIds) {
        bytes32[] memory _permissionIds = $enabledSessions.values(account);
        assembly {
            permissionIds := _permissionIds
        }
    }

    /**
     * @notice Enable user operation policies for a specific permission
     * @dev This function allows adding or updating user operation policies
     * @param permissionId The unique identifier for the permission
     * @param userOpPolicies An array of PolicyData structures containing policy information
     */
    function enableUserOpPolicies(
        PermissionId permissionId,
        PolicyData[] memory userOpPolicies
    )
        public
        enableWithPermissionId(permissionId)
    {
        // Enable the specified user operation policies
        $userOpPolicies.enable({
            policyType: PolicyType.USER_OP,
            permissionId: permissionId,
            configId: permissionId.toUserOpPolicyId().toConfigId(),
            policyDatas: userOpPolicies,
            useRegistry: true
        });
    }

    /**
     * @notice Disable specific user operation policies for a given permission
     * @param permissionId The unique identifier for the permission
     * @param policies An array of policy addresses to be disabled
     */
    function disableUserOpPolicies(
        PermissionId permissionId,
        address[] calldata policies
    )
        public
        disableWithPermissionId(permissionId)
    {
        // Disable the specified user operation policies
        $userOpPolicies.disable({
            policyType: PolicyType.USER_OP,
            smartAccount: msg.sender,
            permissionId: permissionId,
            policies: policies
        });
    }

    function setPermit4337Paymaster(PermissionId permissionId, bool enabled) external {
        $enabledSessions.requirePermissionIdEnabled(permissionId);
        _setPermit4337Paymaster(permissionId, enabled);
    }

    function _setPermit4337Paymaster(PermissionId permissionId, bool enabled) internal {
        $permitERC4337Paymaster[permissionId][msg.sender] = enabled;
        emit PermissionIdPermit4337Paymaster(permissionId, msg.sender, enabled);
    }

    /**
     * @notice Enable ERC1271 policies for a specific permission
     * @param permissionId The unique identifier for the permission
     * @param erc1271Policies An array of PolicyData structures containing ERC1271 policy information
     */
    function enableERC1271Policies(
        PermissionId permissionId,
        ERC7739Data calldata erc1271Policies
    )
        public
        enableWithPermissionId(permissionId)
    {
        // Enable the ERC1271 policies
        $enabledERC7739.enable({ contexts: erc1271Policies.allowedERC7739Content, permissionId: permissionId });
        $erc1271Policies.enable({
            policyType: PolicyType.ERC1271,
            permissionId: permissionId,
            configId: permissionId.toErc1271PolicyId().toConfigId(),
            policyDatas: erc1271Policies.erc1271Policies,
            useRegistry: true
        });
    }

    /**
     * @notice Disable specific ERC1271 policies and contents for a given permission
     * @param permissionId The unique identifier for the permission
     * @param policies An array of policy addresses to be disabled
     * @param contexts An array of 7739 contents to be disabled
     */
    function disableERC1271Policies(
        PermissionId permissionId,
        address[] calldata policies,
        ERC7739Context[] calldata contexts
    )
        public
        disableWithPermissionId(permissionId)
    {
        // Check if the session is enabled for the caller and the given permission
        // forgefmt: disable-next-item
        if (!$enabledSessions.contains(msg.sender, PermissionId.unwrap(permissionId))) revert InvalidSession(permissionId);

        $enabledERC7739.disable(contexts, permissionId, msg.sender);

        // Disable the specified ERC1271 policies
        $erc1271Policies.disable({
            policyType: PolicyType.ERC1271,
            smartAccount: msg.sender,
            permissionId: permissionId,
            policies: policies
        });
    }

    /**
     * @notice Enable action policies for a specific permission
     * @param permissionId The unique identifier for the permission
     * @param actionPolicies An array of ActionData structures containing action policy information
     */
    function enableActionPolicies(
        PermissionId permissionId,
        ActionData[] memory actionPolicies
    )
        public
        enableWithPermissionId(permissionId)
    {
        // Enable the action policies
        $actionPolicies.enable({ permissionId: permissionId, actionPolicyDatas: actionPolicies, useRegistry: true });
    }

    /**
     * @notice Disable specific action policies for a given permission and action ID
     * @param permissionId The unique identifier for the permission
     * @param actionId The specific action identifier
     */
    function disableActionId(
        PermissionId permissionId,
        ActionId actionId
    )
        public
        disableWithPermissionId(permissionId)
    {
        // Disable all action policies for the given action ID
        // No need to emit events here, as unlike with 7739contents and 1271 policies,
        // here disabling the actionId means all action policies are also disabled
        $actionPolicies.actionPolicies[actionId].policyList[permissionId].removeAll(msg.sender);

        // remove action Id from enabledActionIds
        $actionPolicies.enabledActionIds[permissionId].remove(msg.sender, ActionId.unwrap(actionId));
        emit ISmartSession.ActionIdDisabled(permissionId, actionId, msg.sender);
    }

    /**
     * @notice Disable action id for a given permission and action ID
     * @param permissionId The unique identifier for the permission
     * @param actionId The specific action identifier
     * @param policies An array of policy addresses to be disabled
     */
    function disableActionPolicies(
        PermissionId permissionId,
        ActionId actionId,
        address[] calldata policies
    )
        public
        disableWithPermissionId(permissionId)
    {
        // Disable the specified action policies for the given action ID
        $actionPolicies.actionPolicies[actionId].disable({
            policyType: PolicyType.ACTION,
            smartAccount: msg.sender,
            permissionId: permissionId,
            policies: policies
        });

        // remove the actionId from the enabledActionIds if no policies are left
        if ($actionPolicies.actionPolicies[actionId].policyList[permissionId].length(msg.sender) == 0) {
            $actionPolicies.enabledActionIds[permissionId].remove(msg.sender, ActionId.unwrap(actionId));
            emit ISmartSession.ActionIdDisabled(permissionId, actionId, msg.sender);
        }
    }

    /**
     * @notice Enable multiple sessions with their associated policies
     * since this function is only called during the ERC-4337 execution phase, it is safe to use the registry
     * @param sessions An array of Session structures to be enabled
     * @return permissionIds An array of PermissionId values corresponding to the enabled sessions
     */
    function enableSessions(Session[] calldata sessions) external returns (PermissionId[] memory permissionIds) {
        return _enableSessions(sessions, true);
    }

    /**
     * @notice Enable multiple sessions with their associated policies
     * @param sessions An array of Session structures to be enabled
     * @param useRegistry A flag to indicate whether to use a registry check for the policies and session validator
     * @return permissionIds An array of PermissionId values corresponding to the enabled sessions
     */
    function _enableSessions(
        Session[] calldata sessions,
        bool useRegistry
    )
        internal
        returns (PermissionId[] memory permissionIds)
    {
        uint256 length = sessions.length;
        if (length == 0) revert InvalidData();

        permissionIds = new PermissionId[](length);

        for (uint256 i; i < length; i++) {
            Session calldata session = sessions[i];
            PermissionId permissionId = session.toPermissionId();

            // Enable UserOp policies
            $userOpPolicies.enable({
                policyType: PolicyType.USER_OP,
                permissionId: permissionId,
                configId: permissionId.toUserOpPolicyId().toConfigId(),
                policyDatas: session.userOpPolicies,
                useRegistry: useRegistry
            });

            // Enable ERC1271 policies
            $erc1271Policies.enable({
                policyType: PolicyType.ERC1271,
                permissionId: permissionId,
                configId: permissionId.toErc1271PolicyId().toConfigId(),
                policyDatas: session.erc7739Policies.erc1271Policies,
                useRegistry: useRegistry
            });
            $enabledERC7739.enable(session.erc7739Policies.allowedERC7739Content, permissionId);

            // Enable Action policies
            $actionPolicies.enable({
                permissionId: permissionId,
                actionPolicyDatas: session.actions,
                useRegistry: useRegistry
            });

            // Add the session to the list of enabled sessions for the caller
            $enabledSessions.add({ account: msg.sender, value: PermissionId.unwrap(permissionId) });

            _setPermit4337Paymaster(permissionId, session.permitERC4337Paymaster);

            // Enable the ISessionValidator for this session
            if (!_isISessionValidatorSet(permissionId, msg.sender)) {
                $sessionValidators.enable({
                    permissionId: permissionId,
                    sessionValidator: session.sessionValidator,
                    sessionValidatorConfig: session.sessionValidatorInitData,
                    useRegistry: useRegistry
                });
            }
            permissionIds[i] = permissionId;
            emit SessionCreated(permissionId, msg.sender);
        }
    }

    /**
     * @notice Remove a session and all its associated policies
     * @param permissionId The unique identifier for the session to be removed
     */
    function removeSession(PermissionId permissionId) public {
        if (permissionId == EMPTY_PERMISSIONID) revert InvalidSession(permissionId);

        // Remove all UserOp policies for this session
        $userOpPolicies.policyList[permissionId].removeAll(msg.sender);

        // Remove all ERC1271 policies for this session
        $erc1271Policies.policyList[permissionId].removeAll(msg.sender);

        // Remove all Action policies for this session
        uint256 actionLength = $actionPolicies.enabledActionIds[permissionId].length(msg.sender);
        for (uint256 i; i < actionLength; i++) {
            ActionId actionId = ActionId.wrap($actionPolicies.enabledActionIds[permissionId].at(msg.sender, i));
            $actionPolicies.actionPolicies[actionId].policyList[permissionId].removeAll(msg.sender);
        }

        // removing all stored actionIds
        $actionPolicies.enabledActionIds[permissionId].removeAll(msg.sender);

        $sessionValidators.disable({ permissionId: permissionId, smartAccount: msg.sender });
        delete $permitERC4337Paymaster[permissionId][msg.sender];

        // Remove all ERC1271 policies for this session
        $enabledSessions.remove({ account: msg.sender, value: PermissionId.unwrap(permissionId) });
        $enabledERC7739.removeAll({ permissionId: permissionId, smartAccount: msg.sender });
        emit SessionRemoved(permissionId, msg.sender);
    }

    /**
     * Initialize the module with the given data
     *
     * @param data The data to initialize the module with
     *         data is expected to be in the format of: abi.encode(SmartSessionMode, abi.encode(Session[]))
     *         if no data is provided, the module will be installed without any sessions
     */
    function onInstall(bytes calldata data) external override {
        // Its possible that the module was installed before and when uninstalling the module, the smart session storage
        // for that smart account was not zero'ed correctly. In such cases, we need to check if the smart account has
        // still some enabled permissions / sessions set.
        // re-enabling these sessions will cause the smart account to be in the same state as before, potentially
        // activating sessions that the user thought were terminated. This MUST be avoided.
        // if this case happens, it's not possible for the account to install the module again, unless the account calls
        // into the removreSession functions to disable all dangling permissions
        // forgefmt: disable-next-item
        if ($enabledSessions.length({ account: msg.sender }) != 0) revert SmartSessionModuleAlreadyInstalled(msg.sender);
        // It's allowed to install smartsessions on a ERC7579 account without any params
        if (data.length == 0) return;

        // data is expected to be in the format of:
        //    abi.encodePacked(SmartSessionMode, abi.encode(Session[]))
        SmartSessionMode mode = SmartSessionMode(uint8(bytes1(data[:1])));
        // ensure that the mode provided is a valid ENABLE mode
        if (!mode.isEnableMode()) revert InvalidMode();
        // nudge the data pointer to the next byte to make decoding easier
        data = data[1:];
        Session[] calldata sessions;

        // equivalent of abi.decode(data,Session[])
        assembly ("memory-safe") {
            let dataPointer := add(data.offset, calldataload(data.offset))

            sessions.offset := add(dataPointer, 32)
            sessions.length := calldataload(dataPointer)
        }
        _enableSessions(sessions, mode.useRegistry());
    }

    /**
     * De-initialize the module with the given data.
     * All PermissionIds will be wiped from storage
     */
    function onUninstall(bytes calldata /*data*/ ) external override {
        uint256 configIdsCnt = $enabledSessions.length({ account: msg.sender });

        for (uint256 i; i < configIdsCnt; i++) {
            // always remove index 0 since the array is shifted down when the first item is removed
            PermissionId configId = PermissionId.wrap($enabledSessions.at({ account: msg.sender, index: 0 }));
            removeSession(configId);
        }
    }

    function isInitialized(address smartAccount) external view override returns (bool) {
        return $enabledSessions.length({ account: smartAccount }) != 0;
    }

    function isModuleType(uint256 typeID) external pure override returns (bool) {
        return typeID == ERC7579_MODULE_TYPE_VALIDATOR;
    }

    function getSessionDigest(
        PermissionId permissionId,
        address account,
        Session memory data,
        SmartSessionMode mode
    )
        public
        view
        returns (bytes32)
    {
        uint256 nonce = $signerNonce[permissionId][account];
        return data.sessionDigest({ account: account, mode: mode, nonce: nonce });
    }

    function getPermissionId(Session calldata session) public pure returns (PermissionId permissionId) {
        permissionId = session.toPermissionId();
    }

    function _isISessionValidatorSet(PermissionId permissionId, address account) internal view returns (bool) {
        return address($sessionValidators[permissionId][account].sessionValidator) != address(0);
    }

    function isISessionValidatorSet(PermissionId permissionId, address account) external view returns (bool) {
        return _isISessionValidatorSet(permissionId, account);
    }

    function isPermissionEnabled(PermissionId permissionId, address account) external view returns (bool) {
        return $enabledSessions.contains(account, PermissionId.unwrap(permissionId));
    }

    // This function accepts not the array of policies, but full PolicyData,
    // So it is easier to use it with an EnableSessions object
    // If you just need to check array of addresses, use is____PolicyEnabled methods in a loop
    function areUserOpPoliciesEnabled(
        address account,
        PermissionId permissionId,
        PolicyData[] calldata userOpPolicies
    )
        external
        view
        returns (bool)
    {
        return $userOpPolicies.areEnabled({
            permissionId: permissionId,
            smartAccount: account,
            policyDatas: userOpPolicies
        });
    }

    // This function accepts not the array of policies, but full PolicyData,
    // So it is easier to use it with an EnableSessions object
    function areERC1271PoliciesEnabled(
        address account,
        PermissionId permissionId,
        PolicyData[] calldata erc1271Policies
    )
        external
        view
        returns (bool)
    {
        return $erc1271Policies.areEnabled({
            permissionId: permissionId,
            smartAccount: account,
            policyDatas: erc1271Policies
        });
    }

    // This function accepts not the array of policies, but full ActionData,
    // So it is easier to use it with an EnableSessions object
    function areActionsEnabled(
        address account,
        PermissionId permissionId,
        ActionData[] calldata actions
    )
        external
        view
        returns (bool)
    {
        return $actionPolicies.areEnabled({
            permissionId: permissionId,
            smartAccount: account,
            actionPolicyDatas: actions
        });
    }

    function isUserOpPolicyEnabled(
        address account,
        PermissionId permissionId,
        address policy
    )
        external
        view
        returns (bool)
    {
        return $userOpPolicies.policyList[permissionId].contains(account, policy);
    }

    function isERC1271PolicyEnabled(
        address account,
        PermissionId permissionId,
        address policy
    )
        external
        view
        returns (bool)
    {
        return $erc1271Policies.policyList[permissionId].contains(account, policy);
    }

    // for action policies
    function isActionPolicyEnabled(
        address account,
        PermissionId permissionId,
        ActionId actionId,
        address policy
    )
        external
        view
        returns (bool)
    {
        return $actionPolicies.actionPolicies[actionId].policyList[permissionId].contains(account, policy);
    }

    // for actionIds
    function isActionIdEnabled(
        address account,
        PermissionId permissionId,
        ActionId actionId
    )
        external
        view
        returns (bool)
    {
        return $actionPolicies.enabledActionIds[permissionId].contains(account, ActionId.unwrap(actionId));
    }

    function isERC7739ContentEnabled(
        address account,
        PermissionId permissionId,
        bytes32 appDomainSeparator,
        string memory content
    )
        external
        view
        returns (bool)
    {
        return $enabledERC7739.enabledContentNames[permissionId][appDomainSeparator].contains(
            account, content.hashERC7739Content()
        );
    }

    function getUserOpPolicies(address account, PermissionId permissionId) external view returns (address[] memory) {
        return $userOpPolicies.policyList[permissionId].values(account);
    }

    function getERC1271Policies(address account, PermissionId permissionId) external view returns (address[] memory) {
        return $erc1271Policies.policyList[permissionId].values(account);
    }

    function getActionPolicies(
        address account,
        PermissionId permissionId,
        ActionId actionId
    )
        external
        view
        returns (address[] memory)
    {
        return $actionPolicies.actionPolicies[actionId].policyList[permissionId].values(account);
    }

    function getEnabledActions(address account, PermissionId permissionId) external view returns (bytes32[] memory) {
        return $actionPolicies.enabledActionIds[permissionId].values(account);
    }

    function getEnabledERC7739Content(
        address account,
        PermissionId permissionId
    )
        external
        view
        returns (ERC7739ContextHashes[] memory enabledERC7739ContentHashes)
    {
        uint256 length = $enabledERC7739.enabledDomainSeparators[permissionId].length(account);
        enabledERC7739ContentHashes = new ERC7739ContextHashes[](length);
        for (uint256 i; i < length; i++) {
            enabledERC7739ContentHashes[i].appDomainSeparator =
                $enabledERC7739.enabledDomainSeparators[permissionId].at(account, i);
            enabledERC7739ContentHashes[i].contentNameHashes = $enabledERC7739.enabledContentNames[permissionId][enabledERC7739ContentHashes[i]
                .appDomainSeparator].values(account);
        }
    }

    function getSessionValidatorAndConfig(
        address account,
        PermissionId permissionId
    )
        external
        view
        returns (address sessionValidator, bytes memory sessionValidatorData)
    {
        SignerConf storage $s = $sessionValidators[permissionId][account];
        sessionValidator = address($s.sessionValidator);
        sessionValidatorData = $s.config.load();
    }
}
