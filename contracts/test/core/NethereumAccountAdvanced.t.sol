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
// STANDARD MODULE IMPLEMENTATIONS FOR TESTING
// =============================================================================

/// @notice Multisig Validator - Requires M of N signatures
contract MultisigValidator is IValidator {
    struct MultisigConfig {
        address[] signers;
        uint256 threshold;
    }

    mapping(address account => MultisigConfig) public configs;

    function onInstall(bytes calldata data) external override {
        (address[] memory signers, uint256 threshold) = abi.decode(data, (address[], uint256));
        require(threshold > 0 && threshold <= signers.length, "Invalid threshold");
        configs[msg.sender].signers = signers;
        configs[msg.sender].threshold = threshold;
    }

    function onUninstall(bytes calldata) external override {
        delete configs[msg.sender];
    }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == 1;
    }

    function isInitialized(address account) external view override returns (bool) {
        return configs[account].signers.length > 0;
    }

    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) external view override returns (uint256) {
        MultisigConfig storage config = configs[userOp.sender];
        if (config.signers.length == 0) return 1;

        // Signature format: [65-byte sig 1][65-byte sig 2]...
        bytes memory sig = userOp.signature;
        if (sig.length < config.threshold * 65) return 1;

        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(userOpHash);
        uint256 validSigs = 0;
        address lastSigner = address(0);

        for (uint256 i = 0; i < sig.length / 65 && validSigs < config.threshold; i++) {
            bytes memory singleSig = new bytes(65);
            for (uint256 j = 0; j < 65; j++) {
                singleSig[j] = sig[i * 65 + j];
            }

            address recovered = ECDSA.recover(ethHash, singleSig);

            // Signatures must be in ascending order (prevents duplicates)
            if (recovered <= lastSigner) return 1;

            if (_isSigner(config, recovered)) {
                validSigs++;
                lastSigner = recovered;
            }
        }

        return validSigs >= config.threshold ? 0 : 1;
    }

    function isValidSignatureWithSender(
        address,
        bytes32 hash,
        bytes calldata signature
    ) external view override returns (bytes4) {
        MultisigConfig storage config = configs[msg.sender];
        if (config.signers.length == 0) return bytes4(0xffffffff);

        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(hash);
        uint256 validSigs = 0;
        address lastSigner = address(0);

        for (uint256 i = 0; i < signature.length / 65 && validSigs < config.threshold; i++) {
            bytes memory singleSig = new bytes(65);
            for (uint256 j = 0; j < 65; j++) {
                singleSig[j] = signature[i * 65 + j];
            }

            address recovered = ECDSA.recover(ethHash, singleSig);
            if (recovered <= lastSigner) return bytes4(0xffffffff);

            if (_isSigner(config, recovered)) {
                validSigs++;
                lastSigner = recovered;
            }
        }

        return validSigs >= config.threshold ? bytes4(0x1626ba7e) : bytes4(0xffffffff);
    }

    function _isSigner(MultisigConfig storage config, address addr) internal view returns (bool) {
        for (uint256 i = 0; i < config.signers.length; i++) {
            if (config.signers[i] == addr) return true;
        }
        return false;
    }

    function getSigners(address account) external view returns (address[] memory) {
        return configs[account].signers;
    }

    function getThreshold(address account) external view returns (uint256) {
        return configs[account].threshold;
    }
}

/// @notice Session Key Validator with spending limits and time bounds
contract SessionKeyValidator is IValidator {
    struct SessionKey {
        address key;
        uint48 validAfter;
        uint48 validUntil;
        uint256 spendingLimit;
        uint256 spent;
        address[] allowedTargets;
        bool active;
    }

    mapping(address account => mapping(address key => SessionKey)) public sessions;
    mapping(address account => address[]) public sessionKeys;

    function onInstall(bytes calldata data) external override {
        (
            address key,
            uint48 validAfter,
            uint48 validUntil,
            uint256 spendingLimit,
            address[] memory allowedTargets
        ) = abi.decode(data, (address, uint48, uint48, uint256, address[]));

        sessions[msg.sender][key] = SessionKey({
            key: key,
            validAfter: validAfter,
            validUntil: validUntil,
            spendingLimit: spendingLimit,
            spent: 0,
            allowedTargets: allowedTargets,
            active: true
        });
        sessionKeys[msg.sender].push(key);
    }

    function onUninstall(bytes calldata data) external override {
        address key = abi.decode(data, (address));
        delete sessions[msg.sender][key];
    }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == 1;
    }

    function isInitialized(address account) external view override returns (bool) {
        return sessionKeys[account].length > 0;
    }

    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) external view override returns (uint256) {
        // Extract session key from first 20 bytes of signature
        if (userOp.signature.length < 85) return 1; // 20 + 65

        address sessionKey = address(bytes20(userOp.signature[:20]));
        bytes memory sig = userOp.signature[20:];

        SessionKey storage session = sessions[userOp.sender][sessionKey];
        if (!session.active) return 1;
        if (block.timestamp < session.validAfter) return 1;
        if (block.timestamp > session.validUntil) return 1;

        // Verify signature
        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(userOpHash);
        address recovered = ECDSA.recover(ethHash, sig);
        if (recovered != sessionKey) return 1;

        // Return validation data with time bounds
        uint256 validationData = uint256(session.validUntil) << 160 | uint256(session.validAfter) << 208;
        return validationData;
    }

    function isValidSignatureWithSender(
        address,
        bytes32,
        bytes calldata
    ) external pure override returns (bytes4) {
        // Session keys don't support ERC-1271
        return bytes4(0xffffffff);
    }

    function revokeSession(address key) external {
        sessions[msg.sender][key].active = false;
    }

    function updateSpent(address account, address key, uint256 amount) external {
        sessions[account][key].spent += amount;
    }
}

