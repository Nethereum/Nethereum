// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.28;

// Contracts
import { ERC7579HybridValidatorBase } from "modulekit/Modules.sol";

// Types
import { PackedUserOperation } from "modulekit/external/ERC4337.sol";

// Libraries
import { SignatureCheckerLib } from "solady/utils/SignatureCheckerLib.sol";
import { EnumerableSet } from "@erc7579/enumerablemap4337/EnumerableSet4337.sol";
import { CheckSignatures } from "checknsignatures/CheckNSignatures.sol";
import { ECDSA } from "solady/utils/ECDSA.sol";
import { WebAuthn } from "@webauthn/WebAuthn.sol";
import { LibSort } from "solady/utils/LibSort.sol";
import {
    MODULE_TYPE_STATELESS_VALIDATOR as TYPE_STATELESS_VALIDATOR
} from "modulekit/module-bases/utils/ERC7579Constants.sol";

/// @title WebAuthnValidator
/// @author Based on Rhinestone's OwnableValidator
/// @notice A validator module that enables WebAuthn (passkey) authentication with threshold support
/// @dev Module allows smart accounts to authenticate using one or more WebAuthn credentials
///     (passkeys) with support for M-of-N threshold signatures.
contract WebAuthnValidator is ERC7579HybridValidatorBase {
    /*//////////////////////////////////////////////////////////////
                               LIBRARIES
    //////////////////////////////////////////////////////////////*/

    using EnumerableSet for EnumerableSet.Bytes32Set;
    using WebAuthn for bytes;
    using LibSort for bytes32[];

    /*//////////////////////////////////////////////////////////////
                                STRUCTS
    //////////////////////////////////////////////////////////////*/

    /// @notice Structure holding WebAuthn credential information
    /// @dev Maps a credential ID to its public key and verification requirements
    /// @param pubKeyX The X coordinate of the credential's public key on the P-256 curve
    /// @param pubKeyY The Y coordinate of the credential's public key on the P-256 curve
    /// @param requireUV Whether user verification (biometrics/PIN) is required for this credential
    struct WebAuthnCredential {
        uint256 pubKeyX;
        uint256 pubKeyY;
        bool requireUV;
    }

    /// @notice WebAuthVerificationContext
    /// @dev Context for WebAuthn verification, including credential details and threshold
    /// @param usePrecompile Whether to use the RIP7212 precompile for signature verification,
    ///                      or fallback to FreshCryptoLib. According to ERC-7562, calling the
    ///                      precompile is only allowed on networks that support it.
    /// @param threshold The number of signatures required for validation
    /// @param credentialIds The IDs of the credentials used for signing
    /// @param credential data WebAuthn credential data
    struct WebAuthVerificationContext {
        bool usePrecompile;
        uint256 threshold;
        bytes32[] credentialIds;
        WebAuthnCredential[] credentialData;
    }

    /*//////////////////////////////////////////////////////////////
                                 EVENTS
    //////////////////////////////////////////////////////////////*/

    /// @notice Emitted when the module is installed for an account
    event ModuleInitialized(address indexed account);

    /// @notice Emitted when the module is uninstalled for an account
    event ModuleUninitialized(address indexed account);

    /// @notice Emitted when a threshold is set for an account
    /// @param account The address of the smart account
    /// @param threshold The new threshold value
    event ThresholdSet(address indexed account, uint256 threshold);

    /// @notice Emitted when a credential is added to an account
    /// @param account The address of the smart account
    /// @param credentialId The ID of the added credential
    /// @param credential The WebAuthn credential
    event CredentialAdded(
        address indexed account, bytes32 indexed credentialId, WebAuthnCredential credential
    );

    /// @notice Emitted when a credential is removed from an account
    /// @param account The address of the smart account
    /// @param credentialId The ID of the removed credential
    event CredentialRemoved(address indexed account, bytes32 indexed credentialId);

    /*//////////////////////////////////////////////////////////////
                                 ERRORS
    //////////////////////////////////////////////////////////////*/

    /// @notice Thrown when attempting to set a zero threshold
    error ThresholdNotSet();

    /// @notice Thrown when a threshold is invalid (e.g., higher than credential count)
    error InvalidThreshold();

    /// @notice Thrown when credential IDs are not unique
    error NotUnique();

    /// @notice Thrown when credential IDs are not sorted
    error NotSorted();

    /// @notice Thrown when attempting to add more credentials than the maximum allowed
    error MaxCredentialsReached();

    /// @notice Thrown when an invalid credential ID is provided
    /// @param credentialId The ID of the invalid credential
    error InvalidCredential(bytes32 credentialId);

    /// @notice Thrown when the trying to remove a credential would make the threshold unreachable
    error CannotRemoveCredential();

    /// @notice Thrown when an invalid public key is provided
    error InvalidPublicKey();

    /// @notice Thrown when a credential already exists
    error CredentialAlreadyExists();

    /*//////////////////////////////////////////////////////////////
                               CONSTANTS
    //////////////////////////////////////////////////////////////*/

    /// @notice Maximum number of credentials allowed per account
    uint256 constant MAX_CREDENTIALS = 32;

    /*//////////////////////////////////////////////////////////////
                                 STATE
    //////////////////////////////////////////////////////////////*/

    /// @notice Enumerable set of enabled credentials per account
    EnumerableSet.Bytes32Set credentials;

    /// @notice Mapping of credential IDs to their respective WebAuthn credentials
    mapping(bytes32 credentialId => mapping(address account => WebAuthnCredential credential))
        public credentialDetails;

    /// @notice Threshold for each account
    mapping(address account => uint256 threshold) public threshold;

    /*//////////////////////////////////////////////////////////////
                                 CONFIG
    //////////////////////////////////////////////////////////////*/

    /// @notice Initializes the module with WebAuthn credentials
    /// @dev Installs the validator with threshold and initial set of credentials
    /// @param data Encoded as: abi.encode(threshold, pubKeysX, pubKeysY, requireUVs)
    function onInstall(bytes calldata data) external override {
        // Decode the credential data
        (uint256 _threshold, WebAuthnCredential[] memory _credentials) =
            abi.decode(data, (uint256, WebAuthnCredential[]));

        // Cache the length of the credentials
        uint256 credentialLength = _credentials.length;

        // Make sure the threshold is set
        if (_threshold == 0) {
            revert ThresholdNotSet();
        }

        // Make sure the threshold is valid
        if (credentialLength < _threshold) {
            revert InvalidThreshold();
        }

        // Check if max credentials is reached
        if (credentialLength > MAX_CREDENTIALS) {
            revert MaxCredentialsReached();
        }

        // Cache the account address
        address account = msg.sender;

        // Set threshold
        threshold[account] = _threshold;

        // Generate credential IDs and store credentials
        bytes32 credentialId;

        for (uint256 i = 0; i < credentialLength; i++) {
            // Check public key is valid
            if (_credentials[i].pubKeyX == 0 || _credentials[i].pubKeyY == 0) {
                revert InvalidPublicKey();
            }

            // Generate deterministic credential ID
            bytes32 credId = generateCredentialId(_credentials[i].pubKeyX, _credentials[i].pubKeyY);

            // Store the credential
            credentialDetails[credId][account] = WebAuthnCredential({
                pubKeyX: _credentials[i].pubKeyX,
                pubKeyY: _credentials[i].pubKeyY,
                requireUV: _credentials[i].requireUV
            });

            // Add credential ID to the set
            bool isUnique = credentials.add(account, credId);
            if (!isUnique) {
                revert NotUnique();
            }

            // Emit event
            emit CredentialAdded(account, credId, _credentials[i]);

            // Cache the credential ID
            require(credentialId < credId, NotSorted());
            credentialId = credId;
        }

        emit ModuleInitialized(account);
    }

    /// @notice Handles the uninstallation of the module and clears all credentials
    /// @dev Removes all credentials and settings for the account
    function onUninstall(bytes calldata) external override {
        // Cache the account address
        address account = msg.sender;

        // Get all credentials to clear mappings
        bytes32[] memory credentialsIds = getCredentialIds(account);

        // Clear the credentials mapping
        for (uint256 i = 0; i < credentialsIds.length; i++) {
            delete credentialDetails[credentialsIds[i]][account];
        }

        // Remove all credentials from the set
        credentials.removeAll(account);

        // Remove the threshold
        threshold[account] = 0;

        emit ModuleUninitialized(account);
    }

    /// @notice Checks if the module is initialized for a smart account
    /// @param smartAccount Address of the smart account
    /// @return bool True if the module is initialized, false otherwise
    function isInitialized(address smartAccount) public view returns (bool) {
        return threshold[smartAccount] != 0;
    }

    /// @notice Sets the threshold for the account
    /// @dev Updates how many signatures are required to validate operations
    /// @param _threshold Number of required signatures
    function setThreshold(uint256 _threshold) external {
        // Cache the account address
        address account = msg.sender;

        // Check if the module is initialized
        if (!isInitialized(account)) revert NotInitialized(account);

        // Make sure the threshold is set
        if (_threshold == 0) {
            revert InvalidThreshold();
        }

        // Make sure the threshold is less than the number of credentials
        if (credentials.length(account) < _threshold) {
            revert InvalidThreshold();
        }

        // Set the threshold
        threshold[account] = _threshold;

        emit ThresholdSet(account, _threshold);
    }

    /// @notice Adds a WebAuthn credential to the account
    /// @dev Registers a new passkey for authentication
    /// @param pubKeyX X coordinate of the credential's public key
    /// @param pubKeyY Y coordinate of the credential's public key
    /// @param requireUV Whether user verification (biometrics/PIN) is required
    function addCredential(uint256 pubKeyX, uint256 pubKeyY, bool requireUV) external {
        // Cache the account address
        address account = msg.sender;

        // Check if the module is initialized
        if (!isInitialized(account)) revert NotInitialized(account);

        // Verify public key is valid
        if (pubKeyX == 0 || pubKeyY == 0) {
            revert InvalidPublicKey();
        }

        // Generate deterministic credential ID
        bytes32 credentialId = generateCredentialId(pubKeyX, pubKeyY);

        // Check if max credentials is reached
        if (credentials.length(account) >= MAX_CREDENTIALS) {
            revert MaxCredentialsReached();
        }

        // Store the credential
        credentialDetails[credentialId][account] =
            WebAuthnCredential({ pubKeyX: pubKeyX, pubKeyY: pubKeyY, requireUV: requireUV });

        // Add the credential ID to the set
        bool isUnique = credentials.add(account, credentialId);
        if (!isUnique) {
            revert CredentialAlreadyExists();
        }

        emit CredentialAdded(
            account,
            credentialId,
            WebAuthnCredential({ pubKeyX: pubKeyX, pubKeyY: pubKeyY, requireUV: requireUV })
        );
    }

    /// @notice Removes a WebAuthn credential from the account
    /// @dev De-registers a passkey and prevents it from being used for authentication
    /// @param pubKeyX X coordinate of the credential's public key
    /// @param pubKeyY Y coordinate of the public key
    function removeCredential(uint256 pubKeyX, uint256 pubKeyY) external {
        // Cache the account address
        address account = msg.sender;

        // Check if the module is initialized
        if (!isInitialized(account)) revert NotInitialized(account);

        // Generate deterministic credential ID
        bytes32 credentialId = generateCredentialId(pubKeyX, pubKeyY);

        // Check if removing would break threshold
        if (credentials.length(account) <= threshold[account]) {
            revert CannotRemoveCredential();
        }

        // Remove the credential from the set
        bool wasRemoved = credentials.remove(account, credentialId);
        if (!wasRemoved) {
            revert InvalidCredential(credentialId);
        }

        // Delete from the credentials mapping
        delete credentialDetails[credentialId][account];

        emit CredentialRemoved(account, credentialId);
    }

    /// @notice Returns the credential IDs of the account
    /// @dev Gets all registered credential IDs for an account
    /// @param account Address of the account
    /// @return credentialsIds Array of credential IDs
    function getCredentialIds(address account)
        public
        view
        returns (bytes32[] memory credentialsIds)
    {
        return credentials.values(account);
    }

    /// @notice Returns the number of credentials for an account
    /// @param account Address of the account
    /// @return count Count of credentials
    function getCredentialCount(address account) external view returns (uint256 count) {
        return credentials.length(account);
    }

    /// @notice Checks if a credential exists for an account
    /// @dev Verifies if a specific credential is registered using its parameters
    /// @param pubKeyX X coordinate of the credential's public key
    /// @param pubKeyY Y coordinate of the credential's public key
    /// @param account Address of the account to check
    /// @return exists Boolean indicating whether the credential exists
    function hasCredential(
        uint256 pubKeyX,
        uint256 pubKeyY,
        address account
    )
        external
        view
        returns (bool exists)
    {
        bytes32 credentialId = generateCredentialId(pubKeyX, pubKeyY);
        return credentials.contains(account, credentialId);
    }

    /// @notice Checks if a credential exists for an account by its ID
    /// @dev Verifies if a specific credential ID is registered
    /// @param credentialId Credential ID to check
    /// @param account Address of the account to check
    /// @return exists Boolean indicating whether the credential exists
    function hasCredentialById(
        bytes32 credentialId,
        address account
    )
        external
        view
        returns (bool exists)
    {
        return credentials.contains(account, credentialId);
    }

    /// @dev Gets the public key and settings for a credential
    /// @param credentialId Credential ID to query
    /// @param account Address of the account
    /// @return pubKeyX X coordinate of the public key
    /// @return pubKeyY Y coordinate of the public key
    /// @return requireUV Whether user verification is required
    function getCredentialInfo(
        bytes32 credentialId,
        address account
    )
        external
        view
        returns (uint256 pubKeyX, uint256 pubKeyY, bool requireUV)
    {
        WebAuthnCredential memory cred = credentialDetails[credentialId][account];
        return (cred.pubKeyX, cred.pubKeyY, cred.requireUV);
    }

    function generateCredentialId(uint256 pubKeyX, uint256 pubKeyY) public pure returns (bytes32) {
        return keccak256(abi.encode(pubKeyX, pubKeyY));
    }

    /*//////////////////////////////////////////////////////////////
                                VALIDATE
    //////////////////////////////////////////////////////////////*/

    /// @notice Validates a user operation
    /// @dev Verifies WebAuthn signatures for ERC-4337 transactions
    /// @param userOp User operation to validate
    /// @param userOpHash Hash of the user operation
    /// @return ValidationData Result of validation (success or failure)
    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    )
        external
        view
        override
        returns (ValidationData)
    {
        // Validate the signature
        bool isValid = _validateSignatureWithConfig(userOp.sender, userOpHash, userOp.signature);

        // Return the result
        if (isValid) {
            return VALIDATION_SUCCESS;
        }
        return VALIDATION_FAILED;
    }

    /// @notice Validates an ERC-1271 signature with the sender
    /// @dev Implements EIP-1271 isValidSignature for smart contract signatures
    /// @param hash Hash of the data to validate
    /// @param data Signature data
    /// @return bytes4 EIP1271_SUCCESS if valid, EIP1271_FAILED otherwise
    function isValidSignatureWithSender(
        address,
        bytes32 hash,
        bytes calldata data
    )
        external
        view
        override
        returns (bytes4)
    {
        // Validate the signature
        bool isValid = _validateSignatureWithConfig(msg.sender, hash, data);

        // Return the result
        if (isValid) {
            return EIP1271_SUCCESS;
        }
        return EIP1271_FAILED;
    }

    /// @notice Validates a signature with external credential data
    /// @dev Used for stateless validation without pre-registered credentials
    /// @param hash Hash of the data to validate
    /// @param signature WebAuthn signature data
    /// @param data Encoded credential details and threshold
    /// @return bool True if the signature is valid, false otherwise
    function validateSignatureWithData(
        bytes32 hash,
        bytes calldata signature,
        bytes calldata data
    )
        external
        view
        override
        returns (bool)
    {
        // Decode the threshold and credentials from data
        (WebAuthVerificationContext memory context) = abi.decode(data, (WebAuthVerificationContext));
        // Make sure the credentials are unique and sorted
        require(context.credentialIds.isSortedAndUniquified(), NotSorted());

        // Decode signature
        // Format: abi.encode(WebAuthn.WebAuthnAuth[])
        WebAuthn.WebAuthnAuth[] memory auth = abi.decode(signature, (WebAuthn.WebAuthnAuth[]));

        // Check that arrays have matching lengths
        uint256 credentialsLength = context.credentialIds.length;
        if (credentialsLength != context.credentialData.length) {
            return false;
        }

        // Generate credentialId from each entry and verify that it matches the provided data
        for (uint256 i = 0; i < credentialsLength; ++i) {
            bytes32 expectedId = generateCredentialId(
                context.credentialData[i].pubKeyX, context.credentialData[i].pubKeyY
            );
            if (context.credentialIds[i] != expectedId) {
                return false;
            }
        }

        // Check that threshold is valid
        if (context.threshold == 0 || context.threshold > credentialsLength) {
            return false;
        }

        // Verify WebAuthn signatures
        return _verifyWebAuthnSignatures(hash, auth, context);
    }

    /*//////////////////////////////////////////////////////////////
                                INTERNAL
    //////////////////////////////////////////////////////////////*/

    /// @notice Validates a signature against the account's credentials
    /// @dev Internal function to verify WebAuthn signatures against registered keys
    /// @param account Address of the account
    /// @param hash Hash of the data to verify
    /// @param data Signature data
    /// @return bool True if signature is valid and meets threshold, false otherwise
    function _validateSignatureWithConfig(
        address account,
        bytes32 hash,
        bytes calldata data
    )
        internal
        view
        returns (bool)
    {
        // Get the threshold
        uint256 _threshold = threshold[account];
        if (_threshold == 0) {
            return false;
        }

        // Get credential IDs from data
        // Format: abi.encode(bytes32[], bool, bytes)
        (bytes32[] memory credIds, bool usePrecompile, WebAuthn.WebAuthnAuth[] memory auth) =
            abi.decode(data, (bytes32[], bool, WebAuthn.WebAuthnAuth[]));

        // Make sure the credential IDs are unique and sorted
        require(credIds.isSortedAndUniquified(), NotSorted());

        // Prepare WebAuthnCredential array
        WebAuthnCredential[] memory credentialData = new WebAuthnCredential[](credIds.length);

        // Populate credential data
        for (uint256 i; i < credIds.length; ++i) {
            credentialData[i] = credentialDetails[credIds[i]][account];
        }

        // Set up the verification context
        WebAuthVerificationContext memory context = WebAuthVerificationContext({
            usePrecompile: usePrecompile,
            threshold: _threshold,
            credentialIds: credIds,
            credentialData: credentialData
        });

        // Verify WebAuthn signatures
        return _verifyWebAuthnSignatures(hash, auth, context);
    }

    /// @dev Core signature verification logic
    /// @param hash Hash of the data to verify
    /// @param auth WebAuthn data containing signatures
    /// @param context Verification context containing credential details
    /// @return success Whether verification process completed successfully
    function _verifyWebAuthnSignatures(
        bytes32 hash,
        WebAuthn.WebAuthnAuth[] memory auth,
        WebAuthVerificationContext memory context
    )
        internal
        view
        returns (bool success)
    {
        // Cache lengths
        uint256 sigCount = auth.length;

        // Check number of signatures
        if (sigCount == 0 || sigCount < context.threshold) {
            return false;
        }

        // Track valid signatures
        uint256 validCount;

        // Verify each signature
        for (uint256 i; i < sigCount; ++i) {
            // Challenge is the hash to be signed
            bytes memory challenge = abi.encode(hash);

            // IMPORTANT:
            // **********************************************************************
            // * We assume here that signatures are ordered to match credential IDs *
            // **********************************************************************

            // Verify the signature against the credential at the same index
            bool valid = WebAuthn.verify(
                challenge,
                context.credentialData[i].requireUV,
                auth[i],
                context.credentialData[i].pubKeyX,
                context.credentialData[i].pubKeyY,
                context.usePrecompile
            );

            if (valid) {
                ++validCount;

                // Early return if threshold is met
                if (validCount >= context.threshold) {
                    return true;
                }
            }
        }

        // If we reach here, we didn't meet the threshold
        return false;
    }

    /*//////////////////////////////////////////////////////////////
                                METADATA
    //////////////////////////////////////////////////////////////*/

    /// @notice Returns the type of the module
    /// @dev Implements interface to indicate validator capabilities
    /// @param typeID Type identifier to check
    /// @return bool True if this module supports the specified type
    function isModuleType(uint256 typeID) external pure override returns (bool) {
        return typeID == TYPE_VALIDATOR || typeID == TYPE_STATELESS_VALIDATOR;
    }

    /// @notice Returns the name of the module
    /// @dev Provides a human-readable identifier for the module
    /// @return string Module name
    function name() external pure virtual returns (string memory) {
        return "WebAuthnValidator";
    }

    /// @notice Returns the version of the module
    /// @dev Provides version information for compatibility checks
    /// @return string Semantic version of the module
    function version() external pure virtual returns (string memory) {
        return "1.0.0";
    }
}
