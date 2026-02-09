// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import "../DataTypes.sol";

library IdLib {
    bytes4 internal constant VALUE_SELECTOR = 0xFFFFFFFF;

    function toUserOpPolicyId(PermissionId permissionId) internal pure returns (UserOpPolicyId userOpPolicyId) {
        userOpPolicyId = UserOpPolicyId.wrap(PermissionId.unwrap(permissionId));
    }

    function toActionId(address target, bytes calldata callData) internal pure returns (ActionId actionId) {
        if (callData.length < 4) return toActionId(target, VALUE_SELECTOR);
        else return toActionId(target, callData[:4]);
    }

    function toActionId(address target, bytes4 functionSelector) internal pure returns (ActionId actionId) {
        actionId = ActionId.wrap(keccak256(abi.encodePacked(target, functionSelector)));
    }

    function toActionPolicyId(
        PermissionId permissionId,
        ActionId actionId
    )
        internal
        pure
        returns (ActionPolicyId policyId)
    {
        policyId = ActionPolicyId.wrap(keccak256(abi.encodePacked(permissionId, actionId)));
    }

    function toErc1271PolicyId(PermissionId permissionId) internal pure returns (Erc1271PolicyId erc1271PolicyId) {
        erc1271PolicyId = Erc1271PolicyId.wrap(keccak256(abi.encodePacked("ERC1271: ", permissionId)));
    }

    function toConfigId(UserOpPolicyId userOpPolicyId, address account) internal pure returns (ConfigId _id) {
        _id = ConfigId.wrap(keccak256(abi.encodePacked(account, userOpPolicyId)));
    }

    function toConfigId(ActionPolicyId actionPolicyId, address account) internal pure returns (ConfigId _id) {
        _id = ConfigId.wrap(keccak256(abi.encodePacked(account, actionPolicyId)));
    }

    function toConfigId(
        PermissionId permissionId,
        ActionId actionId,
        address account
    )
        internal
        pure
        returns (ConfigId _id)
    {
        _id = toConfigId(toActionPolicyId(permissionId, actionId), account);
    }

    function toConfigId(Erc1271PolicyId erc1271PolicyId, address account) internal pure returns (ConfigId _id) {
        _id = ConfigId.wrap(keccak256(abi.encodePacked(account, erc1271PolicyId)));
    }

    function toConfigId(UserOpPolicyId userOpPolicyId) internal view returns (ConfigId _id) {
        _id = toConfigId(userOpPolicyId, msg.sender);
    }

    function toConfigId(PermissionId permissionId, ActionId actionId) internal view returns (ConfigId _id) {
        _id = toConfigId(toActionPolicyId(permissionId, actionId), msg.sender);
    }

    function toConfigId(Erc1271PolicyId erc1271PolicyId) internal view returns (ConfigId _id) {
        _id = toConfigId(erc1271PolicyId, msg.sender);
    }

    function toPermissionIdMemory(Session memory session) internal pure returns (PermissionId permissionId) {
        permissionId = PermissionId.wrap(
            keccak256(abi.encode(session.sessionValidator, session.sessionValidatorInitData, session.salt))
        );
    }

    function toPermissionId(Session calldata session) internal pure returns (PermissionId permissionId) {
        permissionId = PermissionId.wrap(
            keccak256(abi.encode(session.sessionValidator, session.sessionValidatorInitData, session.salt))
        );
    }
}
