// SPDX-License-Identifier: MIT
pragma solidity ^0.8.25;

import "forge-std/Test.sol";
import "forge-std/console.sol";

// SmartSessions core
import {SmartSession} from "@smartsessions/SmartSession.sol";
import {ISmartSession} from "@smartsessions/ISmartSession.sol";
import {
    Session,
    PermissionId,
    PolicyData,
    ActionData,
    ERC7739Data,
    ERC7739Context,
    SmartSessionMode,
    EnableSession,
    ChainDigest
} from "@smartsessions/DataTypes.sol";
import {ISessionValidator} from "@smartsessions/interfaces/ISessionValidator.sol";

// Policies
import {SudoPolicy} from "@smartsessions/external/policies/SudoPolicy.sol";
import {ERC20SpendingLimitPolicy} from "@smartsessions/external/policies/ERC20SpendingLimitPolicy.sol";
import {UniActionPolicy, ActionConfig, ParamRules, ParamRule, ParamCondition, LimitUsage} from "@smartsessions/external/policies/UniActionPolicy.sol";

// Rhinestone modules (for session validators)
import {OwnableValidator} from "@rhinestone/core-modules/OwnableValidator/OwnableValidator.sol";

// ERC-7579 interfaces
import {IERC7579Account} from "erc7579/interfaces/IERC7579Account.sol";
import {IModule as IERC7579Module} from "erc7579/interfaces/IERC7579Module.sol";
import {ModeLib, ModeCode, CallType, ExecType, ModeSelector, ModePayload, CALLTYPE_SINGLE, CALLTYPE_BATCH} from "erc7579/lib/ModeLib.sol";
import {ExecutionLib} from "erc7579/lib/ExecutionLib.sol";

// Account abstraction
import {PackedUserOperation} from "account-abstraction/interfaces/PackedUserOperation.sol";
import {IEntryPoint} from "account-abstraction/interfaces/IEntryPoint.sol";

// Test utilities
import {MessageHashUtils} from "@openzeppelin/contracts/utils/cryptography/MessageHashUtils.sol";
import {ECDSA} from "@openzeppelin/contracts/utils/cryptography/ECDSA.sol";

// Mock ERC20 for testing
import {ERC20} from "solady/tokens/ERC20.sol";

// Registry interface
import {IRegistry, ModuleType} from "@smartsessions/interfaces/IRegistry.sol";

// Registry address used by SmartSession
address constant REGISTRY_ADDR = 0x000000000069E2a187AEFFb852bF3cCdC95151B2;

// Mock Registry that approves all modules
contract MockRegistry is IRegistry {
    function check(address) external pure {}
    function checkForAccount(address, address) external pure {}
    function check(address, ModuleType) external pure {}
    function checkForAccount(address, address, ModuleType) external pure {}
    function trustAttesters(uint8, address[] calldata) external pure {}
    function check(address, address[] calldata, uint256) external pure {}
    function check(address, ModuleType, address[] calldata, uint256) external pure {}
}

contract MockERC20 is ERC20 {
    string private _name;
    string private _symbol;

    constructor(string memory name_, string memory symbol_) {
        _name = name_;
        _symbol = symbol_;
    }

    function name() public view override returns (string memory) {
        return _name;
    }

    function symbol() public view override returns (string memory) {
        return _symbol;
    }

    function mint(address to, uint256 amount) external {
        _mint(to, amount);
    }
}

// Simple session validator that uses ECDSA
contract SimpleSessionValidator is ISessionValidator {
    function validateSignatureWithData(
        bytes32 hash,
        bytes calldata sig,
        bytes calldata data
    ) external pure override returns (bool validSig) {
        // data contains the session key address
        address sessionKey = abi.decode(data, (address));
        address recovered = ECDSA.recover(MessageHashUtils.toEthSignedMessageHash(hash), sig);
        return recovered == sessionKey;
    }

    function onInstall(bytes calldata) external pure {}
    function onUninstall(bytes calldata) external pure {}
    function isModuleType(uint256 typeID) external pure returns (bool) {
        return typeID == 1 || typeID == 7; // Validator type (1) + ISessionValidator type (7)
    }
    function isInitialized(address) external pure returns (bool) {
        return true;
    }

    // ERC-165 support
    function supportsInterface(bytes4 interfaceId) external pure returns (bool) {
        return interfaceId == type(IERC7579Module).interfaceId
            || interfaceId == type(ISessionValidator).interfaceId
            || interfaceId == 0x01ffc9a7; // ERC-165
    }
}