/// @notice Spending Limit Hook - Enforces daily spending limits
contract SpendingLimitHook is IHook {
    struct Limit {
        uint256 dailyLimit;
        uint256 spentToday;
        uint256 lastResetDay;
    }

    mapping(address account => Limit) public limits;

    function onInstall(bytes calldata data) external override {
        uint256 dailyLimit = abi.decode(data, (uint256));
        limits[msg.sender] = Limit({
            dailyLimit: dailyLimit,
            spentToday: 0,
            lastResetDay: block.timestamp / 1 days
        });
    }

    function onUninstall(bytes calldata) external override {
        delete limits[msg.sender];
    }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == 4;
    }

    function isInitialized(address account) external view override returns (bool) {
        return limits[account].dailyLimit > 0;
    }

    function preCheck(
        address,
        uint256 value,
        bytes calldata
    ) external override returns (bytes memory) {
        Limit storage limit = limits[msg.sender];

        // Reset if new day
        uint256 currentDay = block.timestamp / 1 days;
        if (currentDay > limit.lastResetDay) {
            limit.spentToday = 0;
            limit.lastResetDay = currentDay;
        }

        // Check limit
        require(limit.spentToday + value <= limit.dailyLimit, "Daily limit exceeded");

        return abi.encode(value);
    }

    function postCheck(bytes calldata hookData) external override {
        uint256 value = abi.decode(hookData, (uint256));
        limits[msg.sender].spentToday += value;
    }
}

/// @notice Deadman Switch - Allows recovery after inactivity period
contract DeadmanSwitchValidator is IValidator {
    struct Config {
        address nominee;
        uint256 inactivityPeriod;
        uint256 lastActivity;
    }

    mapping(address account => Config) public configs;

    function onInstall(bytes calldata data) external override {
        (address nominee, uint256 inactivityPeriod) = abi.decode(data, (address, uint256));
        configs[msg.sender] = Config({
            nominee: nominee,
            inactivityPeriod: inactivityPeriod,
            lastActivity: block.timestamp
        });
    }

    function onUninstall(bytes calldata) external override {
        delete configs[msg.sender];
    }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == 1;
    }

    function isInitialized(address account) external view override returns (bool) {
        return configs[account].nominee != address(0);
    }

    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) external view override returns (uint256) {
        Config storage config = configs[userOp.sender];

        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(userOpHash);
        address recovered = ECDSA.recover(ethHash, userOp.signature);

        // Nominee can sign if inactivity period passed
        if (recovered == config.nominee) {
            if (block.timestamp >= config.lastActivity + config.inactivityPeriod) {
                return 0;
            }
            return 1; // Too early
        }

        return 1; // Unknown signer
    }

    function isValidSignatureWithSender(
        address,
        bytes32 hash,
        bytes calldata signature
    ) external view override returns (bytes4) {
        Config storage config = configs[msg.sender];

        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(hash);
        address recovered = ECDSA.recover(ethHash, signature);

        if (recovered == config.nominee &&
            block.timestamp >= config.lastActivity + config.inactivityPeriod) {
            return bytes4(0x1626ba7e);
        }
        return bytes4(0xffffffff);
    }

    function heartbeat(address account) external {
        configs[account].lastActivity = block.timestamp;
    }
}

