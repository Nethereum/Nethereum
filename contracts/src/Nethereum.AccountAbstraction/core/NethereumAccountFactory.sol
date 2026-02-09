// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {NethereumAccount} from "./NethereumAccount.sol";
import "@openzeppelin/contracts/proxy/ERC1967/ERC1967Proxy.sol";

// =============================================================================
// SECURITY VALIDATION: Account Factory
// =============================================================================
// Pattern Sources:
//   - Nexus (Biconomy): NexusAccountFactory.sol
//   - Safe7579 (Rhinestone): SafeProxyFactory + Safe7579Launchpad
//   - Kernel v3 (ZeroDev): KernelFactory
//   - eth-infinitism: SimpleAccountFactory
//
// SECURITY REQUIREMENTS:
//   1. CREATE2 for deterministic addresses
//   2. Address derivation MUST match actual deployment
//   3. idempotent createAccount (return existing if deployed)
//   4. MUST NOT allow frontrunning of account creation
//
// AUDIT FINDINGS ADDRESSED:
//   - Safe7579 Critical: Counterfactual address can be stolen via frontrunning
//     Solution: initData is part of salt computation (changes address)
//   - Nexus H-03: Registry before install
//     Solution: Factory initializes account which sets up modules
//
// ENTRYPOINT VERSION:
//   Factory deploys accounts for a SPECIFIC EntryPoint version.
//   To support new EntryPoint: Deploy new factory with new implementation.
// =============================================================================

/// @title NethereumAccountFactory
/// @notice Factory for deploying NethereumAccount proxies using CREATE2
/// @dev Deterministic addresses based on salt + initData
contract NethereumAccountFactory {
    // =========================================================================
    // IMMUTABLE STATE
    // =========================================================================

    /// @notice The account implementation address
    /// @dev All proxies delegate to this implementation
    address public immutable accountImplementation;

    /// @notice The EntryPoint this factory creates accounts for
    /// @dev Exposed for verification - matches implementation's entryPoint
    address public immutable entryPoint;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// @notice Emitted when a new account is created
    event AccountCreated(
        address indexed account,
        address indexed initialValidator,
        bytes32 indexed salt
    );

    // =========================================================================
    // ERRORS
    // =========================================================================

    error AccountCreationFailed();
    error InvalidEntryPoint();
    error InvalidInitData();

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    /// @notice Creates a new factory
    /// @param _entryPoint The ERC-4337 EntryPoint address
    /// @dev Deploys the account implementation in constructor
    /// @dev SECURITY: Implementation is created once and shared by all proxies
    constructor(address _entryPoint) {
        if (_entryPoint == address(0)) revert InvalidEntryPoint();

        entryPoint = _entryPoint;

        // Deploy the account implementation
        // All proxies will delegatecall to this implementation
        accountImplementation = address(new NethereumAccount(_entryPoint));
    }

    // =========================================================================
    // ACCOUNT CREATION
    // =========================================================================

    /// @notice Creates a new account using CREATE2
    /// @param salt User-provided salt for address derivation
    /// @param initData Initialization data: validator(20 bytes) + validatorInitData
    /// @return account The created (or existing) account address
    /// @dev SECURITY: initData is part of salt computation (prevents frontrunning)
    /// @dev Idempotent: Returns existing account if already deployed
    function createAccount(
        bytes32 salt,
        bytes calldata initData
    ) external payable returns (address account) {
        // SECURITY: initData must contain at least validator address
        if (initData.length < 20) revert InvalidInitData();

        // Compute actual salt (includes initData to prevent frontrunning)
        bytes32 actualSalt = _computeSalt(salt, initData);

        // Get deterministic address
        account = _computeAddress(actualSalt, initData);

        // Check if already deployed
        if (account.code.length > 0) {
            // Already deployed - return existing
            return account;
        }

        // Deploy ERC1967 proxy pointing to implementation
        // Pattern from OpenZeppelin - standard UUPS proxy
        account = address(
            new ERC1967Proxy{salt: actualSalt, value: msg.value}(
                accountImplementation,
                abi.encodeCall(NethereumAccount.initializeAccount, (initData))
            )
        );

        // SECURITY: Verify deployment succeeded
        if (account.code.length == 0) revert AccountCreationFailed();

        // Extract validator for event
        address validator = address(bytes20(initData[:20]));
        emit AccountCreated(account, validator, salt);
    }

    // =========================================================================
    // ADDRESS COMPUTATION
    // =========================================================================

    /// @notice Gets the deterministic address for an account
    /// @param salt User-provided salt
    /// @param initData Initialization data
    /// @return The address the account will have (or already has)
    /// @dev SECURITY: Same computation as createAccount for consistency
    function getAddress(
        bytes32 salt,
        bytes calldata initData
    ) external view returns (address) {
        bytes32 actualSalt = _computeSalt(salt, initData);
        return _computeAddress(actualSalt, initData);
    }

    /// @notice Checks if an account is already deployed
    /// @param salt User-provided salt
    /// @param initData Initialization data
    /// @return True if account exists at computed address
    function isDeployed(
        bytes32 salt,
        bytes calldata initData
    ) external view returns (bool) {
        bytes32 actualSalt = _computeSalt(salt, initData);
        address account = _computeAddress(actualSalt, initData);
        return account.code.length > 0;
    }

    // =========================================================================
    // INITCODE GENERATION
    // =========================================================================

    /// @notice Gets initCode for UserOperation
    /// @param salt User-provided salt
    /// @param initData Initialization data
    /// @return initCode Factory address (20 bytes) + createAccount calldata
    /// @dev Used in UserOperation.initCode for first transaction
    function getInitCode(
        bytes32 salt,
        bytes calldata initData
    ) external view returns (bytes memory) {
        return abi.encodePacked(
            address(this),
            abi.encodeCall(this.createAccount, (salt, initData))
        );
    }

    // =========================================================================
    // INTERNAL HELPERS
    // =========================================================================

    /// @notice Computes the actual CREATE2 salt
    /// @param salt User-provided salt
    /// @param initData Initialization data
    /// @return The salt used for CREATE2
    /// @dev SECURITY: Including initData prevents frontrunning attacks
    /// @dev Attacker cannot claim your address by deploying first with same salt
    function _computeSalt(
        bytes32 salt,
        bytes calldata initData
    ) internal pure returns (bytes32) {
        return keccak256(abi.encodePacked(salt, keccak256(initData)));
    }

    /// @notice Computes the deterministic address
    /// @param actualSalt The computed salt (from _computeSalt)
    /// @param initData The initialization data (needed for bytecode hash)
    /// @return The CREATE2 address
    function _computeAddress(
        bytes32 actualSalt,
        bytes calldata initData
    ) internal view returns (address) {
        // ERC1967Proxy constructor: (implementation, _data)
        // _data is delegatecalled to implementation after deployment
        bytes memory initCallData = abi.encodeCall(
            NethereumAccount.initializeAccount,
            (initData)
        );

        bytes32 bytecodeHash = keccak256(abi.encodePacked(
            type(ERC1967Proxy).creationCode,
            abi.encode(accountImplementation, initCallData)
        ));

        return address(uint160(uint256(keccak256(abi.encodePacked(
            bytes1(0xff),
            address(this),
            actualSalt,
            bytecodeHash
        )))));
    }
}

