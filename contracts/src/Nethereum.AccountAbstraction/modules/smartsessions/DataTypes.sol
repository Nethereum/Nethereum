// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import "./utils/AssociatedArrayLib.sol";
import { IRegistry, ModuleType } from "./interfaces/IRegistry.sol";
import "./interfaces/ISessionValidator.sol";
import { EnumerableSet } from "./utils/EnumerableSet4337.sol";
import { EnumerableMap } from "./utils/EnumerableMap4337.sol";
import { FlatBytesLib } from "flatbytes/BytesLib.sol";

/*´:°•.°+.*•´.*:˚.°*.˚•´.°:°•.°•.*•´.*:˚.°*.˚•´.°:°•.°+.*•´.*:*/
/*                       Parameters                           */
/*.•°:°.´+˚.*°.˚:*.´•*.+°.•°:´*.´•*.•°.•°:°.´:•˚°.*°.˚:*.´+°.•*/

struct EnableSession {
    uint8 chainDigestIndex;
    ChainDigest[] hashesAndChainIds;
    Session sessionToEnable;
    // in order to enable a session, the smart account has to sign a digest. The signature for this is stored here.
    bytes permissionEnableSig;
}

struct ChainDigest {
    uint64 chainId;
    bytes32 sessionDigest;
}

/**
 *
 * Represents a Session structure with various attributes for managing user operations and policies.
 *
 * Attributes:
 *     sessionValidator (ISessionValidator): The validator contract for signing user operations.
 *         Every userOp must be signed by the session key "owner". The signature is validated
 *         via a stateless external contract (ISessionValidator) that can implement different
 *         means of validation.
 *
 *     sessionValidatorInitData (bytes): Initialization data for the ISessionValidator contract.
 *         The ISessionValidator contract can be configured with different parameters that are
 *         passed in this field.
 *
 *     salt (bytes32): A unique identifier to prevent collision between sessions.
 *         A session key owner can have multiple sessions with the same parameters. To facilitate
 *         this, a salt is necessary to avoid collision.
 *
 *     userOpPolicies (PolicyData[]): An array of policy data for user operations.
 *         When every session can have multiple policies set.
 *
 *     erc7739Policies (ERC7739Data): ERC1271 Policies specific to the ERC7739 standard.
 *
 *     actions (ActionData[]): An array of action data for specifying function-specific policies.
 *         A common use case of session keys is to scope access to a specific target and function
 *         selector. SmartSession calls this "Action". With ActionData, we can specify policies
 *         that are only run if a 7579 execution contains a specific action.
 */
struct Session {
    ISessionValidator sessionValidator;
    bytes sessionValidatorInitData;
    bytes32 salt;
    PolicyData[] userOpPolicies;
    ERC7739Data erc7739Policies;
    ActionData[] actions;
    bool permitERC4337Paymaster;
}

struct MultiChainSession {
    ChainSession[] sessionsAndChainIds;
}

struct ChainSession {
    uint64 chainId;
    Session session;
}

// Policy data is a struct that contains the policy address and the initialization data for the policy.
struct PolicyData {
    address policy;
    bytes initData;
}

// Action data is a struct that contains the actionId and the policies that are associated with this action.
struct ActionData {
    bytes4 actionTargetSelector;
    address actionTarget;
    PolicyData[] actionPolicies;
}

struct ERC7739Context {
    // we can not use a detailed EIP712Domain struct here.
    // EIP712 specifies: Protocol designers only need to include the fields that make sense for their signing domain.
    // Unused fields are left out of the struct type.
    bytes32 appDomainSeparator;
    string[] contentNames;
}

struct EIP712Domain {
    string name;
    string version;
    uint256 chainId;
    address verifyingContract;
}

struct ERC7739Data {
    ERC7739Context[] allowedERC7739Content;
    PolicyData[] erc1271Policies;
}

enum SmartSessionMode {
    USE,
    ENABLE,
    UNSAFE_ENABLE
}

struct ERC7739ContextHashes {
    bytes32 appDomainSeparator;
    bytes32[] contentNameHashes;
}

/*´:°•.°+.*•´.*:˚.°*.˚•´.°:°•.°•.*•´.*:˚.°*.˚•´.°:°•.°+.*•´.*:*/
/*                         Storage                            */
/*.•°:°.´+˚.*°.˚:*.´•*.+°.•°:´*.´•*.•°.•°:°.´:•˚°.*°.˚:*.´+°.•*/

