// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "forge-std/Test.sol";
import "@nethereum-aa/core/NethereumAccount.sol";
import "@nethereum-aa/core/NethereumAccountFactory.sol";
import "@nethereum-aa/core/AccountStorage.sol";
import "@nethereum-aa/interfaces/IERC7579Account.sol";
import "@nethereum-aa/interfaces/IERC7579Module.sol";
import "@nethereum-aa/interfaces/PackedUserOperation.sol";
import "@nethereum-aa/lib/ModeLib.sol";
import "@nethereum-aa/lib/ExecutionLib.sol";
import "@openzeppelin/contracts/utils/cryptography/ECDSA.sol";
import "@openzeppelin/contracts/utils/cryptography/MessageHashUtils.sol";
import "@openzeppelin/contracts/proxy/ERC1967/ERC1967Proxy.sol";

// =============================================================================
// MOCK CONTRACTS
// =============================================================================

contract MockEntryPoint {
    mapping(address => uint256) public balances;
    mapping(address => mapping(uint192 => uint256)) public nonces;

    function depositTo(address account) external payable {
        balances[account] += msg.value;
    }

    function withdrawTo(address payable to, uint256 amount) external {
        balances[msg.sender] -= amount;
        to.transfer(amount);
    }

    function balanceOf(address account) external view returns (uint256) {
        return balances[account];
    }

    function getNonce(address sender, uint192 key) external view returns (uint256) {
        return nonces[sender][key];
    }

    function incrementNonce(address sender, uint192 key) external {
        nonces[sender][key]++;
    }

    // Simulate EntryPoint calling validateUserOp
    function simulateValidation(
        address account,
        PackedUserOperation calldata userOp,
        bytes32 userOpHash,
        uint256 missingFunds
    ) external returns (uint256) {
        return NethereumAccount(payable(account)).validateUserOp(userOp, userOpHash, missingFunds);
    }

    // Simulate EntryPoint calling execute
    function simulateExecution(
        address account,
        ModeCode mode,
        bytes calldata executionCalldata
    ) external payable {
        NethereumAccount(payable(account)).execute{value: msg.value}(mode, executionCalldata);
    }

    receive() external payable {}
}

contract MockValidator is IValidator {
    mapping(address => address) public owners;
    bool public shouldFail;
    uint256 public validationResult;

    function setOwner(address account, address owner) external {
        owners[account] = owner;
    }

    function setShouldFail(bool _fail) external {
        shouldFail = _fail;
    }

    function setValidationResult(uint256 result) external {
        validationResult = result;
    }

    function onInstall(bytes calldata data) external override {
        if (data.length >= 20) {
            owners[msg.sender] = address(bytes20(data[:20]));
        }
    }

    function onUninstall(bytes calldata) external override {
        delete owners[msg.sender];
    }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == MODULE_TYPE_VALIDATOR;
    }

    function isInitialized(address smartAccount) external view override returns (bool) {
        return owners[smartAccount] != address(0);
    }

    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) external view override returns (uint256) {
        if (shouldFail) return 1;
        if (validationResult != 0) return validationResult;

        address owner = owners[userOp.sender];
        if (owner == address(0)) return 1;

        // Verify signature
        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(userOpHash);
        address recovered = ECDSA.recover(ethHash, userOp.signature);

        return recovered == owner ? 0 : 1;
    }

    function isValidSignatureWithSender(
        address,
        bytes32 hash,
        bytes calldata signature
    ) external view override returns (bytes4) {
        if (shouldFail) return ERC1271_INVALID;

        address owner = owners[msg.sender];
        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(hash);
        address recovered = ECDSA.recover(ethHash, signature);

        return recovered == owner ? ERC1271_VALID : ERC1271_INVALID;
    }
}

contract MockExecutor is IExecutor {
    bool public installed;
    address public lastAccount;

    function onInstall(bytes calldata) external override {
        installed = true;
        lastAccount = msg.sender;
    }

    function onUninstall(bytes calldata) external override {
        installed = false;
    }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == MODULE_TYPE_EXECUTOR;
    }

    function isInitialized(address) external view override returns (bool) {
        return installed;
    }

    // Execute via the account
    function executeViaAccount(
        address account,
        ModeCode mode,
        bytes calldata executionCalldata
    ) external returns (bytes[] memory) {
        return IERC7579Execution(account).executeFromExecutor(mode, executionCalldata);
    }
}

