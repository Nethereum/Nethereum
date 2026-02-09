// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

/**
 * ============================================================================
 * RHINESTONE ERC-7579 MODULES - INTEGRATION EXAMPLES
 * ============================================================================
 *
 * This file demonstrates how to use the standard Rhinestone ERC-7579 modules
 * with NethereumAccount. These modules are the ecosystem standard used by:
 * - Safe (via Safe7579 adapter)
 * - Biconomy Nexus
 * - ZeroDev Kernel v3
 *
 * Available Modules:
 * ==================
 *
 * VALIDATORS (TYPE 1):
 * - OwnableValidator: M-of-N threshold multisig with sorted signature ordering
 * - SocialRecovery: Guardian-based account recovery
 * - DeadmanSwitch: Inactivity-based account recovery (also works as Hook)
 *
 * EXECUTORS (TYPE 2):
 * - OwnableExecutor: Delegated execution by designated owners
 *
 * HOOKS (TYPE 4):
 * - DeadmanSwitch: Activity tracking (dual role with validator)
 * - HookMultiPlexer: Combine multiple hooks
 * - RegistryHook: ERC-7484 module registry validation
 *
 * ============================================================================
 */

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

// Rhinestone Modules
import {OwnableValidator} from "@nethereum-aa/modules/rhinestone/OwnableValidator.sol";
import {SocialRecovery} from "@nethereum-aa/modules/rhinestone/SocialRecovery.sol";
import {DeadmanSwitch} from "@nethereum-aa/modules/rhinestone/DeadmanSwitch.sol";
import {OwnableExecutor} from "@nethereum-aa/modules/rhinestone/OwnableExecutor.sol";

// =============================================================================
// MOCK CONTRACTS
// =============================================================================

contract MockEntryPoint {
    mapping(address => uint256) public balances;
    mapping(address => mapping(uint192 => uint256)) public nonces;

    function depositTo(address account) external payable {
        balances[account] += msg.value;
    }

    function balanceOf(address account) external view returns (uint256) {
        return balances[account];
    }

    function getNonce(address sender, uint192 key) external view returns (uint256) {
        return nonces[sender][key];
    }

    receive() external payable {}
}

contract MockTarget {
    uint256 public value;
    address public lastCaller;

    function setValue(uint256 _value) external payable {
        value = _value;
        lastCaller = msg.sender;
    }

    receive() external payable {}
}

// =============================================================================
// EXAMPLE TEST CONTRACT
// =============================================================================