struct SignerConf {
    ISessionValidator sessionValidator;
    FlatBytesLib.Bytes config; // using FlatBytes to get around storage slot limitations
}

struct Policy {
    mapping(PermissionId => EnumerableSet.AddressSet) policyList;
}

struct EnumerableActionPolicy {
    mapping(ActionId => Policy) actionPolicies;
    mapping(PermissionId => EnumerableSet.Bytes32Set) enabledActionIds;
}

struct EnumerableERC7739Config {
    mapping(PermissionId => mapping(bytes32 appDomainSeparator => EnumerableSet.Bytes32Set)) enabledContentNames;
    mapping(PermissionId => EnumerableSet.Bytes32Set) enabledDomainSeparators;
}

// struct EnumerableERC7739Config {
//     mapping(PermissionId => EnumerableMap.Bytes32ToBytes32Map) erc1271Policies;
// }
// mapping(PermissionId => EnumerableSet.Bytes32Set) enabledDomainSeparators;

/*´:°•.°+.*•´.*:˚.°*.˚•´.°:°•.°•.*•´.*:˚.°*.˚•´.°:°•.°+.*•´.*:*/
/*                 Custom Types & Constants                   */
/*.•°:°.´+˚.*°.˚:*.´•*.+°.•°:´*.´•*.•°.•°:°.´:•˚°.*°.˚:*.´+°.•*/

type PermissionId is bytes32;

type ActionId is bytes32;

type ActionPolicyId is bytes32;

type UserOpPolicyId is bytes32;

type Erc1271PolicyId is bytes32;

type ConfigId is bytes32;

type ValidationData is uint256;

ActionId constant EMPTY_ACTIONID = ActionId.wrap(bytes32(0));
PermissionId constant EMPTY_PERMISSIONID = PermissionId.wrap(bytes32(0));
UserOpPolicyId constant EMPTY_USEROPPOLICYID = UserOpPolicyId.wrap(bytes32(0));
ActionPolicyId constant EMPTY_ACTIONPOLICYID = ActionPolicyId.wrap(bytes32(0));
Erc1271PolicyId constant EMPTY_ERC1271POLICYID = Erc1271PolicyId.wrap(bytes32(0));
ConfigId constant EMPTY_CONFIGID = ConfigId.wrap(bytes32(0));

ValidationData constant ERC4337_VALIDATION_SUCCESS = ValidationData.wrap(0);
ValidationData constant ERC4337_VALIDATION_FAILED = ValidationData.wrap(1);
bytes4 constant EIP1271_SUCCESS = 0x1626ba7e;
bytes4 constant EIP1271_FAILED = 0xFFFFFFFF;

uint256 constant ERC7579_MODULE_TYPE_VALIDATOR = 1;
uint256 constant ERC7579_MODULE_TYPE_EXECUTOR = 2;
uint256 constant ERC7579_MODULE_TYPE_FALLBACK = 3;
uint256 constant ERC7579_MODULE_TYPE_HOOK = 4;
uint256 constant ERC7579_MODULE_TYPE_STATELESS_VALIDATOR = 7;

enum PolicyType {
    NA,
    USER_OP,
    ACTION,
    ERC1271
}

IRegistry constant registry = IRegistry(0x000000000069E2a187AEFFb852bF3cCdC95151B2);
ModuleType constant VALIDATOR_MODULE_TYPE = ModuleType.wrap(ERC7579_MODULE_TYPE_VALIDATOR);

// ActionId for a fallback action policy. This id will be used if both action
// target and selector are set to 1. During validation if the current target and
// selector does not have a set action policy, then the fallback will be used if
// enabled.
address constant FALLBACK_TARGET_FLAG = address(1);
bytes4 constant FALLBACK_TARGET_SELECTOR_FLAG = 0x00000001;
bytes4 constant FALLBACK_TARGET_SELECTOR_FLAG_PERMITTED_TO_CALL_SMARTSESSION = 0x00000002;
// keccak256(abi.encodePacked(FALLBACK_TARGET_FLAG, FALLBACK_TARGET_SELECTOR_FLAG))
ActionId constant FALLBACK_ACTIONID = ActionId.wrap(0xd884b6afa19f8ace90a388daca691e4e28f20cdac5aeefd46ad8bd1c074d28cf);