// =============================================================================
// ALTERNATIVE: Minimal Proxy (EIP-1167) Factory
// =============================================================================
// For cheaper deployments, use EIP-1167 minimal proxy instead of ERC1967
// Trade-off: Slightly cheaper deploy, but no upgrade slot in proxy
// UUPS still works because upgrade logic is in implementation
// =============================================================================

/// @title NethereumAccountFactoryMinimal
/// @notice Factory using EIP-1167 minimal proxy (cheaper deployment)
/// @dev Use this for gas-optimized deployments
contract NethereumAccountFactoryMinimal {
    address public immutable accountImplementation;
    address public immutable entryPoint;

    event AccountCreated(address indexed account, address indexed initialValidator, bytes32 indexed salt);

    error AccountCreationFailed();
    error InvalidEntryPoint();
    error InvalidInitData();

    constructor(address _entryPoint) {
        if (_entryPoint == address(0)) revert InvalidEntryPoint();
        entryPoint = _entryPoint;
        accountImplementation = address(new NethereumAccount(_entryPoint));
    }

    /// @notice Creates account using EIP-1167 minimal proxy
    /// @dev ~45k gas cheaper than ERC1967Proxy
    function createAccount(
        bytes32 salt,
        bytes calldata initData
    ) external payable returns (address account) {
        if (initData.length < 20) revert InvalidInitData();

        bytes32 actualSalt = _computeSalt(salt, initData);
        account = _computeAddress(actualSalt);

        if (account.code.length > 0) {
            return account;
        }

        // Deploy EIP-1167 minimal proxy
        account = _deployMinimalProxy(actualSalt);

        if (account.code.length == 0) revert AccountCreationFailed();

        // Initialize the account
        NethereumAccount(payable(account)).initializeAccount(initData);

        // Forward any ETH sent to the account
        if (msg.value > 0) {
            (bool success,) = account.call{value: msg.value}("");
            require(success, "ETH transfer failed");
        }

        address validator = address(bytes20(initData[:20]));
        emit AccountCreated(account, validator, salt);
    }

    function getAddress(bytes32 salt, bytes calldata initData) external view returns (address) {
        return _computeAddress(_computeSalt(salt, initData));
    }

    function isDeployed(bytes32 salt, bytes calldata initData) external view returns (bool) {
        return _computeAddress(_computeSalt(salt, initData)).code.length > 0;
    }

    function getInitCode(bytes32 salt, bytes calldata initData) external view returns (bytes memory) {
        return abi.encodePacked(address(this), abi.encodeCall(this.createAccount, (salt, initData)));
    }

    function _computeSalt(bytes32 salt, bytes calldata initData) internal pure returns (bytes32) {
        return keccak256(abi.encodePacked(salt, keccak256(initData)));
    }

    function _computeAddress(bytes32 actualSalt) internal view returns (address) {
        bytes32 bytecodeHash = keccak256(_getMinimalProxyBytecode());
        return address(uint160(uint256(keccak256(abi.encodePacked(
            bytes1(0xff),
            address(this),
            actualSalt,
            bytecodeHash
        )))));
    }

    /// @notice EIP-1167 minimal proxy bytecode
    /// @dev 45 bytes: 3d602d80600a3d3981f3363d3d373d3d3d363d73<impl>5af43d82803e903d91602b57fd5bf3
    function _getMinimalProxyBytecode() internal view returns (bytes memory) {
        return abi.encodePacked(
            hex"3d602d80600a3d3981f3363d3d373d3d3d363d73",
            accountImplementation,
            hex"5af43d82803e903d91602b57fd5bf3"
        );
    }

    function _deployMinimalProxy(bytes32 salt) internal returns (address proxy) {
        bytes memory bytecode = _getMinimalProxyBytecode();

        /// @solidity memory-safe-assembly
        assembly {
            proxy := create2(0, add(bytecode, 0x20), mload(bytecode), salt)
        }
    }
}