contract RhinestoneModulesExamplesTest is Test {
    using ECDSA for bytes32;
    using MessageHashUtils for bytes32;

    NethereumAccountFactory public factory;
    MockEntryPoint public entryPoint;
    MockTarget public target;

    // Rhinestone Modules (deployed once, shared across accounts)
    OwnableValidator public ownableValidator;
    SocialRecovery public socialRecovery;
    DeadmanSwitch public deadmanSwitch;
    OwnableExecutor public ownableExecutor;

    // Test keys - these simulate real private keys
    uint256 constant ALICE_KEY = 0xA11CE;
    uint256 constant BOB_KEY = 0xB0B;
    uint256 constant CHARLIE_KEY = 0xC4A7;
    uint256 constant GUARDIAN1_KEY = 0x6111;
    uint256 constant GUARDIAN2_KEY = 0x6222;
    uint256 constant GUARDIAN3_KEY = 0x6333;
    uint256 constant NOMINEE_KEY = 0x70111;

    address public alice;
    address public bob;
    address public charlie;
    address public guardian1;
    address public guardian2;
    address public guardian3;
    address public nominee;

    function setUp() public {
        // Derive addresses from keys
        alice = vm.addr(ALICE_KEY);
        bob = vm.addr(BOB_KEY);
        charlie = vm.addr(CHARLIE_KEY);
        guardian1 = vm.addr(GUARDIAN1_KEY);
        guardian2 = vm.addr(GUARDIAN2_KEY);
        guardian3 = vm.addr(GUARDIAN3_KEY);
        nominee = vm.addr(NOMINEE_KEY);

        // Deploy infrastructure (shared across all examples)
        entryPoint = new MockEntryPoint();
        factory = new NethereumAccountFactory(address(entryPoint));
        target = new MockTarget();

        // Deploy Rhinestone modules (deployed once, used by many accounts)
        ownableValidator = new OwnableValidator();
        socialRecovery = new SocialRecovery();
        deadmanSwitch = new DeadmanSwitch();
        ownableExecutor = new OwnableExecutor();
    }

    // =========================================================================
    // HELPER FUNCTIONS
    // =========================================================================

    function _sortAddresses(address[] memory addrs) internal pure returns (address[] memory) {
        for (uint256 i = 0; i < addrs.length; i++) {
            for (uint256 j = i + 1; j < addrs.length; j++) {
                if (addrs[i] > addrs[j]) {
                    (addrs[i], addrs[j]) = (addrs[j], addrs[i]);
                }
            }
        }
        return addrs;
    }

    // =========================================================================
    // EXAMPLE 1: Single Owner Account
    // =========================================================================
    // Most basic setup - one owner controls the account

    function test_Example_SingleOwnerAccount() public {
        // Step 1: Prepare owner list (must be sorted even for single owner)
        address[] memory owners = new address[](1);
        owners[0] = alice;

        // Step 2: Encode validator init data
        // OwnableValidator expects: abi.encode(threshold, owners)
        uint256 threshold = 1;
        bytes memory validatorData = abi.encode(threshold, owners);

        // Step 3: Create account via factory
        // Factory expects: abi.encodePacked(validatorAddress, initData)
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorData);
        bytes32 salt = keccak256("single-owner-example");
        address accountAddr = factory.createAccount(salt, initData);
        NethereumAccount account = NethereumAccount(payable(accountAddr));

        // Fund the account
        vm.deal(accountAddr, 10 ether);

        // Verify: Alice is the owner
        address[] memory storedOwners = ownableValidator.getOwners(accountAddr);
        assertEq(storedOwners.length, 1);
        assertEq(storedOwners[0], alice);

        // Verify: Can execute via EntryPoint
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

    // =========================================================================
    // EXAMPLE 2: 2-of-3 Multisig Account
    // =========================================================================
    // Corporate/team account requiring multiple signatures

    function test_Example_MultisigAccount() public {
        // Step 1: Prepare sorted owner list
        address[] memory owners = new address[](3);
        owners[0] = alice;
        owners[1] = bob;
        owners[2] = charlie;
        owners = _sortAddresses(owners);

        // Step 2: Create 2-of-3 multisig
        uint256 threshold = 2;
        bytes memory validatorData = abi.encode(threshold, owners);
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorData);

        bytes32 salt = keccak256("multisig-example");
        address accountAddr = factory.createAccount(salt, initData);
        NethereumAccount account = NethereumAccount(payable(accountAddr));

        vm.deal(accountAddr, 10 ether);

        // Verify configuration
        assertEq(ownableValidator.threshold(accountAddr), 2);
        assertEq(ownableValidator.ownerCount(accountAddr), 3);

        // Dynamic threshold adjustment (requires 2 signatures to approve)
        // In production, this would be part of a UserOperation
        bytes memory setThresholdCall = abi.encodeCall(OwnableValidator.setThreshold, (3));
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(ownableValidator),
            0,
            setThresholdCall
        );

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        // Threshold changed to 3-of-3
        assertEq(ownableValidator.threshold(accountAddr), 3);
    }

    // =========================================================================
    // EXAMPLE 3: Account with Social Recovery
    // =========================================================================
    // Personal account with guardian recovery capability

    function test_Example_SocialRecoveryAccount() public {
        // Step 1: Create account with single owner
        address[] memory owners = new address[](1);
        owners[0] = alice;

        bytes memory validatorData = abi.encode(uint256(1), owners);
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorData);

        bytes32 salt = keccak256("social-recovery-example");
        address accountAddr = factory.createAccount(salt, initData);
        NethereumAccount account = NethereumAccount(payable(accountAddr));

        vm.deal(accountAddr, 10 ether);

        // Step 2: Setup guardians (sorted list required)
        address[] memory guardians = new address[](3);
        guardians[0] = guardian1;
        guardians[1] = guardian2;
        guardians[2] = guardian3;
        guardians = _sortAddresses(guardians);

        // Step 3: Install SocialRecovery as additional validator
        // 2-of-3 guardians required for recovery
        uint256 recoveryThreshold = 2;
        bytes memory recoveryData = abi.encode(recoveryThreshold, guardians);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(socialRecovery), recoveryData);

        // Verify: Account now has two validators
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(ownableValidator), ""));
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(socialRecovery), ""));

        // Verify: Guardian configuration
        assertEq(socialRecovery.threshold(accountAddr), 2);
        assertEq(socialRecovery.guardianCount(accountAddr), 3);
    }

    // =========================================================================
    // EXAMPLE 4: Account with Deadman Switch
    // =========================================================================
    // Account with inheritance/recovery if owner is inactive

    function test_Example_DeadmanSwitchAccount() public {
        // Step 1: Create account with single owner
        address[] memory owners = new address[](1);
        owners[0] = alice;

        bytes memory validatorData = abi.encode(uint256(1), owners);
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorData);

        bytes32 salt = keccak256("deadman-switch-example");
        address accountAddr = factory.createAccount(salt, initData);
        NethereumAccount account = NethereumAccount(payable(accountAddr));

        vm.deal(accountAddr, 10 ether);

        // Step 2: Configure deadman switch
        uint48 timeout = 365 days; // 1 year of inactivity
        address nomineeAddr = nominee;

        // DeadmanSwitch expects: abi.encodePacked(nominee, timeout)
        bytes memory dmsData = abi.encodePacked(nomineeAddr, timeout);

        // Install as both validator (for nominee to use) and hook (to track activity)
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(deadmanSwitch), dmsData);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_HOOK, address(deadmanSwitch), "");

        // Verify configuration
        (uint48 lastAccess, uint48 storedTimeout, address storedNominee) =
            deadmanSwitch.config(accountAddr);

        assertEq(storedNominee, nomineeAddr);
        assertEq(storedTimeout, timeout);
        assertGt(lastAccess, 0);

        // Simulate normal activity (updates lastAccess)
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(target),
            0,
            abi.encodeCall(MockTarget.setValue, (1))
        );

        (uint48 lastAccessBefore,,) = deadmanSwitch.config(accountAddr);

        vm.warp(block.timestamp + 1 days);

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        (uint48 lastAccessAfter,,) = deadmanSwitch.config(accountAddr);
        assertGt(lastAccessAfter, lastAccessBefore, "Activity should update lastAccess");
    }

    // =========================================================================
    // EXAMPLE 5: Delegated Execution with OwnableExecutor
    // =========================================================================
    // Allow trusted party to execute on behalf of account

    function test_Example_DelegatedExecution() public {
        // Step 1: Create account owned by Alice
        address[] memory owners = new address[](1);
        owners[0] = alice;

        bytes memory validatorData = abi.encode(uint256(1), owners);
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorData);

        bytes32 salt = keccak256("delegated-execution-example");
        address accountAddr = factory.createAccount(salt, initData);
        NethereumAccount account = NethereumAccount(payable(accountAddr));

        vm.deal(accountAddr, 10 ether);

        // Step 2: Install OwnableExecutor with Bob as delegate
        // Bob can execute transactions on behalf of Alice's account
        bytes memory executorData = abi.encodePacked(bob);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_EXECUTOR, address(ownableExecutor), executorData);

        // Step 3: Bob executes a transaction
        bytes memory execCallData = ExecutionLib.encodeSingle(
            address(target),
            1 ether,
            abi.encodeCall(MockTarget.setValue, (999))
        );

        vm.prank(bob);
        ownableExecutor.executeOnOwnedAccount(accountAddr, execCallData);

        // Verify: Transaction executed
        assertEq(target.value(), 999);
        assertEq(address(target).balance, 1 ether);
    }

    // =========================================================================
    // EXAMPLE 6: Full Security Setup
    // =========================================================================
    // Comprehensive account with multisig, recovery, and deadman switch

    function test_Example_FullSecuritySetup() public {
        // Step 1: Create 2-of-3 multisig account
        address[] memory owners = new address[](3);
        owners[0] = alice;
        owners[1] = bob;
        owners[2] = charlie;
        owners = _sortAddresses(owners);

        bytes memory validatorData = abi.encode(uint256(2), owners);
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorData);

        bytes32 salt = keccak256("full-security-example");
        address accountAddr = factory.createAccount(salt, initData);
        NethereumAccount account = NethereumAccount(payable(accountAddr));

        vm.deal(accountAddr, 100 ether);

        // Step 2: Add social recovery (backup validators)
        address[] memory guardians = new address[](3);
        guardians[0] = guardian1;
        guardians[1] = guardian2;
        guardians[2] = guardian3;
        guardians = _sortAddresses(guardians);

        bytes memory recoveryData = abi.encode(uint256(2), guardians);
        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(socialRecovery), recoveryData);

        // Step 3: Add deadman switch (inheritance)
        uint48 inheritanceTimeout = 180 days;
        bytes memory dmsData = abi.encodePacked(nominee, inheritanceTimeout);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_VALIDATOR, address(deadmanSwitch), dmsData);

        vm.prank(address(entryPoint));
        account.installModule(MODULE_TYPE_HOOK, address(deadmanSwitch), "");

        // Verify: Full module setup
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(ownableValidator), ""));
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(socialRecovery), ""));
        assertTrue(account.isModuleInstalled(MODULE_TYPE_VALIDATOR, address(deadmanSwitch), ""));
        assertTrue(account.isModuleInstalled(MODULE_TYPE_HOOK, address(deadmanSwitch), ""));

        // Account can now be accessed via:
        // 1. 2-of-3 owner signatures (normal operation)
        // 2. 2-of-3 guardian signatures (recovery)
        // 3. Nominee (after 180 days of inactivity)
    }

    // =========================================================================
    // EXAMPLE 7: Dynamic Owner Management
    // =========================================================================
    // Adding and removing owners from multisig

    function test_Example_DynamicOwnerManagement() public {
        // Create 1-of-2 multisig
        address[] memory initialOwners = new address[](2);
        initialOwners[0] = alice;
        initialOwners[1] = bob;
        initialOwners = _sortAddresses(initialOwners);

        bytes memory validatorData = abi.encode(uint256(1), initialOwners);
        bytes memory initData = abi.encodePacked(address(ownableValidator), validatorData);

        bytes32 salt = keccak256("dynamic-owners-example");
        address accountAddr = factory.createAccount(salt, initData);
        NethereumAccount account = NethereumAccount(payable(accountAddr));

        vm.deal(accountAddr, 10 ether);

        assertEq(ownableValidator.ownerCount(accountAddr), 2);

        // Add Charlie as owner
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory addOwnerCall = abi.encodeCall(OwnableValidator.addOwner, (charlie));
        bytes memory execData = ExecutionLib.encodeSingle(
            address(ownableValidator),
            0,
            addOwnerCall
        );

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        assertEq(ownableValidator.ownerCount(accountAddr), 3);

        // Increase threshold to 2-of-3
        bytes memory setThresholdCall = abi.encodeCall(OwnableValidator.setThreshold, (2));
        execData = ExecutionLib.encodeSingle(address(ownableValidator), 0, setThresholdCall);

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        assertEq(ownableValidator.threshold(accountAddr), 2);
    }
}
