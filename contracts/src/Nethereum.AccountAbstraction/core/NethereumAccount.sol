// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {IAccount} from "../interfaces/IAccount.sol";
import {PackedUserOperation} from "../interfaces/PackedUserOperation.sol";
import {
    IERC7579Account,
    IERC7579Execution,
    IERC7579ModuleConfig,
    IERC7579AccountConfig,
    MODULE_TYPE_VALIDATOR,
    MODULE_TYPE_EXECUTOR,
    MODULE_TYPE_FALLBACK,
    MODULE_TYPE_HOOK,
    ERC1271_VALID,
    ERC1271_INVALID
} from "../interfaces/IERC7579Account.sol";
import {IValidator, IHook} from "../interfaces/IERC7579Module.sol";
import {ModeCode} from "../lib/ModeLib.sol";
import {ModuleManager} from "./ModuleManager.sol";
import {ExecutionManager} from "./ExecutionManager.sol";
import {AccountStorage, AccountStorageLib, FallbackHandler, FallbackCallType} from "./AccountStorage.sol";

import "@openzeppelin/contracts/proxy/utils/UUPSUpgradeable.sol";
import "@openzeppelin/contracts/proxy/utils/Initializable.sol";

// =============================================================================
// SECURITY VALIDATION: NethereumAccount
// =============================================================================
// Pattern Sources:
//   - Nexus (Biconomy): Nexus.sol
//   - Safe7579 (Rhinestone): Safe7579.sol
//   - Kernel v3 (ZeroDev): Kernel.sol
//
// ERC-4337 COMPLIANCE:
//   1. validateUserOp MUST only be callable by EntryPoint
//   2. MUST return SIG_VALIDATION_FAILED (1) on signature failure, NOT revert
//   3. MUST pay missingAccountFunds to EntryPoint
//   4. Nonce managed by EntryPoint (replay protection)
//
// ERC-7579 COMPLIANCE:
//   1. execute MUST only be callable by EntryPoint or self
//   2. executeFromExecutor MUST only be callable by installed executors
//   3. installModule/uninstallModule MUST only be callable by EntryPoint or self
//   4. MUST support module types 1-4
//
// UUPS UPGRADE SECURITY:
//   - Upgrade logic in implementation (cheaper proxies)
//   - _authorizeUpgrade protected by onlyEntryPointOrSelf
//   - Storage uses ERC-7201 (no collision on upgrade)
//
// ENTRYPOINT IMMUTABILITY:
//   - Pattern: ALL production implementations (Kernel, Nexus, Safe7579)
//   - WHY: Prevents EntryPoint tampering attacks
//   - HOW TO UPGRADE ENTRYPOINT: Deploy new implementation, upgrade proxy
// =============================================================================