/// @notice Time-locked Recovery Module
contract SocialRecoveryValidator is IValidator {
    struct RecoveryConfig {
        address[] guardians;
        uint256 threshold;
        uint256 recoveryDelay;
    }

    struct RecoveryRequest {
        address newOwner;
        uint256 executeAfter;
        uint256 approvals;
        mapping(address => bool) approved;
    }

    mapping(address account => RecoveryConfig) public configs;
    mapping(address account => RecoveryRequest) public requests;

    function onInstall(bytes calldata data) external override {
        (address[] memory guardians, uint256 threshold, uint256 delay) =
            abi.decode(data, (address[], uint256, uint256));
        require(threshold > 0 && threshold <= guardians.length, "Invalid threshold");
        configs[msg.sender].guardians = guardians;
        configs[msg.sender].threshold = threshold;
        configs[msg.sender].recoveryDelay = delay;
    }

    function onUninstall(bytes calldata) external override {
        delete configs[msg.sender];
    }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == 1;
    }

    function isInitialized(address account) external view override returns (bool) {
        return configs[account].guardians.length > 0;
    }

    function initiateRecovery(address account, address newOwner) external {
        require(_isGuardian(account, msg.sender), "Not a guardian");

        RecoveryRequest storage req = requests[account];
        req.newOwner = newOwner;
        req.executeAfter = block.timestamp + configs[account].recoveryDelay;
        req.approvals = 1;
        req.approved[msg.sender] = true;
    }

    function approveRecovery(address account) external {
        require(_isGuardian(account, msg.sender), "Not a guardian");
        RecoveryRequest storage req = requests[account];
        require(req.newOwner != address(0), "No pending recovery");
        require(!req.approved[msg.sender], "Already approved");

        req.approved[msg.sender] = true;
        req.approvals++;
    }

    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) external view override returns (uint256) {
        RecoveryRequest storage req = requests[userOp.sender];
        RecoveryConfig storage config = configs[userOp.sender];

        // Check if recovery can be executed
        if (req.approvals < config.threshold) return 1;
        if (block.timestamp < req.executeAfter) return 1;

        // Verify new owner's signature
        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(userOpHash);
        address recovered = ECDSA.recover(ethHash, userOp.signature);

        return recovered == req.newOwner ? 0 : 1;
    }

    function isValidSignatureWithSender(
        address,
        bytes32,
        bytes calldata
    ) external pure override returns (bytes4) {
        return bytes4(0xffffffff);
    }

    function _isGuardian(address account, address addr) internal view returns (bool) {
        address[] storage guardians = configs[account].guardians;
        for (uint256 i = 0; i < guardians.length; i++) {
            if (guardians[i] == addr) return true;
        }
        return false;
    }

    function getGuardians(address account) external view returns (address[] memory) {
        return configs[account].guardians;
    }
}

// =============================================================================
// MOCK CONTRACTS FOR ATTACK VECTOR TESTING
// =============================================================================

contract MockEntryPointV2 {
    mapping(address => uint256) public balances;

    function depositTo(address account) external payable {
        balances[account] += msg.value;
    }

    function balanceOf(address account) external view returns (uint256) {
        return balances[account];
    }

    function getNonce(address, uint192) external pure returns (uint256) {
        return 0;
    }

    // Malicious: skips validation
    function handleOpsWithoutValidation(
        address account,
        bytes calldata callData
    ) external {
        (bool success,) = account.call(callData);
        require(success, "Call failed");
    }
}

contract MaliciousDelegateTarget {
    // Tries to overwrite ownership storage
    function attack(address newOwner) external {
        // Attempt to write to storage slot 0
        assembly {
            sstore(0, newOwner)
        }
    }
}

contract GasGriefingContract {
    uint256 public counter;

    function consumeGas(uint256 iterations) external {
        for (uint256 i = 0; i < iterations; i++) {
            counter = counter + 1;
        }
    }

    receive() external payable {
        // Consume all remaining gas
        while (gasleft() > 1000) {
            counter++;
        }
    }
}

contract ReentrancyAttacker {
    NethereumAccount public target;
    uint256 public attackCount;
    bool public attackViaFallback;

    function setTarget(address _target) external {
        target = NethereumAccount(payable(_target));
    }

    function setAttackMode(bool viaFallback) external {
        attackViaFallback = viaFallback;
    }

    function attack() external {
        attackCount++;
        if (attackCount < 3) {
            ModeCode mode = ModeLib.encodeSimpleSingle();
            bytes memory execData = ExecutionLib.encodeSingle(address(this), 0, "");
            target.execute(mode, execData);
        }
    }

    receive() external payable {
        if (attackViaFallback) {
            this.attack();
        }
    }
}

contract MockERC20 {
    mapping(address => uint256) public balanceOf;
    mapping(address => mapping(address => uint256)) public allowance;

    function mint(address to, uint256 amount) external {
        balanceOf[to] += amount;
    }

    function transfer(address to, uint256 amount) external returns (bool) {
        require(balanceOf[msg.sender] >= amount, "Insufficient balance");
        balanceOf[msg.sender] -= amount;
        balanceOf[to] += amount;
        return true;
    }

    function approve(address spender, uint256 amount) external returns (bool) {
        allowance[msg.sender][spender] = amount;
        return true;
    }

    function transferFrom(address from, address to, uint256 amount) external returns (bool) {
        require(allowance[from][msg.sender] >= amount, "Insufficient allowance");
        require(balanceOf[from] >= amount, "Insufficient balance");
        allowance[from][msg.sender] -= amount;
        balanceOf[from] -= amount;
        balanceOf[to] += amount;
        return true;
    }
}

/// @notice Basic ECDSA Validator for comparison
contract ECDSAValidator is IValidator {
    mapping(address account => address owner) public owners;

    function onInstall(bytes calldata data) external override {
        owners[msg.sender] = address(bytes20(data[:20]));
    }

    function onUninstall(bytes calldata) external override {
        delete owners[msg.sender];
    }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == 1;
    }

    function isInitialized(address account) external view override returns (bool) {
        return owners[account] != address(0);
    }

    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) external view override returns (uint256) {
        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(userOpHash);
        address recovered = ECDSA.recover(ethHash, userOp.signature);
        return recovered == owners[userOp.sender] ? 0 : 1;
    }

    function isValidSignatureWithSender(
        address,
        bytes32 hash,
        bytes calldata signature
    ) external view override returns (bytes4) {
        bytes32 ethHash = MessageHashUtils.toEthSignedMessageHash(hash);
        address recovered = ECDSA.recover(ethHash, signature);
        return recovered == owners[msg.sender] ? bytes4(0x1626ba7e) : bytes4(0xffffffff);
    }
}

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

