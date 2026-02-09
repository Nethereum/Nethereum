// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "forge-std/Test.sol";
import "@openzeppelin/contracts/utils/cryptography/ECDSA.sol";
import "@openzeppelin/contracts/utils/cryptography/MessageHashUtils.sol";

// Nethereum core
import {NethereumAccount} from "@nethereum-aa/core/NethereumAccount.sol";
import {NethereumAccountFactory} from "@nethereum-aa/core/NethereumAccountFactory.sol";
import {MODULE_TYPE_VALIDATOR, MODULE_TYPE_EXECUTOR, MODULE_TYPE_HOOK} from "@nethereum-aa/interfaces/IERC7579Account.sol";
import {PackedUserOperation} from "@nethereum-aa/interfaces/PackedUserOperation.sol";
import {ModeLib, ModeCode} from "@nethereum-aa/lib/ModeLib.sol";
import {ExecutionLib} from "@nethereum-aa/lib/ExecutionLib.sol";

// Rhinestone Modules - these bring their own ERC7579 types
import {OwnableValidator} from "@nethereum-aa/modules/rhinestone/OwnableValidator.sol";
import {SocialRecovery} from "@nethereum-aa/modules/rhinestone/SocialRecovery.sol";
import {DeadmanSwitch} from "@nethereum-aa/modules/rhinestone/DeadmanSwitch.sol";
import {OwnableExecutor} from "@nethereum-aa/modules/rhinestone/OwnableExecutor.sol";

// =============================================================================
// MOCK ENTRYPOINT
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

    function simulateValidation(
        address account,
        PackedUserOperation calldata userOp,
        bytes32 userOpHash,
        uint256 missingAccountFunds
    ) external returns (uint256) {
        return NethereumAccount(payable(account)).validateUserOp(userOp, userOpHash, missingAccountFunds);
    }

    receive() external payable {}
}

// =============================================================================
// MOCK TARGET
// =============================================================================

contract MockTarget {
    uint256 public value;

    function setValue(uint256 _value) external payable {
        value = _value;
    }

    function getValue() external view returns (uint256) {
        return value;
    }

    receive() external payable {}
}

// =============================================================================
// MOCK REGISTRY (for ERC-7484)
// =============================================================================

contract MockRegistry {
    mapping(address => mapping(address => mapping(uint256 => bool))) public attestations;

    function check(address module, address attester) external view returns (uint256) {
        // Return 1 if attested, 0 otherwise
        return attestations[module][attester][1] ? 1 : 0;
    }

    function checkForAccount(
        address smartAccount,
        address module,
        uint256 moduleType
    ) external view {
        // No-op for testing - allows all modules
    }

    function attest(address module, address attester, uint256 moduleType) external {
        attestations[module][attester][moduleType] = true;
    }
}

// =============================================================================
// RHINESTONE MODULES INTEGRATION TESTS
// =============================================================================