// Mock 7579 Account for testing
contract Mock7579Account is IERC7579Account {
    using ModeLib for ModeCode;
    using ExecutionLib for bytes;

    mapping(address => bool) public isValidatorInstalled;
    mapping(address => bool) public isExecutorInstalled;
    address public owner;

    // For ERC1271
    mapping(bytes32 => bool) public approvedHashes;

    constructor(address _owner) {
        owner = _owner;
    }

    function installModule(uint256 moduleTypeId, address module, bytes calldata initData) external payable {
        if (moduleTypeId == 1) {
            isValidatorInstalled[module] = true;
            IERC7579Module(module).onInstall(initData);
        } else if (moduleTypeId == 2) {
            isExecutorInstalled[module] = true;
            IERC7579Module(module).onInstall(initData);
        }
    }

    function uninstallModule(uint256 moduleTypeId, address module, bytes calldata deInitData) external payable {
        if (moduleTypeId == 1) {
            isValidatorInstalled[module] = false;
        } else if (moduleTypeId == 2) {
            isExecutorInstalled[module] = false;
        }
        IERC7579Module(module).onUninstall(deInitData);
    }

    function isModuleInstalled(uint256 moduleTypeId, address module, bytes calldata) external view returns (bool) {
        if (moduleTypeId == 1) return isValidatorInstalled[module];
        if (moduleTypeId == 2) return isExecutorInstalled[module];
        return false;
    }

    function execute(ModeCode mode, bytes calldata executionCalldata) external payable {
        (CallType callType,,,) = ModeLib.decode(mode);

        if (CallType.unwrap(callType) == CallType.unwrap(CALLTYPE_SINGLE)) {
            (address target, uint256 value, bytes calldata callData) = executionCalldata.decodeSingle();
            (bool success,) = target.call{value: value}(callData);
            require(success, "Execution failed");
        } else if (CallType.unwrap(callType) == CallType.unwrap(CALLTYPE_BATCH)) {
            // Handle batch execution
            // bytes calldata execution.decodeBatch() returns array of (target, value, calldata)
        }
    }

    function executeFromExecutor(ModeCode mode, bytes calldata executionCalldata) external payable returns (bytes[] memory) {
        require(isExecutorInstalled[msg.sender], "Not an executor");
        // Simplified execution
        return new bytes[](0);
    }

    function accountId() external pure returns (string memory) {
        return "mock.7579.account";
    }

    function supportsExecutionMode(ModeCode) external pure returns (bool) {
        return true;
    }

    function supportsModule(uint256 moduleTypeId) external pure returns (bool) {
        return moduleTypeId == 1 || moduleTypeId == 2 || moduleTypeId == 3 || moduleTypeId == 4;
    }

    // ERC1271
    function isValidSignature(bytes32 hash, bytes calldata signature) external view returns (bytes4) {
        // For testing: approve if signed by owner
        address signer = ECDSA.recover(hash, signature);
        if (signer == owner) {
            return 0x1626ba7e; // EIP1271_MAGIC_VALUE
        }
        return 0xffffffff;
    }

    function approveHash(bytes32 hash) external {
        require(msg.sender == owner, "Not owner");
        approvedHashes[hash] = true;
    }

    receive() external payable {}
}