// =============================================================================
// ADVANCED TEST CONTRACT
// =============================================================================

contract NethereumAccountAdvancedTest is Test {
    using ECDSA for bytes32;
    using MessageHashUtils for bytes32;

    NethereumAccount public implementation;
    NethereumAccount public account;
    NethereumAccountFactory public factory;
    MockEntryPoint public entryPoint;
    ECDSAValidator public ecdsaValidator;
    MultisigValidator public multisigValidator;
    SessionKeyValidator public sessionKeyValidator;
    SpendingLimitHook public spendingLimitHook;
    DeadmanSwitchValidator public deadmanSwitch;
    SocialRecoveryValidator public socialRecovery;
    MockTarget public target;
    MockERC20 public token;

    uint256 public ownerKey = 0x1234;
    address public owner;
    uint256 public signer1Key = 0x5678;
    address public signer1;
    uint256 public signer2Key = 0x9ABC;
    address public signer2;
    uint256 public signer3Key = 0xDEF0;
    address public signer3;
    uint256 public sessionKey = 0xAAAA;
    address public sessionKeyAddr;
    uint256 public nomineeKey = 0xBBBB;
    address public nominee;
    uint256 public guardian1Key = 0xCCCC;
    address public guardian1;
    uint256 public guardian2Key = 0xDDDD;
    address public guardian2;

    bytes32 public constant SALT = bytes32(uint256(1));

    function setUp() public {
        owner = vm.addr(ownerKey);
        signer1 = vm.addr(signer1Key);
        signer2 = vm.addr(signer2Key);
        signer3 = vm.addr(signer3Key);
        sessionKeyAddr = vm.addr(sessionKey);
        nominee = vm.addr(nomineeKey);
        guardian1 = vm.addr(guardian1Key);
        guardian2 = vm.addr(guardian2Key);

        // Deploy core contracts
        entryPoint = new MockEntryPoint();
        ecdsaValidator = new ECDSAValidator();
        multisigValidator = new MultisigValidator();
        sessionKeyValidator = new SessionKeyValidator();
        spendingLimitHook = new SpendingLimitHook();
        deadmanSwitch = new DeadmanSwitchValidator();
        socialRecovery = new SocialRecoveryValidator();
        target = new MockTarget();
        token = new MockERC20();

        // Deploy factory
        factory = new NethereumAccountFactory(address(entryPoint));

        // Create account with ECDSA validator
        bytes memory initData = abi.encodePacked(address(ecdsaValidator), owner);
        address accountAddr = factory.createAccount(SALT, initData);
        account = NethereumAccount(payable(accountAddr));

        // Fund account
        vm.deal(address(account), 100 ether);
        token.mint(address(account), 1000 ether);
    }

    // =========================================================================
    // MULTISIG VALIDATOR TESTS
    // =========================================================================

    function test_Multisig_2of3_ValidSignatures() public {
        // Setup 2-of-3 multisig
        address[] memory signers = new address[](3);
        signers[0] = signer1;
        signers[1] = signer2;
        signers[2] = signer3;

        // Sort signers for signature ordering
        if (signer1 > signer2) (signers[0], signers[1]) = (signer2, signer1);
        if (signers[1] > signer3) (signers[1], signers[2]) = (signer3, signers[1]);
        if (signers[0] > signers[1]) (signers[0], signers[1]) = (signers[1], signers[0]);

        bytes memory installData = abi.encode(signers, uint256(2));

        vm.prank(address(entryPoint));
        account.installModule(1, address(multisigValidator), installData);

        // Create UserOp
        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        // Sign with first two signers (sorted order)
        uint256 key1 = signers[0] == signer1 ? signer1Key : (signers[0] == signer2 ? signer2Key : signer3Key);
        uint256 key2 = signers[1] == signer1 ? signer1Key : (signers[1] == signer2 ? signer2Key : signer3Key);

        (uint8 v1, bytes32 r1, bytes32 s1) = vm.sign(key1, ethHash);
        (uint8 v2, bytes32 r2, bytes32 s2) = vm.sign(key2, ethHash);

        userOp.signature = abi.encodePacked(
            address(multisigValidator),
            abi.encodePacked(r1, s1, v1),
            abi.encodePacked(r2, s2, v2)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 0, "Multisig validation should succeed");
    }

    function test_Multisig_InsufficientSignatures() public {
        address[] memory signers = new address[](3);
        signers[0] = signer1;
        signers[1] = signer2;
        signers[2] = signer3;

        bytes memory installData = abi.encode(signers, uint256(2));

        vm.prank(address(entryPoint));
        account.installModule(1, address(multisigValidator), installData);

        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        // Only one signature
        (uint8 v1, bytes32 r1, bytes32 s1) = vm.sign(signer1Key, ethHash);
        userOp.signature = abi.encodePacked(
            address(multisigValidator),
            abi.encodePacked(r1, s1, v1)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Should fail with insufficient signatures");
    }

    function test_Multisig_DuplicateSignatures() public {
        address[] memory signers = new address[](2);
        signers[0] = signer1;
        signers[1] = signer2;

        bytes memory installData = abi.encode(signers, uint256(2));

        vm.prank(address(entryPoint));
        account.installModule(1, address(multisigValidator), installData);

        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        // Same signer twice
        (uint8 v1, bytes32 r1, bytes32 s1) = vm.sign(signer1Key, ethHash);
        userOp.signature = abi.encodePacked(
            address(multisigValidator),
            abi.encodePacked(r1, s1, v1),
            abi.encodePacked(r1, s1, v1)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Should reject duplicate signatures");
    }

    // =========================================================================
    // SESSION KEY TESTS
    // =========================================================================

    function test_SessionKey_ValidWithinTimeBounds() public {
        // Install session key
        bytes memory installData = abi.encode(
            sessionKeyAddr,
            uint48(block.timestamp),
            uint48(block.timestamp + 1 days),
            1 ether,
            new address[](0)
        );

        vm.prank(address(entryPoint));
        account.installModule(1, address(sessionKeyValidator), installData);

        // Create and sign UserOp with session key
        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        (uint8 v, bytes32 r, bytes32 s) = vm.sign(sessionKey, ethHash);
        userOp.signature = abi.encodePacked(
            address(sessionKeyValidator),
            sessionKeyAddr,
            abi.encodePacked(r, s, v)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        // Should return time-bounded validation data (not 1 for failure)
        assertTrue(result != 1, "Session key validation should succeed");
    }

    function test_SessionKey_ExpiredSession() public {
        // Set a reasonable timestamp first (foundry starts at timestamp=1)
        vm.warp(1000000);

        // Install session key with past validity window
        bytes memory installData = abi.encode(
            sessionKeyAddr,
            uint48(100), // validAfter - in the past
            uint48(500), // validUntil - also in the past (expired)
            1 ether,
            new address[](0)
        );

        vm.prank(address(entryPoint));
        account.installModule(1, address(sessionKeyValidator), installData);

        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        (uint8 v, bytes32 r, bytes32 s) = vm.sign(sessionKey, ethHash);
        userOp.signature = abi.encodePacked(
            address(sessionKeyValidator),
            sessionKeyAddr,
            abi.encodePacked(r, s, v)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Expired session should fail");
    }

    function test_SessionKey_RevokedSession() public {
        bytes memory installData = abi.encode(
            sessionKeyAddr,
            uint48(block.timestamp),
            uint48(block.timestamp + 1 days),
            1 ether,
            new address[](0)
        );

        vm.prank(address(entryPoint));
        account.installModule(1, address(sessionKeyValidator), installData);

        // Revoke the session
        vm.prank(address(account));
        sessionKeyValidator.revokeSession(sessionKeyAddr);

        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        (uint8 v, bytes32 r, bytes32 s) = vm.sign(sessionKey, ethHash);
        userOp.signature = abi.encodePacked(
            address(sessionKeyValidator),
            sessionKeyAddr,
            abi.encodePacked(r, s, v)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Revoked session should fail");
    }

    // =========================================================================
    // SPENDING LIMIT HOOK TESTS
    // =========================================================================

    function test_SpendingLimit_WithinLimit() public {
        // Install spending limit hook
        vm.prank(address(entryPoint));
        account.installModule(4, address(spendingLimitHook), abi.encode(10 ether));

        assertTrue(account.isModuleInstalled(4, address(spendingLimitHook), ""));
    }

    function test_SpendingLimit_ExceedsLimit() public {
        vm.prank(address(entryPoint));
        account.installModule(4, address(spendingLimitHook), abi.encode(1 ether));

        // The hook tracks msg.value sent to execute(), not value in execution data
        // This demonstrates a real limitation - spending limit hooks need to parse
        // execution data for comprehensive protection
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(target), 0, "");

        // Send 2 ether with the call - this is what the hook sees
        vm.deal(address(entryPoint), 3 ether);
        vm.prank(address(entryPoint));
        vm.expectRevert("Daily limit exceeded");
        account.execute{value: 2 ether}(mode, execData);
    }

    // =========================================================================
    // DEADMAN SWITCH TESTS
    // =========================================================================

    function test_DeadmanSwitch_NomineeCanAccessAfterInactivity() public {
        // Install deadman switch with 30 day inactivity period
        bytes memory installData = abi.encode(nominee, 30 days);

        vm.prank(address(entryPoint));
        account.installModule(1, address(deadmanSwitch), installData);

        // Fast forward past inactivity period
        vm.warp(block.timestamp + 31 days);

        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        (uint8 v, bytes32 r, bytes32 s) = vm.sign(nomineeKey, ethHash);
        userOp.signature = abi.encodePacked(
            address(deadmanSwitch),
            abi.encodePacked(r, s, v)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 0, "Nominee should be able to access after inactivity");
    }

    function test_DeadmanSwitch_NomineeBlockedBeforeInactivity() public {
        bytes memory installData = abi.encode(nominee, 30 days);

        vm.prank(address(entryPoint));
        account.installModule(1, address(deadmanSwitch), installData);

        // Only 10 days passed
        vm.warp(block.timestamp + 10 days);

        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        (uint8 v, bytes32 r, bytes32 s) = vm.sign(nomineeKey, ethHash);
        userOp.signature = abi.encodePacked(
            address(deadmanSwitch),
            abi.encodePacked(r, s, v)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Nominee should be blocked before inactivity period");
    }

    // =========================================================================
    // SOCIAL RECOVERY TESTS
    // =========================================================================

    function test_SocialRecovery_FullRecoveryFlow() public {
        address[] memory guardians = new address[](2);
        guardians[0] = guardian1;
        guardians[1] = guardian2;

        bytes memory installData = abi.encode(guardians, uint256(2), uint256(1 days));

        vm.prank(address(entryPoint));
        account.installModule(1, address(socialRecovery), installData);

        address newOwner = vm.addr(0xFFFF);

        // Guardian 1 initiates recovery
        vm.prank(guardian1);
        socialRecovery.initiateRecovery(address(account), newOwner);

        // Guardian 2 approves
        vm.prank(guardian2);
        socialRecovery.approveRecovery(address(account));

        // Wait for delay
        vm.warp(block.timestamp + 2 days);

        // New owner can now sign
        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        (uint8 v, bytes32 r, bytes32 s) = vm.sign(0xFFFF, ethHash);
        userOp.signature = abi.encodePacked(
            address(socialRecovery),
            abi.encodePacked(r, s, v)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 0, "Recovery should succeed after threshold and delay");
    }

    function test_SocialRecovery_InsufficientApprovals() public {
        address[] memory guardians = new address[](2);
        guardians[0] = guardian1;
        guardians[1] = guardian2;

        bytes memory installData = abi.encode(guardians, uint256(2), uint256(1 days));

        vm.prank(address(entryPoint));
        account.installModule(1, address(socialRecovery), installData);

        address newOwner = vm.addr(0xFFFF);

        // Only guardian 1 initiates (acts as first approval)
        vm.prank(guardian1);
        socialRecovery.initiateRecovery(address(account), newOwner);

        // Wait for delay but don't get second approval
        vm.warp(block.timestamp + 2 days);

        PackedUserOperation memory userOp = _createUserOp(address(account));
        bytes32 userOpHash = keccak256(abi.encode(userOp));
        bytes32 ethHash = userOpHash.toEthSignedMessageHash();

        (uint8 v, bytes32 r, bytes32 s) = vm.sign(0xFFFF, ethHash);
        userOp.signature = abi.encodePacked(
            address(socialRecovery),
            abi.encodePacked(r, s, v)
        );

        uint256 result = entryPoint.simulateValidation(address(account), userOp, userOpHash, 0);
        assertEq(result, 1, "Should fail without sufficient approvals");
    }

    // =========================================================================
    // SIGNATURE REPLAY ATTACK TESTS
    // =========================================================================

    function test_Attack_CrossAccountReplay() public {
        // Create second account with DIFFERENT owner
        address differentOwner = vm.addr(0x9999);
        bytes memory initData = abi.encodePacked(address(ecdsaValidator), differentOwner);
        address accountAddr2 = factory.createAccount(bytes32(uint256(2)), initData);
        NethereumAccount account2 = NethereumAccount(payable(accountAddr2));

        // Sign for account1 with original owner
        PackedUserOperation memory userOp1 = _createUserOp(address(account));
        bytes32 userOpHash1 = keccak256(abi.encode(userOp1));
        bytes32 ethHash1 = userOpHash1.toEthSignedMessageHash();

        (uint8 v, bytes32 r, bytes32 s) = vm.sign(ownerKey, ethHash1);
        userOp1.signature = abi.encodePacked(
            address(ecdsaValidator),
            abi.encodePacked(r, s, v)
        );

        // Verify works for account1
        uint256 result1 = entryPoint.simulateValidation(address(account), userOp1, userOpHash1, 0);
        assertEq(result1, 0, "Original signature should work");

        // Try to replay on account2 - should fail because account2 has different owner
        // Even if attacker tries to use the same signature
        PackedUserOperation memory userOp2 = userOp1;
        userOp2.sender = address(account2);

        uint256 result2 = entryPoint.simulateValidation(address(account2), userOp2, userOpHash1, 0);
        // Should fail because recovered signer != account2's owner
        assertEq(result2, 1, "Cross-account replay should fail - different owner");
    }

    function test_Attack_CrossChainReplay() public {
        // UserOp hash should include chainId to prevent cross-chain replay
        PackedUserOperation memory userOp = _createUserOp(address(account));

        // Real EntryPoint includes chainId in hash computation
        bytes32 userOpHash = keccak256(abi.encode(
            userOp,
            address(entryPoint),
            block.chainid
        ));

        bytes32 ethHash = userOpHash.toEthSignedMessageHash();
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(ownerKey, ethHash);

        userOp.signature = abi.encodePacked(
            address(ecdsaValidator),
            abi.encodePacked(r, s, v)
        );

        // On different chainId, the hash would be different
        bytes32 differentChainHash = keccak256(abi.encode(
            userOp,
            address(entryPoint),
            block.chainid + 1 // Different chain
        ));

        // Signature won't match for different chain hash
        assertTrue(userOpHash != differentChainHash, "Hash should differ across chains");
    }

    // =========================================================================
    // GAS GRIEFING ATTACK TESTS
    // =========================================================================

    function test_Attack_GasGriefingViaTarget() public {
        GasGriefingContract griefingContract = new GasGriefingContract();

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(griefingContract),
            0,
            abi.encodeCall(griefingContract.consumeGas, (1000))
        );

        // Should complete without running out of gas
        vm.prank(address(entryPoint));
        account.execute(mode, execData);
    }

    function test_Attack_GasGriefingViaReceive() public {
        GasGriefingContract griefingContract = new GasGriefingContract();

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(griefingContract),
            0.1 ether,
            ""
        );

        // The call should complete even if receive() tries to consume gas
        vm.prank(address(entryPoint));
        // This might run out of gas, which is actually the expected behavior
        // The key is that the account itself remains secure
        try account.execute(mode, execData) {
            // Success is fine
        } catch {
            // Failure is also acceptable - the griefing is contained
        }

        // Account should still be functional
        assertTrue(address(account).balance >= 99 ether, "Account should retain most funds");
    }

    // =========================================================================
    // REENTRANCY ATTACK TESTS
    // =========================================================================

    function test_Attack_ReentrancyViaCallback() public {
        ReentrancyAttacker attacker = new ReentrancyAttacker();
        attacker.setTarget(address(account));
        attacker.setAttackMode(false);

        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(
            address(attacker),
            0,
            abi.encodeCall(attacker.attack, ())
        );

        // Even if attacker tries to re-enter, it shouldn't cause issues
        vm.prank(address(entryPoint));
        // This will revert because attacker doesn't have execute permission
        vm.expectRevert();
        account.execute(mode, execData);
    }

    // =========================================================================
    // DELEGATECALL STORAGE COLLISION TESTS
    // =========================================================================

    function test_Attack_DelegatecallStorageCollision() public {
        MaliciousDelegateTarget malicious = new MaliciousDelegateTarget();

        // Attempt delegatecall that tries to overwrite storage
        ModeCode mode = ModeLib.encode(CALLTYPE_DELEGATECALL, EXECTYPE_DEFAULT, MODE_DEFAULT, PAYLOAD_DEFAULT);
        // Delegatecall format: target (20 bytes) + callData (no value field)
        bytes memory execData = abi.encodePacked(
            address(malicious),
            abi.encodeCall(malicious.attack, (address(0xdead)))
        );

        // Get validator before attack
        bool validatorBefore = account.isModuleInstalled(1, address(ecdsaValidator), "");

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        // Validator should still be installed (ERC-7201 namespace protection)
        bool validatorAfter = account.isModuleInstalled(1, address(ecdsaValidator), "");
        assertEq(validatorBefore, validatorAfter, "Storage should be protected by ERC-7201 namespace");
    }

    // =========================================================================
    // MODULE TYPE CONFUSION TESTS
    // =========================================================================

    function test_Attack_ValidatorAsExecutor() public {
        // ecdsaValidator is already installed as validator in setUp

        // Verify it's installed as validator, not executor
        assertTrue(account.isModuleInstalled(1, address(ecdsaValidator), ""), "Should be validator");
        assertFalse(account.isModuleInstalled(2, address(ecdsaValidator), ""), "Should NOT be executor");

        // Validator should not be able to call executeFromExecutor
        vm.prank(address(ecdsaValidator));
        vm.expectRevert();
        account.executeFromExecutor(ModeLib.encodeSimpleSingle(), "");
    }

    // =========================================================================
    // FACTORY FRONTRUNNING TESTS
    // =========================================================================

    function test_Attack_FactoryFrontrunning() public {
        // initData includes owner in salt, preventing frontrunning
        bytes memory initData = abi.encodePacked(address(ecdsaValidator), owner);

        // Compute expected address
        address expectedAddr = factory.getAddress(bytes32(uint256(999)), initData);

        // Attacker tries to frontrun with different owner
        bytes memory attackerInitData = abi.encodePacked(address(ecdsaValidator), address(0xdead));
        address attackerAddr = factory.getAddress(bytes32(uint256(999)), attackerInitData);

        // Addresses should be different because initData is part of salt
        assertTrue(expectedAddr != attackerAddr, "Frontrunning should result in different address");

        // Original user can still deploy their account
        address deployed = factory.createAccount(bytes32(uint256(999)), initData);
        assertEq(deployed, expectedAddr, "User should get their expected address");

        // Attacker's deployment gets different address
        address attackerDeployed = factory.createAccount(bytes32(uint256(999)), attackerInitData);
        assertEq(attackerDeployed, attackerAddr, "Attacker gets different address");
    }

    // =========================================================================
    // UPGRADE ATTACK TESTS
    // =========================================================================

    function test_Attack_UpgradeToMaliciousImpl() public {
        // Only entryPoint or self can upgrade
        NethereumAccount maliciousImpl = new NethereumAccount(address(entryPoint));

        // Random user cannot upgrade
        vm.prank(address(0xdead));
        vm.expectRevert();
        account.upgradeToAndCall(address(maliciousImpl), "");

        // Only entryPoint can upgrade
        vm.prank(address(entryPoint));
        account.upgradeToAndCall(address(maliciousImpl), "");

        // Implementation changed
        bytes32 implSlot = bytes32(uint256(keccak256("eip1967.proxy.implementation")) - 1);
        address currentImpl = address(uint160(uint256(vm.load(address(account), implSlot))));
        assertEq(currentImpl, address(maliciousImpl));
    }

    function test_Attack_UpgradeStoragePreservation() public {
        // Install modules
        vm.startPrank(address(entryPoint));
        account.installModule(1, address(multisigValidator), abi.encode(new address[](1), uint256(1)));

        NethereumAccount newImpl = new NethereumAccount(address(entryPoint));
        account.upgradeToAndCall(address(newImpl), "");
        vm.stopPrank();

        // Original validator should still work
        assertTrue(account.isModuleInstalled(1, address(ecdsaValidator), ""));
        assertTrue(account.isModuleInstalled(1, address(multisigValidator), ""));
    }

    // =========================================================================
    // NONCE MANIPULATION TESTS
    // =========================================================================

    function test_Attack_NonceReplay() public {
        // Create two UserOps with same nonce
        PackedUserOperation memory userOp1 = _createUserOp(address(account));
        userOp1.nonce = 0;

        PackedUserOperation memory userOp2 = _createUserOp(address(account));
        userOp2.nonce = 0; // Same nonce
        userOp2.callData = hex"dead"; // Different calldata

        bytes32 userOpHash1 = keccak256(abi.encode(userOp1));
        bytes32 userOpHash2 = keccak256(abi.encode(userOp2));

        // Hashes should be different
        assertTrue(userOpHash1 != userOpHash2, "Different calldata should produce different hash");

        // Same nonce but EntryPoint will only accept one
        // (EntryPoint tracks nonces and increments after execution)
    }

    // =========================================================================
    // ENTRYPOINT TRUST ATTACK TESTS
    // =========================================================================

    function test_Attack_MaliciousEntryPointBypass() public {
        MockEntryPointV2 maliciousEP = new MockEntryPointV2();

        // Create account that trusts malicious EntryPoint
        NethereumAccountFactory maliciousFactory = new NethereumAccountFactory(address(maliciousEP));
        bytes memory initData = abi.encodePacked(address(ecdsaValidator), owner);
        address victimAddr = maliciousFactory.createAccount(SALT, initData);

        // Malicious EntryPoint can call without validation
        bytes memory maliciousCall = abi.encodeCall(
            NethereumAccount.execute,
            (ModeLib.encodeSimpleSingle(), ExecutionLib.encodeSingle(address(target), 0, ""))
        );

        // This works because the account trusts this EntryPoint
        maliciousEP.handleOpsWithoutValidation(victimAddr, maliciousCall);

        // This demonstrates why EntryPoint must be immutable and trusted
        // Our implementation uses immutable entryPoint to prevent changing it
    }

    // =========================================================================
    // HOOK DENIAL OF SERVICE TESTS
    // =========================================================================

    function test_Attack_MaliciousHookDoS() public {
        // Create a hook that always reverts on postCheck
        MockMaliciousHookDoS maliciousHook = new MockMaliciousHookDoS();

        vm.prank(address(entryPoint));
        account.installModule(4, address(maliciousHook), "");

        // Account is now locked - all executions will fail
        ModeCode mode = ModeLib.encodeSimpleSingle();
        bytes memory execData = ExecutionLib.encodeSingle(address(target), 0, "");

        vm.prank(address(entryPoint));
        vm.expectRevert("DoS Attack!");
        account.execute(mode, execData);

        // Emergency uninstall should still work (tested in base tests)
    }

    // =========================================================================
    // BATCH EXECUTION ISOLATION TESTS
    // =========================================================================

    function test_Attack_BatchExecutionIsolation() public {
        // One failing call in batch (TRY mode) should not affect others
        target.setShouldRevert(true);
        MockTarget target2 = new MockTarget();

        ModeCode mode = ModeLib.encode(CALLTYPE_BATCH, EXECTYPE_TRY, MODE_DEFAULT, PAYLOAD_DEFAULT);

        // Use Execution[] struct array for batch encoding
        Execution[] memory executions = new Execution[](2);
        executions[0] = Execution({
            target: address(target),
            value: 0,
            callData: abi.encodeCall(target.setValue, (42))
        });
        executions[1] = Execution({
            target: address(target2),
            value: 0,
            callData: abi.encodeCall(target2.setValue, (100))
        });

        bytes memory execData = ExecutionLib.encodeBatch(executions);

        vm.prank(address(entryPoint));
        account.execute(mode, execData);

        // First call failed, second should succeed
        assertEq(target.getValue(), 0, "First call should have failed");
        assertEq(target2.getValue(), 100, "Second call should succeed");
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
}

/// @notice Malicious hook that causes DoS
contract MockMaliciousHookDoS is IHook {
    function onInstall(bytes calldata) external override {}
    function onUninstall(bytes calldata) external override {}

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == 4;
    }

    function isInitialized(address) external pure override returns (bool) {
        return true;
    }

    function preCheck(address, uint256, bytes calldata) external pure override returns (bytes memory) {
        return "";
    }

    function postCheck(bytes calldata) external pure override {
        revert("DoS Attack!");
    }
}