/// @title NethereumAccount
/// @notice ERC-4337 + ERC-7579 compliant modular smart account
/// @dev UUPS upgradeable with ERC-7201 namespaced storage
contract NethereumAccount is
    IAccount,
    IERC7579Account,
    ModuleManager,
    ExecutionManager,
    UUPSUpgradeable,
    Initializable
{
    // =========================================================================
    // IMMUTABLE STATE
    // =========================================================================

    /// @notice The ERC-4337 EntryPoint - IMMUTABLE
    /// @dev Set in constructor, cannot be changed
    /// @dev SECURITY (Kernel/Nexus/Safe7579 pattern): Prevents EntryPoint tampering
    /// @dev To change EntryPoint: Deploy new implementation and upgrade
    address public immutable entryPoint;

    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// @notice Account implementation identifier per ERC-7579
    /// @dev Format: "vendorname.accountname.semver"
    string public constant ACCOUNT_ID = "nethereum.account.1.0.0";

    /// @notice ERC-4337 validation return values
    uint256 internal constant SIG_VALIDATION_SUCCESS = 0;
    uint256 internal constant SIG_VALIDATION_FAILED = 1;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// @notice Emitted when account is initialized
    event AccountInitialized(address indexed account, address indexed validator);

    // =========================================================================
    // ERRORS
    // =========================================================================

    error OnlyEntryPoint();
    error OnlyEntryPointOrSelf();
    error OnlyExecutorModule();
    error InvalidEntryPoint();
    error InvalidSignatureLength();
    error FallbackHandlerNotInstalled(bytes4 selector);

    // =========================================================================
    // MODIFIERS
    // =========================================================================

    /// @notice Restricts to EntryPoint only
    /// @dev SECURITY: validateUserOp MUST only be called by EntryPoint
    modifier onlyEntryPoint() {
        if (msg.sender != entryPoint) revert OnlyEntryPoint();
        _;
    }

    /// @notice Restricts to EntryPoint or self
    /// @dev SECURITY: execute, installModule, uninstallModule, upgrade
    modifier onlyEntryPointOrSelf() {
        if (msg.sender != entryPoint && msg.sender != address(this)) {
            revert OnlyEntryPointOrSelf();
        }
        _;
    }

    /// @notice Restricts to installed executor modules
    /// @dev SECURITY: executeFromExecutor only for executors
    modifier onlyExecutorModule() {
        if (!_isExecutorInstalled(msg.sender)) {
            revert OnlyExecutorModule();
        }
        _;
    }

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    /// @notice Creates a new account implementation
    /// @param _entryPoint The ERC-4337 EntryPoint address
    /// @dev SECURITY: EntryPoint is immutable - cannot be changed after deployment
    /// @dev SECURITY: _disableInitializers prevents initialization of implementation
    constructor(address _entryPoint) {
        if (_entryPoint == address(0)) revert InvalidEntryPoint();
        entryPoint = _entryPoint;

        // SECURITY: Prevent initialization of implementation contract
        // Pattern from OpenZeppelin - implementation should never be initialized
        _disableInitializers();
    }

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    /// @notice Initializes the account with a default validator
    /// @param initData ABI encoded: validator(20 bytes) + validatorInitData
    /// @dev Can only be called once per proxy instance
    /// @dev SECURITY: Uses OpenZeppelin Initializable for single-execution guard
    function initializeAccount(bytes calldata initData) external initializer {
        // Initialize module storage (SentinelLists)
        _initModuleManager();

        // SECURITY: Account MUST have at least one validator
        if (initData.length >= 20) {
            address validator = address(bytes20(initData[:20]));
            bytes calldata validatorInitData = initData[20:];

            // Install the initial validator
            _installModule(MODULE_TYPE_VALIDATOR, validator, validatorInitData);

            emit AccountInitialized(address(this), validator);
        }
    }

    // =========================================================================
    // ERC-4337: VALIDATE USER OPERATION
    // =========================================================================

    /// @notice Validates a UserOperation
    /// @param userOp The packed UserOperation
    /// @param userOpHash Hash of the UserOperation (includes EntryPoint + chainId)
    /// @param missingAccountFunds Amount to pay EntryPoint for gas
    /// @return validationData Packed: sigFailed (1 bit) + validUntil (48 bits) + validAfter (48 bits)
    /// @dev SECURITY: Only callable by EntryPoint (onlyEntryPoint modifier)
    /// @dev SECURITY: Returns SIG_VALIDATION_FAILED (1) on failure, does NOT revert
    /// @dev Signature format: validator(20 bytes) + validatorSignature
    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash,
        uint256 missingAccountFunds
    ) external onlyEntryPoint returns (uint256 validationData) {
        // SECURITY: Signature must contain validator address prefix
        if (userOp.signature.length < 20) {
            return SIG_VALIDATION_FAILED;
        }

        // Extract validator address from signature prefix
        // Pattern from Nexus/Safe7579: validator(20 bytes) + actual signature
        address validator = address(bytes20(userOp.signature[:20]));
        bytes calldata signature = userOp.signature[20:];

        // SECURITY: Verify validator is installed
        // MUST check before delegating validation
        if (!_isValidatorInstalled(validator)) {
            return SIG_VALIDATION_FAILED;
        }

        // Create modified userOp with stripped signature for validator
        PackedUserOperation memory modifiedUserOp = userOp;
        modifiedUserOp.signature = signature;

        // Delegate validation to the validator module
        // Validator returns: 0 = success, 1 = failed, or packed validAfter/validUntil
        validationData = IValidator(validator).validateUserOp(modifiedUserOp, userOpHash);

        // SECURITY: Pay prefund to EntryPoint AFTER validation
        // This ensures gas estimation works correctly
        if (missingAccountFunds > 0) {
            _payPrefund(missingAccountFunds);
        }
    }

    // =========================================================================
    // ERC-7579: EXECUTION
    // =========================================================================

    /// @notice Executes a transaction
    /// @param mode The encoded execution mode (CallType + ExecType + ...)
    /// @param executionCalldata The encoded execution data
    /// @dev SECURITY: Only callable by EntryPoint or self
    /// @dev Executes with optional hook pre/post checks
    function execute(
        ModeCode mode,
        bytes calldata executionCalldata
    ) external payable onlyEntryPointOrSelf {
        _executeWithHook(mode, executionCalldata);
    }

    /// @notice Executes from an installed executor module
    /// @param mode The encoded execution mode
    /// @param executionCalldata The encoded execution data
    /// @return returnData Array of return data from each execution
    /// @dev SECURITY: Only callable by installed executor modules
    function executeFromExecutor(
        ModeCode mode,
        bytes calldata executionCalldata
    ) external payable onlyExecutorModule returns (bytes[] memory returnData) {
        return _executeFromExecutorWithHook(mode, executionCalldata);
    }

    // =========================================================================
    // ERC-7579: MODULE CONFIGURATION
    // =========================================================================

    /// @notice Installs a module on the account
    /// @param moduleTypeId The type of module (1-4)
    /// @param module The module address
    /// @param initData Data for module initialization
    /// @dev SECURITY: Only callable by EntryPoint or self
    function installModule(
        uint256 moduleTypeId,
        address module,
        bytes calldata initData
    ) external payable onlyEntryPointOrSelf {
        _installModule(moduleTypeId, module, initData);
    }

    /// @notice Uninstalls a module from the account
    /// @param moduleTypeId The type of module
    /// @param module The module address
    /// @param deInitData Data for module de-initialization
    /// @dev SECURITY: Only callable by EntryPoint or self
    function uninstallModule(
        uint256 moduleTypeId,
        address module,
        bytes calldata deInitData
    ) external payable onlyEntryPointOrSelf {
        _uninstallModule(moduleTypeId, module, deInitData);
    }

    /// @notice Checks if a module is installed
    /// @param moduleTypeId The type of module
    /// @param module The module address
    /// @param additionalContext Additional context (e.g., selector for fallback)
    /// @return True if installed
    function isModuleInstalled(
        uint256 moduleTypeId,
        address module,
        bytes calldata additionalContext
    ) external view returns (bool) {
        return _isModuleInstalled(moduleTypeId, module, additionalContext);
    }

    // =========================================================================
    // ERC-7579: ACCOUNT CONFIGURATION
    // =========================================================================

    /// @notice Returns the account identifier
    /// @return The account ID in format "vendorname.accountname.semver"
    function accountId() external pure returns (string memory) {
        return ACCOUNT_ID;
    }

    /// @notice Checks if an execution mode is supported
    /// @param encodedMode The encoded execution mode
    /// @return True if supported
    function supportsExecutionMode(ModeCode encodedMode) external pure returns (bool) {
        return _supportsExecutionMode(encodedMode);
    }

    /// @notice Checks if a module type is supported
    /// @param moduleTypeId The module type ID
    /// @return True if supported (1-4)
    function supportsModule(uint256 moduleTypeId) external pure returns (bool) {
        return moduleTypeId >= MODULE_TYPE_VALIDATOR && moduleTypeId <= MODULE_TYPE_HOOK;
    }

    // =========================================================================
    // ERC-1271: SIGNATURE VALIDATION
    // =========================================================================

    /// @notice Validates a signature per ERC-1271
    /// @param hash The hash that was signed
    /// @param signature The signature (validator(20) + actual signature)
    /// @return magicValue 0x1626ba7e if valid, 0xffffffff if invalid
    /// @dev Pattern from Safe7579/Nexus: Delegates to installed validator
    function isValidSignature(
        bytes32 hash,
        bytes calldata signature
    ) external view returns (bytes4 magicValue) {
        // SECURITY: Signature must contain validator address
        if (signature.length < 20) {
            return ERC1271_INVALID;
        }

        // Extract validator from signature prefix
        address validator = address(bytes20(signature[:20]));
        bytes calldata validatorSignature = signature[20:];

        // SECURITY: Verify validator is installed
        if (!_isValidatorInstalled(validator)) {
            return ERC1271_INVALID;
        }

        // Delegate to validator's ERC-1271 method
        // isValidSignatureWithSender provides context about the caller
        return IValidator(validator).isValidSignatureWithSender(
            msg.sender,
            hash,
            validatorSignature
        );
    }

    // =========================================================================
    // DEPOSIT MANAGEMENT
    // =========================================================================

    /// @notice Deposits ETH to EntryPoint for gas
    function addDeposit() external payable {
        IEntryPointDeposit(entryPoint).depositTo{value: msg.value}(address(this));
    }

    /// @notice Withdraws deposit from EntryPoint
    /// @param to Recipient address
    /// @param amount Amount to withdraw
    /// @dev SECURITY: Only callable via UserOp (EntryPoint) or self
    function withdrawDepositTo(
        address payable to,
        uint256 amount
    ) external onlyEntryPointOrSelf {
        IEntryPointDeposit(entryPoint).withdrawTo(to, amount);
    }

    /// @notice Gets current deposit balance at EntryPoint
    function getDeposit() external view returns (uint256) {
        return IEntryPointDeposit(entryPoint).balanceOf(address(this));
    }

    /// @notice Gets current nonce from EntryPoint
    /// @param key The nonce key (for parallel nonces)
    function getNonce(uint192 key) external view returns (uint256) {
        return IEntryPointNonce(entryPoint).getNonce(address(this), key);
    }

    // =========================================================================
    // UUPS UPGRADE
    // =========================================================================

    /// @notice Authorizes an upgrade
    /// @param newImplementation Address of new implementation
    /// @dev SECURITY: Only callable via UserOp (EntryPoint) or self
    /// @dev SECURITY: Pattern from Nexus/Safe7579 - account controls its upgrades
    function _authorizeUpgrade(
        address newImplementation
    ) internal override onlyEntryPointOrSelf {
        // Additional checks can be added here:
        // - Timelock for upgrades
        // - Multi-sig approval
        // - Upgrade registry validation
        (newImplementation); // Silence unused warning
    }

    // =========================================================================
    // FALLBACK HANDLER
    // =========================================================================

    /// @notice Routes unknown function calls to fallback handlers
    /// @dev Pattern from Safe7579/Nexus: ERC-2771 compliant
    /// @dev SECURITY: Appends msg.sender to calldata for handler to verify
    fallback() external payable {
        FallbackHandler storage handler = _getFallbackHandler(msg.sig);

        if (handler.handler == address(0)) {
            revert FallbackHandlerNotInstalled(msg.sig);
        }

        address target = handler.handler;
        FallbackCallType calltype = handler.calltype;

        // ERC-2771: Append original msg.sender to calldata
        // Handler can extract via _msgSender() pattern
        bytes memory callData = abi.encodePacked(msg.data, msg.sender);

        /// @solidity memory-safe-assembly
        assembly {
            let success
            let returnSize

            switch calltype
            case 0 {
                // CALL
                // SECURITY (Nexus audit H-04): Forward msg.value
                success := call(
                    gas(),
                    target,
                    callvalue(),
                    add(callData, 0x20),
                    mload(callData),
                    0,
                    0
                )
            }
            case 1 {
                // STATICCALL
                success := staticcall(
                    gas(),
                    target,
                    add(callData, 0x20),
                    mload(callData),
                    0,
                    0
                )
            }
            case 2 {
                // DELEGATECALL
                // WARNING: Handler shares storage with account
                success := delegatecall(
                    gas(),
                    target,
                    add(callData, 0x20),
                    mload(callData),
                    0,
                    0
                )
            }

            returnSize := returndatasize()
            returndatacopy(0, 0, returnSize)

            if iszero(success) {
                revert(0, returnSize)
            }

            return(0, returnSize)
        }
    }

    /// @notice Receives ETH
    receive() external payable {}

    // =========================================================================
    // INTERNAL HELPERS
    // =========================================================================

    /// @notice Pays gas prefund to EntryPoint
    /// @dev Pattern from all implementations - inline assembly for gas efficiency
    function _payPrefund(uint256 missingAccountFunds) internal {
        /// @solidity memory-safe-assembly
        assembly {
            // Send ETH to EntryPoint (caller)
            // Ignore return value - EntryPoint handles failure
            pop(call(gas(), caller(), missingAccountFunds, 0, 0, 0, 0))
        }
    }

    // =========================================================================
    // STORAGE ACCESS OVERRIDE
    // =========================================================================

    /// @notice Resolves _getAccountStorage from both ModuleManager and ExecutionManager
    function _getAccountStorage()
        internal
        pure
        override(ModuleManager, ExecutionManager)
        returns (AccountStorage storage $)
    {
        return AccountStorageLib.getStorage();
    }
}

// =========================================================================
// ENTRYPOINT INTERFACES (minimal)
// =========================================================================

interface IEntryPointDeposit {
    function depositTo(address account) external payable;
    function withdrawTo(address payable withdrawAddress, uint256 withdrawAmount) external;
    function balanceOf(address account) external view returns (uint256);
}

interface IEntryPointNonce {
    function getNonce(address sender, uint192 key) external view returns (uint256);
}