contract MockHook is IHook {
    bool public preCheckCalled;
    bool public postCheckCalled;
    bool public shouldRevert;
    bytes public lastPreCheckData;

    function setShouldRevert(bool _revert) external {
        shouldRevert = _revert;
    }

    function onInstall(bytes calldata) external override {}

    function onUninstall(bytes calldata) external override {}

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == MODULE_TYPE_HOOK;
    }

    function isInitialized(address) external pure override returns (bool) {
        return true;
    }

    function preCheck(
        address msgSender,
        uint256 msgValue,
        bytes calldata msgData
    ) external override returns (bytes memory hookData) {
        if (shouldRevert) revert("Hook blocked");
        preCheckCalled = true;
        lastPreCheckData = msgData;
        return abi.encode(msgSender, msgValue);
    }

    function postCheck(bytes calldata hookData) external override {
        if (shouldRevert) revert("Hook blocked post");
        postCheckCalled = true;
        (hookData); // silence warning
    }

    function reset() external {
        preCheckCalled = false;
        postCheckCalled = false;
    }
}

contract MockFallbackHandler is IFallback {
    bool public called;
    address public lastSender;
    bytes public lastData;

    function onInstall(bytes calldata) external override {}

    function onUninstall(bytes calldata) external override {}

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == MODULE_TYPE_FALLBACK;
    }

    function isInitialized(address) external pure override returns (bool) {
        return true;
    }

    // Custom function that account will route to
    function customFunction(uint256 value) external returns (uint256) {
        called = true;
        // Extract original sender from calldata (ERC-2771)
        lastSender = _msgSender();
        lastData = msg.data;
        return value * 2;
    }

    function _msgSender() internal pure returns (address sender) {
        // ERC-2771: Last 20 bytes of calldata is the original sender
        assembly {
            sender := shr(96, calldataload(sub(calldatasize(), 20)))
        }
    }
}

contract MockMaliciousHook is IHook {
    bool public blockUninstall;

    function setBlockUninstall(bool _block) external {
        blockUninstall = _block;
    }

    function onInstall(bytes calldata) external override {}

    function onUninstall(bytes calldata) external override {
        if (blockUninstall) revert("Cannot uninstall me!");
    }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == MODULE_TYPE_HOOK;
    }

    function isInitialized(address) external pure override returns (bool) {
        return true;
    }

    function preCheck(address, uint256, bytes calldata) external pure override returns (bytes memory) {
        return "";
    }

    function postCheck(bytes calldata) external pure override {}
}

contract MockTarget {
    uint256 public value;
    bool public shouldRevert;

    function setValue(uint256 _value) external payable {
        if (shouldRevert) revert("Target reverted");
        value = _value;
    }

    function getValue() external view returns (uint256) {
        return value;
    }

    function setShouldRevert(bool _revert) external {
        shouldRevert = _revert;
    }

    receive() external payable {}
}

contract ReentrantAttacker {
    NethereumAccount public target;
    uint256 public attackCount;

    function setTarget(address _target) external {
        target = NethereumAccount(payable(_target));
    }

    function attack() external {
        // Try to re-enter during execution
        attackCount++;
        if (attackCount < 3) {
            ModeCode mode = ModeLib.encodeSimpleSingle();
            bytes memory execData = ExecutionLib.encodeSingle(address(this), 0, "");
            target.execute(mode, execData);
        }
    }

    receive() external payable {
        this.attack();
    }
}

// =============================================================================
// TEST CONTRACT
// =============================================================================