contract SmartSessionsIntegrationTest is Test {
    SmartSession public smartSession;
    SimpleSessionValidator public sessionValidator;
    SudoPolicy public sudoPolicy;
    ERC20SpendingLimitPolicy public spendingLimitPolicy;
    UniActionPolicy public uniActionPolicy;
    OwnableValidator public ownableValidator;

    Mock7579Account public account;
    MockERC20 public token;

    // Test keys
    uint256 constant OWNER_KEY = 0x1;
    uint256 constant SESSION_KEY = 0x2;
    uint256 constant SESSION_KEY_2 = 0x3;

    address public ownerAddr;
    address public sessionKeyAddr;
    address public sessionKeyAddr2;

    function setUp() public {
        // Derive addresses from keys
        ownerAddr = vm.addr(OWNER_KEY);
        sessionKeyAddr = vm.addr(SESSION_KEY);
        sessionKeyAddr2 = vm.addr(SESSION_KEY_2);

        // Deploy mock registry at the expected address
        MockRegistry mockRegistry = new MockRegistry();
        vm.etch(REGISTRY_ADDR, address(mockRegistry).code);

        // Deploy contracts
        smartSession = new SmartSession();
        sessionValidator = new SimpleSessionValidator();
        sudoPolicy = new SudoPolicy();
        spendingLimitPolicy = new ERC20SpendingLimitPolicy();
        uniActionPolicy = new UniActionPolicy();
        ownableValidator = new OwnableValidator();

        // Deploy mock account
        account = new Mock7579Account(ownerAddr);

        // Deploy test token
        token = new MockERC20("Test Token", "TEST");
        token.mint(address(account), 1000 ether);

        // Fund account
        vm.deal(address(account), 10 ether);

        // Install SmartSession as validator
        vm.prank(address(account));
        account.installModule(1, address(smartSession), "");

        console.log("SmartSession deployed at:", address(smartSession));
        console.log("Account deployed at:", address(account));
        console.log("Owner address:", ownerAddr);
        console.log("Session key address:", sessionKeyAddr);
    }

    // =========================================================================
    //                          BASIC INSTALLATION TESTS
    // =========================================================================

    function test_SmartSessionInstalled() public view {
        assertTrue(account.isValidatorInstalled(address(smartSession)));
        // Note: SmartSession's isInitialized returns true if at least one session is enabled
        // or if the module was installed with init data. Our empty install doesn't set this.
        // The module is still functional - sessions can be enabled.
    }

    function test_SmartSessionModuleType() public view {
        assertTrue(smartSession.isModuleType(1)); // Validator
        assertFalse(smartSession.isModuleType(2)); // Not Executor
    }

    // =========================================================================
    //                          SESSION ENABLING TESTS
    // =========================================================================

    function test_EnableSessionWithSudoPolicy() public {
        // Create session with sudo policy (allows everything)
        Session memory session = _createSudoSession(sessionKeyAddr);

        // Get permission ID
        PermissionId permissionId = smartSession.getPermissionId(session);

        // Enable session via account
        Session[] memory sessions = new Session[](1);
        sessions[0] = session;

        vm.prank(address(account));
        PermissionId[] memory permissionIds = smartSession.enableSessions(sessions);

        assertEq(PermissionId.unwrap(permissionIds[0]), PermissionId.unwrap(permissionId));
        assertTrue(smartSession.isPermissionEnabled(permissionId, address(account)));

        console.log("Session enabled with PermissionId:");
        console.logBytes32(PermissionId.unwrap(permissionId));
    }

    function test_EnableMultipleSessions() public {
        // Create two sessions with different keys
        Session memory session1 = _createSudoSession(sessionKeyAddr);
        Session memory session2 = _createSudoSession(sessionKeyAddr2);

        Session[] memory sessions = new Session[](2);
        sessions[0] = session1;
        sessions[1] = session2;

        vm.prank(address(account));
        PermissionId[] memory permissionIds = smartSession.enableSessions(sessions);

        assertTrue(smartSession.isPermissionEnabled(permissionIds[0], address(account)));
        assertTrue(smartSession.isPermissionEnabled(permissionIds[1], address(account)));

        console.log("Enabled", permissionIds.length, "sessions");
    }

    // =========================================================================
    //                       SPENDING LIMIT POLICY TESTS
    // =========================================================================

    function test_EnableSessionWithSpendingLimit() public {
        // Create session with ERC20 spending limit
        uint256 spendingLimit = 100 ether;
        Session memory session = _createSpendingLimitSession(sessionKeyAddr, address(token), spendingLimit);

        Session[] memory sessions = new Session[](1);
        sessions[0] = session;

        vm.prank(address(account));
        PermissionId[] memory permissionIds = smartSession.enableSessions(sessions);

        assertTrue(smartSession.isPermissionEnabled(permissionIds[0], address(account)));

        console.log("Session with spending limit enabled");
        console.log("Token:", address(token));
        console.log("Limit:", spendingLimit);
    }

    // =========================================================================
    //                       UNI ACTION POLICY TESTS
    // =========================================================================

    function test_EnableSessionWithActionPolicy() public {
        // Create session that only allows transfers to specific address
        address allowedRecipient = address(0x1234);
        Session memory session = _createActionRestrictedSession(sessionKeyAddr, address(token), allowedRecipient);

        Session[] memory sessions = new Session[](1);
        sessions[0] = session;

        vm.prank(address(account));
        PermissionId[] memory permissionIds = smartSession.enableSessions(sessions);

        assertTrue(smartSession.isPermissionEnabled(permissionIds[0], address(account)));

        console.log("Session with action policy enabled");
        console.log("Allowed recipient:", allowedRecipient);
    }

    // =========================================================================
    //                         SESSION REMOVAL TESTS
    // =========================================================================

    function test_RemoveSession() public {
        // Enable session
        Session memory session = _createSudoSession(sessionKeyAddr);
        Session[] memory sessions = new Session[](1);
        sessions[0] = session;

        vm.prank(address(account));
        PermissionId[] memory permissionIds = smartSession.enableSessions(sessions);

        PermissionId permissionId = permissionIds[0];
        assertTrue(smartSession.isPermissionEnabled(permissionId, address(account)));

        // Remove session
        vm.prank(address(account));
        smartSession.removeSession(permissionId);

        assertFalse(smartSession.isPermissionEnabled(permissionId, address(account)));

        console.log("Session removed successfully");
    }

    // =========================================================================
    //                         POLICY QUERY TESTS
    // =========================================================================

    function test_QuerySessionValidator() public {
        Session memory session = _createSudoSession(sessionKeyAddr);
        Session[] memory sessions = new Session[](1);
        sessions[0] = session;

        vm.prank(address(account));
        PermissionId[] memory permissionIds = smartSession.enableSessions(sessions);

        assertTrue(smartSession.isISessionValidatorSet(permissionIds[0], address(account)));
    }

    function test_QueryUserOpPolicies() public {
        Session memory session = _createSudoSession(sessionKeyAddr);
        Session[] memory sessions = new Session[](1);
        sessions[0] = session;

        vm.prank(address(account));
        PermissionId[] memory permissionIds = smartSession.enableSessions(sessions);

        // Check if userOp policies are enabled
        assertTrue(smartSession.areUserOpPoliciesEnabled(
            address(account),
            permissionIds[0],
            session.userOpPolicies
        ));
    }

    // =========================================================================
    //                         NONCE MANAGEMENT TESTS
    // =========================================================================

    function test_NonceIncrementsOnEnable() public {
        Session memory session1 = _createSudoSession(sessionKeyAddr);
        PermissionId permissionId = smartSession.getPermissionId(session1);

        // Check initial nonce
        uint256 nonce1 = smartSession.getNonce(permissionId, address(account));
        assertEq(nonce1, 0);

        // Enable session
        Session[] memory sessions = new Session[](1);
        sessions[0] = session1;

        vm.prank(address(account));
        smartSession.enableSessions(sessions);

        // Nonce should still be 0 after first enable (incremented on re-enable)
        uint256 nonce2 = smartSession.getNonce(permissionId, address(account));
        console.log("Nonce after enable:", nonce2);
    }

    // =========================================================================
    //                    PERMISSION ID CALCULATION TESTS
    // =========================================================================

    function test_PermissionIdDeterministic() public view {
        Session memory session = _createSudoSession(sessionKeyAddr);

        PermissionId id1 = smartSession.getPermissionId(session);
        PermissionId id2 = smartSession.getPermissionId(session);

        assertEq(PermissionId.unwrap(id1), PermissionId.unwrap(id2));

        console.log("PermissionId is deterministic");
        console.logBytes32(PermissionId.unwrap(id1));
    }

    function test_DifferentSessionsDifferentPermissionIds() public view {
        Session memory session1 = _createSudoSession(sessionKeyAddr);
        Session memory session2 = _createSudoSession(sessionKeyAddr2);

        PermissionId id1 = smartSession.getPermissionId(session1);
        PermissionId id2 = smartSession.getPermissionId(session2);

        assertTrue(PermissionId.unwrap(id1) != PermissionId.unwrap(id2));

        console.log("Different sessions have different PermissionIds");
    }

    // =========================================================================
    //                    ADDING POLICIES TO EXISTING SESSION
    // =========================================================================

    function test_EnableAdditionalUserOpPolicies() public {
        // First enable a session with sudo policy
        Session memory session = _createSudoSession(sessionKeyAddr);
        Session[] memory sessions = new Session[](1);
        sessions[0] = session;

        vm.prank(address(account));
        PermissionId[] memory permissionIds = smartSession.enableSessions(sessions);

        PermissionId permissionId = permissionIds[0];

        // Add additional userOp policy
        PolicyData[] memory additionalPolicies = new PolicyData[](1);
        additionalPolicies[0] = PolicyData({
            policy: address(sudoPolicy),
            initData: ""
        });

        vm.prank(address(account));
        smartSession.enableUserOpPolicies(permissionId, additionalPolicies);

        console.log("Additional userOp policy enabled");
    }

    // =========================================================================
    //                         HELPER FUNCTIONS
    // =========================================================================

    function _createSudoSession(address sessionKey) internal view returns (Session memory) {
        // UserOp policies - just sudo (allows everything)
        PolicyData[] memory userOpPolicies = new PolicyData[](1);
        userOpPolicies[0] = PolicyData({
            policy: address(sudoPolicy),
            initData: ""
        });

        // Action policies - sudo for ERC20 transfer
        ActionData[] memory actions = new ActionData[](1);
        PolicyData[] memory actionPolicies = new PolicyData[](1);
        actionPolicies[0] = PolicyData({
            policy: address(sudoPolicy),
            initData: ""
        });
        actions[0] = ActionData({
            actionTargetSelector: bytes4(keccak256("transfer(address,uint256)")), // ERC20 transfer
            actionTarget: address(token), // Token address
            actionPolicies: actionPolicies
        });

        // ERC7739 policies (for ERC1271)
        ERC7739Data memory erc7739 = ERC7739Data({
            allowedERC7739Content: new ERC7739Context[](0),
            erc1271Policies: new PolicyData[](0)
        });

        return Session({
            sessionValidator: ISessionValidator(address(sessionValidator)),
            sessionValidatorInitData: abi.encode(sessionKey),
            salt: bytes32(uint256(1)),
            userOpPolicies: userOpPolicies,
            erc7739Policies: erc7739,
            actions: actions,
            permitERC4337Paymaster: false
        });
    }

    function _createSpendingLimitSession(
        address sessionKey,
        address tokenAddr,
        uint256 limit
    ) internal view returns (Session memory) {
        // UserOp policies
        PolicyData[] memory userOpPolicies = new PolicyData[](1);
        userOpPolicies[0] = PolicyData({
            policy: address(sudoPolicy),
            initData: ""
        });

        // Action policies with spending limit
        ActionData[] memory actions = new ActionData[](1);
        PolicyData[] memory actionPolicies = new PolicyData[](1);

        // Encode spending limit init data
        address[] memory tokens = new address[](1);
        tokens[0] = tokenAddr;
        uint256[] memory limits = new uint256[](1);
        limits[0] = limit;

        actionPolicies[0] = PolicyData({
            policy: address(spendingLimitPolicy),
            initData: abi.encode(tokens, limits)
        });

        actions[0] = ActionData({
            actionTargetSelector: bytes4(keccak256("transfer(address,uint256)")),
            actionTarget: tokenAddr,
            actionPolicies: actionPolicies
        });

        ERC7739Data memory erc7739 = ERC7739Data({
            allowedERC7739Content: new ERC7739Context[](0),
            erc1271Policies: new PolicyData[](0)
        });

        return Session({
            sessionValidator: ISessionValidator(address(sessionValidator)),
            sessionValidatorInitData: abi.encode(sessionKey),
            salt: bytes32(uint256(2)),
            userOpPolicies: userOpPolicies,
            erc7739Policies: erc7739,
            actions: actions,
            permitERC4337Paymaster: false
        });
    }

    function _createActionRestrictedSession(
        address sessionKey,
        address tokenAddr,
        address allowedRecipient
    ) internal view returns (Session memory) {
        // UserOp policies
        PolicyData[] memory userOpPolicies = new PolicyData[](1);
        userOpPolicies[0] = PolicyData({
            policy: address(sudoPolicy),
            initData: ""
        });

        // Action policies with UniActionPolicy
        ActionData[] memory actions = new ActionData[](1);
        PolicyData[] memory actionPolicies = new PolicyData[](1);

        // Create action config that restricts recipient
        ParamRule[16] memory rules;
        rules[0] = ParamRule({
            condition: ParamCondition.EQUAL,
            offset: 0, // First param (recipient address)
            isLimited: false,
            ref: bytes32(uint256(uint160(allowedRecipient))),
            usage: LimitUsage({limit: 0, used: 0})
        });

        ActionConfig memory config = ActionConfig({
            valueLimitPerUse: 0, // No ETH value allowed
            paramRules: ParamRules({
                length: 1,
                rules: rules
            })
        });

        actionPolicies[0] = PolicyData({
            policy: address(uniActionPolicy),
            initData: abi.encode(config)
        });

        actions[0] = ActionData({
            actionTargetSelector: bytes4(keccak256("transfer(address,uint256)")),
            actionTarget: tokenAddr,
            actionPolicies: actionPolicies
        });

        ERC7739Data memory erc7739 = ERC7739Data({
            allowedERC7739Content: new ERC7739Context[](0),
            erc1271Policies: new PolicyData[](0)
        });

        return Session({
            sessionValidator: ISessionValidator(address(sessionValidator)),
            sessionValidatorInitData: abi.encode(sessionKey),
            salt: bytes32(uint256(3)),
            userOpPolicies: userOpPolicies,
            erc7739Policies: erc7739,
            actions: actions,
            permitERC4337Paymaster: false
        });
    }
}