contract RhinestoneModulesIntegrationTest is Test {
    using ECDSA for bytes32;
    using MessageHashUtils for bytes32;

    NethereumAccount public implementation;
    NethereumAccount public account;
    NethereumAccountFactory public factory;
    MockEntryPoint public entryPoint;
    MockTarget public target;
    MockRegistry public registry;

    // Rhinestone Validators
    OwnableValidator public ownableValidator;
    SocialRecovery public socialRecovery;
    DeadmanSwitch public deadmanSwitch;

    // Rhinestone Executors
    OwnableExecutor public ownableExecutor;

    // Test keys
    uint256 public ownerKey = 0x1234;
    address public owner;
    uint256 public owner2Key = 0x5678;
    address public owner2;
    uint256 public owner3Key = 0x9ABC;
    address public owner3;
    uint256 public guardian1Key = 0xCCCC;
    address public guardian1;
    uint256 public guardian2Key = 0xDDDD;
    address public guardian2;
    uint256 public nomineeKey = 0xEEEE;
    address public nominee;

    bytes32 public constant SALT = bytes32(uint256(1));

    function setUp() public {
        // Derive addresses from keys
        owner = vm.addr(ownerKey);
        owner2 = vm.addr(owner2Key);
        owner3 = vm.addr(owner3Key);
        guardian1 = vm.addr(guardian1Key);
        guardian2 = vm.addr(guardian2Key);
        nominee = vm.addr(nomineeKey);

        // Deploy infrastructure
        entryPoint = new MockEntryPoint();
        target = new MockTarget();
        registry = new MockRegistry();

        // Deploy Rhinestone modules
        ownableValidator = new OwnableValidator();
        socialRecovery = new SocialRecovery();
        deadmanSwitch = new DeadmanSwitch();
        ownableExecutor = new OwnableExecutor();
        // Note: HookMultiPlexer and RegistryHook require ERC-7484 registry in constructor
        // hookMultiplexer = new HookMultiPlexer(IERC7484(address(registry)));
        // registryHook = new RegistryHook(IERC7484(address(registry)));

        // Deploy factory
        factory = new NethereumAccountFactory(address(entryPoint));
    }

    // =========================================================================
    // HELPER FUNCTIONS
    // =========================================================================

    function _createAccountWithOwnableValidator(address[] memory owners, uint256 threshold) internal returns (NethereumAccount) {
        // Encode OwnableValidator init data: abi.encode(threshold, owners)
        bytes memory validatorInitData = abi.encode(threshold, owners);

        // Create account with OwnableValidator
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorInitData);
        address accountAddr = factory.createAccount(SALT, initData);

        return NethereumAccount(payable(accountAddr));
    }

    function _sortAddresses(address[] memory addrs) internal pure returns (address[] memory) {
        // Simple bubble sort for small arrays
        for (uint256 i = 0; i < addrs.length; i++) {
            for (uint256 j = i + 1; j < addrs.length; j++) {
                if (addrs[i] > addrs[j]) {
                    (addrs[i], addrs[j]) = (addrs[j], addrs[i]);
                }
            }
        }
        return addrs;
    }

    function _createUserOp(
        address sender,
        uint256 nonce,
        bytes memory callData
    ) internal pure returns (PackedUserOperation memory) {
        return PackedUserOperation({
            sender: sender,
            nonce: nonce,
            initCode: "",
            callData: callData,
            accountGasLimits: bytes32(uint256(100000) << 128 | uint256(100000)),
            preVerificationGas: 21000,
            gasFees: bytes32(uint256(1 gwei) << 128 | uint256(1 gwei)),
            paymasterAndData: "",
            signature: ""
        });
    }

    function _signUserOp(
        PackedUserOperation memory userOp,
        bytes32 userOpHash,
        uint256 privateKey
    ) internal pure returns (bytes memory) {
        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(userOpHash);
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(privateKey, ethHash);
        return abi.encodePacked(r, s, v);
    }

    // =========================================================================
    // OWNABLE VALIDATOR TESTS
    // =========================================================================

    function test_OwnableValidator_SingleOwner() public {
        // Create account with single owner
        address[] memory owners = new address[](1);
        owners[0] = owner;

        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        // Verify owner is set
        address[] memory storedOwners = ownableValidator.getOwners(address(account));
        assertEq(storedOwners.length, 1);
        assertEq(storedOwners[0], owner);
        assertEq(ownableValidator.threshold(address(account)), 1);
    }

    function test_OwnableValidator_2of3_Multisig() public {
        // Create sorted array of owners (required by OwnableValidator)
        address[] memory owners = new address[](3);
        owners[0] = owner;
        owners[1] = owner2;
        owners[2] = owner3;
        owners = _sortAddresses(owners);

        account = _createAccountWithOwnableValidator(owners, 2);
        vm.deal(address(account), 10 ether);

        // Verify configuration
        assertEq(ownableValidator.threshold(address(account)), 2);
        assertEq(ownableValidator.ownerCount(address(account)), 3);
    }

    function test_OwnableValidator_AddOwner() public {
        // Create account with single owner
        address[] memory owners = new address[](1);
        owners[0] = owner;

        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        // Add new owner via account execution
        bytes memory addOwnerCall = abi.encodeCall(OwnableValidator.addOwner, (owner2));

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(ownableValidator), 0, addOwnerCall);

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        // Verify new owner count
        assertEq(ownableValidator.ownerCount(address(account)), 2);
    }

    function test_OwnableValidator_SetThreshold() public {
        // Create account with 3 owners, threshold 1
        address[] memory owners = new address[](3);
        owners[0] = owner;
        owners[1] = owner2;
        owners[2] = owner3;
        owners = _sortAddresses(owners);

        account = _createAccountWithOwnableValidator(owners, 1);

        // Update threshold to 2
        bytes memory setThresholdCall = abi.encodeCall(OwnableValidator.setThreshold, (2));

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(ownableValidator), 0, setThresholdCall);

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        // Verify threshold updated
        assertEq(ownableValidator.threshold(address(account)), 2);
    }

    // =========================================================================
    // SOCIAL RECOVERY TESTS
    // =========================================================================

    function test_SocialRecovery_Setup() public {
        // First create account with OwnableValidator
        address[] memory owners = new address[](1);
        owners[0] = owner;
        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        // Setup guardians (sorted)
        address[] memory guardians = new address[](2);
        guardians[0] = guardian1;
        guardians[1] = guardian2;
        guardians = _sortAddresses(guardians);

        // Install SocialRecovery as additional validator
        bytes memory recoveryInitData = abi.encode(uint256(2), guardians); // threshold = 2

        vm.prank(address(entryPoint));
        account.installModule(
            MODULE_TYPE_VALIDATOR,
            address(socialRecovery),
            recoveryInitData
        );

        // Verify guardians are set
        assertEq(socialRecovery.threshold(address(account)), 2);
        assertEq(socialRecovery.guardianCount(address(account)), 2);
    }

    function test_SocialRecovery_AddGuardian() public {
        // Setup account with SocialRecovery
        address[] memory owners = new address[](1);
        owners[0] = owner;
        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        address[] memory guardians = new address[](1);
        guardians[0] = guardian1;

        bytes memory recoveryInitData = abi.encode(uint256(1), guardians);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(socialRecovery), recoveryInitData);

        // Add another guardian
        bytes memory addGuardianCall = abi.encodeCall(SocialRecovery.addGuardian, (guardian2));

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(socialRecovery), 0, addGuardianCall);

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        // Verify guardian added
        assertEq(socialRecovery.guardianCount(address(account)), 2);
    }

    // =========================================================================
    // DEADMAN SWITCH TESTS
    // =========================================================================

    function test_DeadmanSwitch_Setup() public {
        // Create account with OwnableValidator
        address[] memory owners = new address[](1);
        owners[0] = owner;
        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        // Install DeadmanSwitch with nominee and 30-day timeout
        uint48 timeout = 30 days;
        bytes memory dmsInitData = abi.encodePacked(nominee, timeout);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(deadmanSwitch), dmsInitData);

        // Verify configuration
        (uint48 lastAccess, uint48 storedTimeout, address storedNominee) = deadmanSwitch.config(address(account));
        assertEq(storedNominee, nominee);
        assertEq(storedTimeout, timeout);
        assertGt(lastAccess, 0);
    }

    function test_DeadmanSwitch_ActivityUpdatesLastAccess() public {
        // Setup
        address[] memory owners = new address[](1);
        owners[0] = owner;
        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        uint48 timeout = 30 days;
        bytes memory dmsInitData = abi.encodePacked(nominee, timeout);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(deadmanSwitch), dmsInitData);

        // Also install as hook to track activity
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_HOOK, address(deadmanSwitch), "");

        (uint48 initialLastAccess,,) = deadmanSwitch.config(address(account));

        // Simulate time passing
        vm.warp(block.timestamp + 1 days);

        // Execute a transaction (activity)
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(target), 0, abi.encodeCall(MockTarget.setValue, (42)));

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        // Verify lastAccess was updated
        (uint48 newLastAccess,,) = deadmanSwitch.config(address(account));
        assertGt(newLastAccess, initialLastAccess);
    }

    // =========================================================================
    // OWNABLE EXECUTOR TESTS
    // =========================================================================

    function test_OwnableExecutor_Setup() public {
        // Create account
        address[] memory owners = new address[](1);
        owners[0] = owner;
        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        // Install OwnableExecutor with owner as the executor owner
        bytes memory executorInitData = abi.encodePacked(owner);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_EXECUTOR, address(ownableExecutor), executorInitData);

        // Verify executor installed
        assertTrue(account.isModuleInstalled(MODULE_TYPE_EXECUTOR, address(ownableExecutor), ""));
    }

    function test_OwnableExecutor_ExecuteOnBehalf() public {
        // Setup account with OwnableExecutor
        address[] memory owners = new address[](1);
        owners[0] = owner;
        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        bytes memory executorInitData = abi.encodePacked(owner);
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_EXECUTOR, address(ownableExecutor), executorInitData);

        // Owner executes via OwnableExecutor
        // ExecutionLib.encodeSingle encodes (target, value, calldata)
        bytes memory execCallData = ExecutionLib.encodeSingle(
            address(target),
            0,
            abi.encodeCall(MockTarget.setValue, (123))
        );

        vm.prank(owner);
        ownableExecutor.executeOnOwnedAccount(address(account), execCallData);

        // Verify execution
        assertEq(target.value(), 123);
    }

    // =========================================================================
    // MODULE COMBINATION TESTS
    // =========================================================================

    function test_MultipleValidators_OwnableAndSocialRecovery() public {
        // Create account with OwnableValidator
        address[] memory owners = new address[](1);
        owners[0] = owner;
        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        // Add SocialRecovery as second validator
        address[] memory guardians = new address[](2);
        guardians[0] = guardian1;
        guardians[1] = guardian2;
        guardians = _sortAddresses(guardians);

        bytes memory recoveryInitData = abi.encode(uint256(2), guardians);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(socialRecovery), recoveryInitData);

        // Verify both validators installed
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(ownableValidator), ""));
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(socialRecovery), ""));
    }

    function test_ValidatorAndHook_OwnableWithDeadmanSwitch() public {
        // Create account with OwnableValidator
        address[] memory owners = new address[](1);
        owners[0] = owner;
        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        // Install DeadmanSwitch as validator
        uint48 timeout = 30 days;
        bytes memory dmsValidatorData = abi.encodePacked(nominee, timeout);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(deadmanSwitch), dmsValidatorData);

        // Install DeadmanSwitch as hook (dual role)
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_HOOK, address(deadmanSwitch), "");

        // Verify both module types installed
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(deadmanSwitch), ""));
        assertTrue(account.isModuleInstalled(MODULE_TYPE_HOOK, address(deadmanSwitch), ""));
    }

    // =========================================================================
    // EDGE CASES AND SECURITY TESTS
    // =========================================================================

    function test_OwnableValidator_RevertOnDuplicateOwners() public {
        // Try to create account with duplicate owners
        address[] memory owners = new address[](2);
        owners[0] = owner;
        owners[1] = owner; // duplicate

        bytes memory validatorInitData = abi.encode(uint256(1), owners);
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorInitData);

        // Should revert because owners are not unique
        vm.expectRevert(); // OwnableValidator.NotSortedAndUnique
        factory.createAccount(SALT, initData);
    }

    function test_OwnableValidator_RevertOnZeroThreshold() public {
        address[] memory owners = new address[](1);
        owners[0] = owner;

        bytes memory validatorInitData = abi.encode(uint256(0), owners); // zero threshold
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorInitData);

        vm.expectRevert(); // OwnableValidator.ThresholdNotSet
        factory.createAccount(SALT, initData);
    }

    function test_OwnableValidator_RevertOnThresholdExceedsOwners() public {
        address[] memory owners = new address[](1);
        owners[0] = owner;

        bytes memory validatorInitData = abi.encode(uint256(2), owners); // threshold > owners
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorInitData);

        vm.expectRevert(); // OwnableValidator.InvalidThreshold
        factory.createAccount(SALT, initData);
    }

    function test_SocialRecovery_CannotAddZeroGuardian() public {
        // Setup account
        address[] memory owners = new address[](1);
        owners[0] = owner;
        account = _createAccountWithOwnableValidator(owners, 1);
        vm.deal(address(account), 10 ether);

        address[] memory guardians = new address[](1);
        guardians[0] = guardian1;

        bytes memory recoveryInitData = abi.encode(uint256(1), guardians);
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(socialRecovery), recoveryInitData);

        // Try to add zero address as guardian
        bytes memory addGuardianCall = abi.encodeCall(SocialRecovery.addGuardian, (address(0)));

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(socialRecovery), 0, addGuardianCall);

        vm.prank(address(entryPoint));
        vm.expectRevert(); // SocialRecovery.InvalidGuardian
        account.execute(mode, execData);
    }
}