contract NethereumAccountTest is Test {
    using ECDSA for bytes32;
    using MessageHashUtils for bytes32;

    NethereumAccount public implementation;
    NethereumAccount public account;
    NethereumAccountFactory public factory;
    NethereumAccountFactoryMinimal public factoryMinimal;
    MockEntryPoint public entryPoint;
    MockValidator public validator;
    MockValidator public validator2;
    MockExecutor public executor;
    MockHook public hook;
    MockFallbackHandler public fallbackHandler;
    MockTarget public target;

    address public owner;
    uint256 public ownerKey;
    address public user;
    uint256 public userKey;

    bytes32 constant SALT = bytes32(uint256(1));

    function setUp() public {
        // Create keys
        ownerKey = 0xA11CE;
        owner = vm.addr(ownerKey);
        userKey = 0xB0B;
        user = vm.addr(userKey);

        // Deploy mock EntryPoint
        entryPoint = new MockEntryPoint();

        // Deploy modules
        validator = new MockValidator();
        validator2 = new MockValidator();
        executor = new MockExecutor();
        hook = new MockHook();
        fallbackHandler = new MockFallbackHandler();
        target = new MockTarget();

        // Deploy factory
        factory = new NethereumAccountFactory(address(entryPoint));
        factoryMinimal = new NethereumAccountFactoryMinimal(address(entryPoint));

        // Create account via factory
        // initData format: validator(20 bytes) + validatorInitData(owner as 20 bytes)
        bytes memory initData = abi.encodePacked(address(validator), owner);
        address accountAddr = factory.createAccount(SALT, initData);
        account = NethereumAccount(payable(accountAddr));

        // Fund account
        vm.deal(address(account), 100 ether);
    }

    // =========================================================================
    // INITIALIZATION TESTS
    // =========================================================================

    function test_Initialization_ValidatorInstalled() public view {
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(validator), ""));
    }

    function test_Initialization_OwnerSetInValidator() public view {
        assertEq(validator.owners(address(account)), owner);
    }

    function test_Initialization_CannotReinitialize() public {
        bytes memory initData = abi.encodePacked(address(validator), abi.encode(user));

        vm.expectRevert();
        account.initializeAccount(initData);
    }

    function test_Initialization_AccountId() public view {
        assertEq(account.accountId(), "nethereum.account.1.0.0");
    }

    function test_Initialization_SupportsAllModuleTypes() public view {
        assertTrue(account.supportsModule(MODULE_TYPE_VALIDATOR));
        assertTrue(account.supportsModule(MODULE_TYPE_EXECUTOR));
        assertTrue(account.supportsModule(MODULE_TYPE_FALLBACK));
        assertTrue(account.supportsModule(MODULE_TYPE_HOOK));
        assertFalse(account.supportsModule(5));
    }

    // =========================================================================
    // ERC-4337 VALIDATEUSEROP TESTS
    // =========================================================================

    function test_ValidateUserOp_OnlyEntryPoint() public {
        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));

        vm.prank(user);
        vm.expectRevert(NethereumAccount.OnlyEntryPoint.selector);
        account.validateUserOp(userOp, userOpHash, 0);
    }

    function test_ValidateUserOp_ValidSignature() public {
        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));

        // Sign with owner
        bytes memory signature = _signUserOp(ownerKey, userOpHash);
        userOp.signature = abi.encodePacked(address(validator), signature);

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 0, "Validation should succeed");
    }

    function test_ValidateUserOp_InvalidSignature() public {
        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));

        // Sign with wrong key
        bytes memory signature = _signUserOp(userKey, userOpHash);
        userOp.signature = abi.encodePacked(address(validator), signature);

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Validation should fail");
    }

    function test_ValidateUserOp_UninstalledValidator() public {
        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));

        // Use uninstalled validator
        userOp.signature = abi.encodePacked(address(validator2), _signUserOp(ownerKey, userOpHash));

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Should fail for uninstalled validator");
    }

    function test_ValidateUserOp_SignatureTooShort() public {
        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));

        // Signature shorter than 20 bytes (no validator address)
        userOp.signature = hex"1234";

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Should fail for short signature");
    }

    function test_ValidateUserOp_PaysPrefund() public {
        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        userOp.signature = abi.encodePacked(address(validator), _signUserOp(ownerKey, userOpHash));

        uint256 prefund = 1 ether;
        uint256 accountBalanceBefore = address(account).balance;
        uint256 entryPointBalanceBefore = address(entryPoint).balance;

        entryPoint.simulateValidation(address(account), userOp, userOpHash, prefund);

        assertEq(address(account).balance, accountBalanceBefore - prefund);
        assertEq(address(entryPoint).balance, entryPointBalanceBefore + prefund);
    }

    function test_ValidateUserOp_ReturnsValidationData() public {
        // Test time-bounded validation
        uint48 validAfter = uint48(block.timestamp + 100);
        uint48 validUntil = uint48(block.timestamp + 200);
        uint256 expectedData = uint256(validAfter) << 208 | uint256(validUntil) << 160;

        validator.setValidationResult(expectedData);

        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        userOp.signature = abi.encodePacked(address(validator), _signUserOp(ownerKey, userOpHash));

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, expectedData);

        validator.setValidationResult(0); // Reset
    }

    // =========================================================================
    // ERC-7579 EXECUTION TESTS
    // =========================================================================

    function test_Execute_SingleCall() public {
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(target),
            0,
            abi.encodeCall(MockTarget.setValue, (42))
        );

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        assertEq(target.value(), 42);
    }

    function test_Execute_SingleCallWithValue() public {
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(target),
            1 ether,
            abi.encodeCall(MockTarget.setValue, (100))
        );

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        assertEq(target.value(), 100);
        assertEq(address(target).balance, 1 ether);
    }

    function test_Execute_BatchCalls() public {
        MockTarget target2 = new MockTarget();

        Execution[] memory executions = new Execution[](2);
        executions[0] = Execution(address(target), 0, abi.encodeCall(MockTarget.setValue, (10)));
        executions[1] = Execution(address(target2), 0, abi.encodeCall(MockTarget.setValue, (20)));

        ModeCode mode = ModeLib.encodeSimpleBatch();
        bytes memory execData = ExecutionLib.encodeBatch(executions);

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        assertEq(target.value(), 10);
        assertEq(target2.value(), 20);
    }

    function test_Execute_TryMode_ContinuesOnFailure() public {
        target.setShouldRevert(true);
        MockTarget target2 = new MockTarget();

        Execution[] memory executions = new Execution[](2);
        executions[0] = Execution(address(target), 0, abi.encodeCall(MockTarget.setValue, (10)));
        executions[1] = Execution(address(target2), 0, abi.encodeCall(MockTarget.setValue, (20)));

        ModeCode mode = ModeLib.encode(CALLTYPE_BATCH, EXECTYPE_TRY, MODE_DEFAULT, PAYLOAD_DEFAULT);
        bytes memory execData = ExecutionLib.encodeBatch(executions);

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        // First call failed, second succeeded
        assertEq(target.value(), 0);
        assertEq(target2.value(), 20);
    }

    function test_Execute_DefaultMode_RevertsOnFailure() public {
        target.setShouldRevert(true);

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(target),
            0,
            abi.encodeCall(MockTarget.setValue, (42))
        );

        vm.prank(address(entryPoint));
        vm.expectRevert("Target reverted");
        account.execute(mode, execData);
    }

    function test_Execute_OnlyEntryPointOrSelf() public {
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(target), 0, "");

        vm.prank(user);
        vm.expectRevert(NethereumAccount.OnlyEntryPointOrSelf.selector);
        account.execute(mode, execData);
    }

    function test_Execute_SelfCallAllowed() public {
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(target),
            0,
            abi.encodeCall(MockTarget.setValue, (99))
        );

        // Account calls itself
        vm.prank(address(account));
        account.execute(mode, execData);

        assertEq(target.value(), 99);
    }

    function test_ExecuteFromExecutor_OnlyExecutorModule() public {
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(target), 0, "");

        vm.prank(user);
        vm.expectRevert(NethereumAccount.OnlyExecutorModule.selector);
        account.executeFromExecutor(mode, execData);
    }

    function test_ExecuteFromExecutor_InstalledExecutorWorks() public {
        // Install executor
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_EXECUTOR, address(executor), "");

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(target),
            0,
            abi.encodeCall(MockTarget.setValue, (123))
        );

        executor.executeViaAccount(address(account), mode, execData);

        assertEq(target.value(), 123);
    }

    function test_Execute_UnsupportedMode() public {
        // Create invalid mode (invalid CallType)
        ModeCode invalidMode = ModeCode.wrap(bytes32(uint256(0x99) << 248));

        vm.prank(address(entryPoint));
        vm.expectRevert();
        account.execute(invalidMode, "");
    }

    // =========================================================================
    // MODULE MANAGEMENT TESTS
    // =========================================================================

    function test_InstallModule_Validator() public {
        vm.prank(address(entryPoint));
        // validatorInitData is raw owner address (20 bytes)
        account.installModule(MODULE_TYPE_VALIDATOR, address(validator2), abi.encodePacked(user));

        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(validator2), ""));
        assertEq(validator2.owners(address(account)), user);
    }

    function test_InstallModule_Executor() public {
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_EXECUTOR, address(executor), "");

        assertTrue(account.isModuleInstalled(MODULE_TYPE_EXECUTOR, address(executor), ""));
        assertTrue(executor.installed());
    }

    function test_InstallModule_Hook() public {
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_HOOK, address(hook), "");

        assertTrue(account.isModuleInstalled(MODULE_TYPE_HOOK, address(hook), ""));
    }

    function test_InstallModule_Fallback() public {
        bytes4 selector = MockFallbackHandler.customFunction.selector;
        bytes memory initData = abi.encodePacked(selector, uint8(0)); // CallType.CALL

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_FALLBACK, address(fallbackHandler), initData);

        assertTrue(account.isModuleInstalled(MODULE_TYPE_FALLBACK, address(fallbackHandler), abi.encodePacked(selector)));
    }

    function test_InstallModule_OnlyEntryPointOrSelf() public {
        vm.prank(user);
        vm.expectRevert(NethereumAccount.OnlyEntryPointOrSelf.selector);
        account.installModule(MODULE_TYPE_VALIDATOR, address(validator2), "");
    }

    function test_UninstallModule_Validator() public {
        // First install second validator
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(validator2), abi.encode(user));

        // Now uninstall first validator
        vm.prank(address(entryPoint));
        account.uninstallModule(MODULE_TYPE_VALIDATOR, address(validator), "");

        assertFalse(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(validator), ""));
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(validator2), ""));
    }

    function test_UninstallModule_CannotRemoveLastValidator() public {
        vm.prank(address(entryPoint));
        vm.expectRevert(ModuleManager.CannotRemoveLastValidator.selector);
        account.uninstallModule(MODULE_TYPE_VALIDATOR, address(validator), "");
    }

    function test_InstallModule_ForbiddenSelector_OnInstall() public {
        bytes4 selector = IModule.onInstall.selector;
        bytes memory initData = abi.encodePacked(selector, uint8(0));

        vm.prank(address(entryPoint));
        vm.expectRevert(abi.encodeWithSelector(ModuleManager.ForbiddenSelector.selector, selector));
        account.installModule(MODULE_TYPE_FALLBACK, address(fallbackHandler), initData);
    }

    function test_InstallModule_ForbiddenSelector_OnUninstall() public {
        bytes4 selector = IModule.onUninstall.selector;
        bytes memory initData = abi.encodePacked(selector, uint8(0));

        vm.prank(address(entryPoint));
        vm.expectRevert(abi.encodeWithSelector(ModuleManager.ForbiddenSelector.selector, selector));
        account.installModule(MODULE_TYPE_FALLBACK, address(fallbackHandler), initData);
    }

    function test_InstallModule_HookAlreadyInstalled() public {
        MockHook hook2 = new MockHook();

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_HOOK, address(hook), "");

        vm.prank(address(entryPoint));
        vm.expectRevert(abi.encodeWithSelector(ModuleManager.HookAlreadyInstalled.selector, address(hook)));
        account.installModule(MODULE_TYPE_HOOK, address(hook2), "");
    }

    // =========================================================================
    // HOOK TESTS
    // =========================================================================

    function test_Hook_PreCheckCalled() public {
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_HOOK, address(hook), "");

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(target),
            0,
            abi.encodeCall(MockTarget.setValue, (42))
        );

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        assertTrue(hook.preCheckCalled());
        assertTrue(hook.postCheckCalled());
    }

    function test_Hook_BlocksExecution() public {
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_HOOK, address(hook), "");

        hook.setShouldRevert(true);

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(target), 0, "");

        vm.prank(address(entryPoint));
        vm.expectRevert("Hook blocked");
        account.execute(mode, execData);
    }

    function test_Hook_EmergencyUninstall() public {
        MockMaliciousHook maliciousHook = new MockMaliciousHook();
        maliciousHook.setBlockUninstall(true);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_HOOK, address(maliciousHook), "");

        // Normal uninstall fails
        vm.prank(address(entryPoint));
        vm.expectRevert("Cannot uninstall me!");
        account.uninstallModule(MODULE_TYPE_HOOK, address(maliciousHook), "");

        // Still installed
        assertTrue(account.isModuleInstalled(MODULE_TYPE_HOOK, address(maliciousHook), ""));
    }

    // =========================================================================
    // FALLBACK HANDLER TESTS
    // =========================================================================

    function test_Fallback_RoutesToHandler() public {
        bytes4 selector = MockFallbackHandler.customFunction.selector;
        bytes memory initData = abi.encodePacked(selector, uint8(0)); // CallType.CALL

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_FALLBACK, address(fallbackHandler), initData);

        // Call custom function via account
        (bool success, bytes memory result) = address(account).call(
            abi.encodeCall(MockFallbackHandler.customFunction, (21))
        );

        assertTrue(success);
        assertEq(abi.decode(result, (uint256)), 42); // 21 * 2
        assertTrue(fallbackHandler.called());
    }

    function test_Fallback_ERC2771Context() public {
        bytes4 selector = MockFallbackHandler.customFunction.selector;
        bytes memory initData = abi.encodePacked(selector, uint8(0));

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_FALLBACK, address(fallbackHandler), initData);

        // Call from specific address
        vm.prank(user);
        (bool success,) = address(account).call(
            abi.encodeCall(MockFallbackHandler.customFunction, (1))
        );

        assertTrue(success);
        // Handler should see user as the original sender (ERC-2771)
        assertEq(fallbackHandler.lastSender(), user);
    }

    function test_Fallback_NotInstalled() public {
        bytes4 unknownSelector = bytes4(keccak256("unknownFunction()"));

        vm.expectRevert(abi.encodeWithSelector(NethereumAccount.FallbackHandlerNotInstalled.selector, unknownSelector));
        (bool success,) = address(account).call(abi.encodeWithSelector(unknownSelector));
        (success); // silence warning
    }

    // =========================================================================
    // ERC-1271 SIGNATURE VALIDATION TESTS
    // =========================================================================

    function test_IsValidSignature_Valid() public {
        bytes32 hash = keccak256("test message");
        bytes memory signature = _signMessage(ownerKey, hash);

        // Prepend validator address
        bytes memory fullSig = abi.encodePacked(address(validator), signature);

        bytes4 result = account.isValidSignature(hash, fullSig);
        assertEq(result, ERC1271_VALID);
    }

    function test_IsValidSignature_Invalid() public {
        bytes32 hash = keccak256("test message");
        bytes memory signature = _signMessage(userKey, hash); // Wrong key

        bytes memory fullSig = abi.encodePacked(address(validator), signature);

        bytes4 result = account.isValidSignature(hash, fullSig);
        assertEq(result, ERC1271_INVALID);
    }

    function test_IsValidSignature_UninstalledValidator() public {
        bytes32 hash = keccak256("test message");
        bytes memory signature = _signMessage(ownerKey, hash);

        // Use uninstalled validator2
        bytes memory fullSig = abi.encodePacked(address(validator2), signature);

        bytes4 result = account.isValidSignature(hash, fullSig);
        assertEq(result, ERC1271_INVALID);
    }

    function test_IsValidSignature_ShortSignature() public {
        bytes32 hash = keccak256("test message");

        // Signature too short (no validator prefix)
        bytes4 result = account.isValidSignature(hash, hex"1234");
        assertEq(result, ERC1271_INVALID);
    }

    // =========================================================================
    // DEPOSIT MANAGEMENT TESTS
    // =========================================================================

    function test_AddDeposit() public {
        uint256 depositAmount = 1 ether;

        account.addDeposit{value: depositAmount}();

        assertEq(account.getDeposit(), depositAmount);
    }

    function test_WithdrawDeposit() public {
        // First deposit
        account.addDeposit{value: 2 ether}();

        uint256 userBalanceBefore = user.balance;

        vm.prank(address(entryPoint));
        account.withdrawDepositTo(payable(user), 1 ether);

        assertEq(user.balance, userBalanceBefore + 1 ether);
        assertEq(account.getDeposit(), 1 ether);
    }

    function test_WithdrawDeposit_OnlyEntryPointOrSelf() public {
        account.addDeposit{value: 1 ether}();

        vm.prank(user);
        vm.expectRevert(NethereumAccount.OnlyEntryPointOrSelf.selector);
        account.withdrawDepositTo(payable(user), 1 ether);
    }

    // =========================================================================
    // FACTORY TESTS
    // =========================================================================

    function test_Factory_DeterministicAddress() public {
        bytes memory initData = abi.encodePacked(address(validator), abi.encode(user));

        address predicted = factory.getAddress(bytes32(uint256(2)), initData);
        address created = factory.createAccount(bytes32(uint256(2)), initData);

        assertEq(predicted, created);
    }

    function test_Factory_Idempotent() public {
        bytes memory initData = abi.encodePacked(address(validator), abi.encode(user));
        bytes32 salt = bytes32(uint256(3));

        address first = factory.createAccount(salt, initData);
        address second = factory.createAccount(salt, initData);

        assertEq(first, second);
    }

    function test_Factory_IsDeployed() public {
        bytes memory initData = abi.encodePacked(address(validator), abi.encode(user));
        bytes32 salt = bytes32(uint256(4));

        assertFalse(factory.isDeployed(salt, initData));

        factory.createAccount(salt, initData);

        assertTrue(factory.isDeployed(salt, initData));
    }

    function test_Factory_GetInitCode() public view {
        bytes memory initData = abi.encodePacked(address(validator), abi.encode(user));
        bytes memory initCode = factory.getInitCode(bytes32(uint256(5)), initData);

        // First 20 bytes should be factory address
        address factoryAddr;
        assembly {
            factoryAddr := mload(add(initCode, 20))
        }
        assertEq(factoryAddr, address(factory));
    }

    function test_FactoryMinimal_CreateAccount() public {
        bytes memory initData = abi.encodePacked(address(validator), abi.encode(user));

        address accountAddr = factoryMinimal.createAccount(bytes32(uint256(10)), initData);

        NethereumAccount minimalAccount = NethereumAccount(payable(accountAddr));
        assertTrue(minimalAccount.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(validator), ""));
    }

    // =========================================================================
    // SECURITY TESTS
    // =========================================================================

    function test_Security_ReentrancyViaExecution() public {
        ReentrantAttacker attacker = new ReentrantAttacker();
        attacker.setTarget(address(account));

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(attacker),
            1 ether,
            ""
        );

        // This should not allow reentrancy
        vm.prank(address(entryPoint));
        vm.expectRevert(); // Attacker trying to call execute should fail
        account.execute(mode, execData);
    }

    function test_Security_ValidatorMustBeInstalled() public {
        // Create a malicious "validator" that always returns success
        MockValidator maliciousValidator = new MockValidator();

        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));

        // Try to use malicious validator (not installed)
        userOp.signature = abi.encodePacked(address(maliciousValidator), _signUserOp(ownerKey, userOpHash));

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Uninstalled validator should fail");
    }

    function test_Security_ExecutorMustBeInstalled() public {
        MockExecutor fakeExecutor = new MockExecutor();

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(target), 0, "");

        vm.prank(address(fakeExecutor));
        vm.expectRevert(NethereumAccount.OnlyExecutorModule.selector);
        account.executeFromExecutor(mode, execData);
    }

    function test_Security_CannotInstallZeroAddress() public {
        vm.prank(address(entryPoint));
        vm.expectRevert(abi.encodeWithSelector(ModuleManager.InvalidModule.selector, address(0)));
        account.installModule(MODULE_TYPE_VALIDATOR, address(0), "");
    }

    function test_Security_CannotInstallSentinel() public {
        address sentinel = address(0x1);

        vm.prank(address(entryPoint));
        vm.expectRevert(abi.encodeWithSelector(ModuleManager.InvalidModule.selector, sentinel));
        account.installModule(MODULE_TYPE_VALIDATOR, sentinel, "");
    }

    function test_Security_DoubleInstallValidator() public {
        // Already installed in setUp
        vm.prank(address(entryPoint));
        vm.expectRevert(); // SentinelList will revert
        account.installModule(MODULE_TYPE_VALIDATOR, address(validator), abi.encode(user));
    }

    function test_Security_CanReceiveETH() public {
        uint256 balanceBefore = address(account).balance;

        (bool success,) = address(account).call{value: 1 ether}("");
        assertTrue(success);

        assertEq(address(account).balance, balanceBefore + 1 ether);
    }

    // =========================================================================
    // UPGRADE TESTS
    // =========================================================================

    function test_Upgrade_OnlyEntryPointOrSelf() public {
        address newImpl = address(new NethereumAccount(address(entryPoint)));

        vm.prank(user);
        vm.expectRevert();
        account.upgradeToAndCall(newImpl, "");
    }

    function test_Upgrade_ViaEntryPoint() public {
        NethereumAccount newImpl = new NethereumAccount(address(entryPoint));

        // Get initial implementation slot
        bytes32 implSlot = bytes32(uint256(keccak256("eip1967.proxy.implementation")) - 1);
        address oldImpl = address(uint160(uint256(vm.load(address(account), implSlot))));

        vm.prank(address(entryPoint));
        account.upgradeToAndCall(address(newImpl), "");

        address currentImpl = address(uint160(uint256(vm.load(address(account), implSlot))));
        assertEq(currentImpl, address(newImpl));
        assertTrue(currentImpl != oldImpl);
    }

    function test_Upgrade_StoragePreserved() public {
        // Install additional modules before upgrade
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_EXECUTOR, address(executor), "");

        NethereumAccount newImpl = new NethereumAccount(address(entryPoint));

        vm.prank(address(entryPoint));
        account.upgradeToAndCall(address(newImpl), "");

        // Modules should still be installed
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(validator), ""));
        assertTrue(account.isModuleInstalled(MODULE_TYPE_EXECUTOR, address(executor), ""));
    }

    // =========================================================================
    // EXECUTION MODE SUPPORT TESTS
    // =========================================================================

    function test_SupportsExecutionMode_SingleDefault() public view {
        ModeCode mode = ModeLib.encodeSimpleSingle();
        assertTrue(account.supportsExecutionMode(mode));
    }

    function test_SupportsExecutionMode_BatchDefault() public view {
        ModeCode mode = ModeLib.encodeSimpleBatch();
        assertTrue(account.supportsExecutionMode(mode));
    }

    function test_SupportsExecutionMode_SingleTry() public view {
        ModeCode mode = ModeLib.encode(CALLTYPE_SINGLE, EXECTYPE_TRY, MODE_DEFAULT, PAYLOAD_DEFAULT);
        assertTrue(account.supportsExecutionMode(mode));
    }

    function test_SupportsExecutionMode_Delegatecall() public view {
        ModeCode mode = ModeLib.encode(CALLTYPE_DELEGATECALL, EXECTYPE_DEFAULT, MODE_DEFAULT, PAYLOAD_DEFAULT);
        assertTrue(account.supportsExecutionMode(mode));
    }

    function test_SupportsExecutionMode_InvalidCallType() public view {
        // 0x99 is not a valid CallType
        ModeCode mode = ModeCode.wrap(bytes32(uint256(0x99) << 248));
        assertFalse(account.supportsExecutionMode(mode));
    }

    // =========================================================================
    // PAGINATION TESTS
    // =========================================================================

    function test_GetValidatorsPaginated() public {
        // Install more validators
        MockValidator v2 = new MockValidator();
        MockValidator v3 = new MockValidator();

        vm.startPrank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(v2), abi.encode(user));
        account.installModule(MODULE_TYPE_VALIDATOR, address(v3), abi.encode(user));
        vm.stopPrank();

        (address[] memory validators, address next) = account.getValidatorsPaginated(address(0x1), 10);

        assertEq(validators.length, 3);
        assertTrue(next == address(0x1) || next == address(0)); // Sentinel or end
    }

    // =========================================================================
    // HELPER FUNCTIONS
    // =========================================================================

    function _createUserOp(address sender) internal pure returns (PackedUserOperation memory) {
        return PackedUserOperation({
            sender: sender,
            nonce: 0,
            initCode: "",
            callData: "",
            accountGasLimits: bytes32(0),
            preVerificationGas: 0,
            gasFees: bytes32(0),
            paymasterAndData: "",
            signature: ""
        });
    }

    function _signUserOp(uint256 privateKey, bytes32 userOpHash) internal pure returns (bytes memory) {
        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(userOpHash);
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(privateKey, ethHash);
        return abi.encodePacked(r, s, v);
    }

    function _signMessage(uint256 privateKey, bytes32 hash) internal pure returns (bytes memory) {
        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(hash);
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(privateKey, ethHash);
        return abi.encodePacked(r, s, v);
    }
}