// keccak256(abi.encodePacked(FALLBACK_TARGET_FLAG, FALLBACK_TARGET_SELECTOR_FLAG_PERMITTED_TO_CALL_SMARTSESSION))
ActionId constant FALLBACK_ACTIONID_SMARTSESSION_CALL =
    ActionId.wrap(0x986126569d6396d837d7adeb3ca726199afaf83546f38726e6f331bb92d8e9d6);

// A unique ValidationData value to retry a policy check with the FALLBACK_ACTIONID.
ValidationData constant RETRY_WITH_FALLBACK = ValidationData.wrap(uint256(0x50FFBAAD));

using { validationDataEq as == } for ValidationData global;
using { validationDataNeq as != } for ValidationData global;

function validationDataEq(ValidationData uid1, ValidationData uid2) pure returns (bool) {
    return ValidationData.unwrap(uid1) == ValidationData.unwrap(uid2);
}

function validationDataNeq(ValidationData uid1, ValidationData uid2) pure returns (bool) {
    return ValidationData.unwrap(uid1) != ValidationData.unwrap(uid2);
}

using { permissionIdEq as == } for PermissionId global;
using { permissionIdNeq as != } for PermissionId global;

function permissionIdEq(PermissionId uid1, PermissionId uid2) pure returns (bool) {
    return PermissionId.unwrap(uid1) == PermissionId.unwrap(uid2);
}

function permissionIdNeq(PermissionId uid1, PermissionId uid2) pure returns (bool) {
    return PermissionId.unwrap(uid1) != PermissionId.unwrap(uid2);
}

// ActionId
using { actionIdEq as == } for ActionId global;
using { actionIdNeq as != } for ActionId global;

function actionIdEq(ActionId id1, ActionId id2) pure returns (bool) {
    return ActionId.unwrap(id1) == ActionId.unwrap(id2);
}

function actionIdNeq(ActionId id1, ActionId id2) pure returns (bool) {
    return ActionId.unwrap(id1) != ActionId.unwrap(id2);
}

// UserOpPolicyId
using { userOpPolicyIdEq as == } for UserOpPolicyId global;
using { userOpPolicyIdNeq as != } for UserOpPolicyId global;

function userOpPolicyIdEq(UserOpPolicyId id1, UserOpPolicyId id2) pure returns (bool) {
    return UserOpPolicyId.unwrap(id1) == UserOpPolicyId.unwrap(id2);
}

function userOpPolicyIdNeq(UserOpPolicyId id1, UserOpPolicyId id2) pure returns (bool) {
    return UserOpPolicyId.unwrap(id1) != UserOpPolicyId.unwrap(id2);
}

// ActionPolicyId
using { actionPolicyIdEq as == } for ActionPolicyId global;
using { actionPolicyIdNeq as != } for ActionPolicyId global;

function actionPolicyIdEq(ActionPolicyId id1, ActionPolicyId id2) pure returns (bool) {
    return ActionPolicyId.unwrap(id1) == ActionPolicyId.unwrap(id2);
}

function actionPolicyIdNeq(ActionPolicyId id1, ActionPolicyId id2) pure returns (bool) {
    return ActionPolicyId.unwrap(id1) != ActionPolicyId.unwrap(id2);
}

// Erc1271PolicyId
using { erc1271PolicyIdEq as == } for Erc1271PolicyId global;
using { erc1271PolicyIdNeq as != } for Erc1271PolicyId global;

function erc1271PolicyIdEq(Erc1271PolicyId id1, Erc1271PolicyId id2) pure returns (bool) {
    return Erc1271PolicyId.unwrap(id1) == Erc1271PolicyId.unwrap(id2);
}

function erc1271PolicyIdNeq(Erc1271PolicyId id1, Erc1271PolicyId id2) pure returns (bool) {
    return Erc1271PolicyId.unwrap(id1) != Erc1271PolicyId.unwrap(id2);
}

// ConfigId
using { configIdEq as == } for ConfigId global;
using { configIdNeq as != } for ConfigId global;

function configIdEq(ConfigId id1, ConfigId id2) pure returns (bool) {
    return ConfigId.unwrap(id1) == ConfigId.unwrap(id2);
}

function configIdNeq(ConfigId id1, ConfigId id2) pure returns (bool) {
    return ConfigId.unwrap(id1) != ConfigId.unwrap(id2);
}
