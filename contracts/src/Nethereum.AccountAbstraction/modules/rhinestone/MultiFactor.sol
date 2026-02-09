// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import { ERC7579ValidatorBase, ERC7484RegistryAdapter } from "modulekit/Modules.sol";
import { PackedUserOperation } from "modulekit/external/ERC4337.sol";
import { IStatelessValidator, IERC7484 } from "modulekit/Interfaces.sol";
import { MODULE_TYPE_VALIDATOR } from "modulekit/accounts/common/interfaces/IERC7579Module.sol";
import {
    Validator,
    SubValidatorConfig,
    MFAConfig,
    IterativeSubvalidatorRecord,
    ValidatorId
} from "./MultiFactorDataTypes.sol";
import { MultiFactorLib } from "./MultiFactorLib.sol";
import { FlatBytesLib } from "flatbytes/BytesLib.sol";
import { ECDSA } from "solady/utils/ECDSA.sol";

/**
 * @title MultiFactor
 * @dev A validator that multiplexes multiple other validators
 * @author Rhinestone
 */
contract MultiFactor is ERC7579ValidatorBase, ERC7484RegistryAdapter {
    using FlatBytesLib for *;

    /*//////////////////////////////////////////////////////////////////////////
                            CONSTANTS & STORAGE
    //////////////////////////////////////////////////////////////////////////*/

    error ZeroThreshold();
    error InvalidThreshold(uint256 length, uint256 threshold);
    error InvalidValidatorData();

    event ValidatorAdded(
        address indexed smartAccount, address indexed validator, ValidatorId id, uint256 iteration
    );
    event ValidatorRemoved(
        address indexed smartAccount, address indexed validator, ValidatorId id, uint256 iteration
    );
    event IterationIncreased(address indexed smartAccount, uint256 iteration);
    event ThesholdSet(address indexed smartAccount, uint8 threshold);

    // account => MFAConfig
    mapping(address account => MFAConfig config) public accountConfig;

    // iteration => subValidator => IterativeSubvalidatorRecord
    // this mapping is keyed on the iteration number so that it is easy and cheap to uninstall all
    // subvalidators by incrementing the iteration number
    mapping(
        uint256 iteration => mapping(address subValidator => IterativeSubvalidatorRecord record)
    ) internal iterationToSubValidator;

    constructor(IERC7484 _registry) ERC7484RegistryAdapter(_registry) { }

    /*//////////////////////////////////////////////////////////////////////////
                                     CONFIG
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Initializes the module with a threshold and a list of validators
     * @dev this function will revert if the module is already installed
     *
     * @param data the data to initialize the module with, formatted as abi.encodePacked(uint8
     * threshold, abi.encode(Validator[]))
     */
    function onInstall(bytes calldata data) external {
        // cache the account
        address account = msg.sender;
        // check if the module is already initialized and revert if it is
        if (isInitialized(account)) revert ModuleAlreadyInitialized(account);

        // unpack the threshold
        uint8 threshold = uint8(bytes1(data[:1]));
        // unpack the validators
        Validator[] calldata validators = MultiFactorLib.decode(data[1:]);

        // cache the validator length
        uint256 length = validators.length;
        // check if threshold is 0 and revert if it is
        if (threshold == 0) revert ZeroThreshold();
        // check if the length is less than the threshold and revert if it is
        if (length < threshold) revert InvalidThreshold(length, threshold);

        // get storage reference to account config
        MFAConfig storage $config = accountConfig[account];
        // cache the current iteration
        uint256 iteration = $config.iteration;

        if (length > type(uint8).max) revert InvalidValidatorData();
        $config.threshold = threshold;
        emit ThesholdSet(account, threshold);

        uint8 _validationLength;

        // iterate over the validators
        for (uint256 i; i < length; i++) {
            // cache the validator
            Validator calldata _validator = validators[i];

            // unpack the validator address and id
            // this data is packed to save calldata gas
            (address validatorAddress, ValidatorId id) =
                MultiFactorLib.unpack(_validator.packedValidatorAndId);

            // get storage reference to subValidator config
            FlatBytesLib.Bytes storage $validator = $subValidatorData({
                account: account,
                iteration: iteration,
                subValidator: validatorAddress,
                id: id
            });

            // check if the subValidator is an attested validator and revert if it is not
            REGISTRY.checkForAccount({
                smartAccount: account,
                module: validatorAddress,
                moduleType: MODULE_TYPE_VALIDATOR
            });
            // set the subValidator data
            $validator.store(_validator.data);

            if (_validator.data.length != 0) {
                _validationLength++;
            }

            // emit the ValidatorAdded event
            emit ValidatorAdded(account, validatorAddress, id, iteration);
        }

        $config.validationLength = _validationLength;
    }

    /**
     * Removes all subValidators when module is uninstalled
     * @dev this function will not revert if the module is not installed
     */
    function onUninstall(bytes calldata) external {
        // cache the account
        address account = msg.sender;
        // get storage reference to account config
        MFAConfig storage $config = accountConfig[account];

        // increment the iteration number
        uint256 _newIteration = $config.iteration + 1;
        $config.iteration = uint128(_newIteration);

        // delete the threshold & validationLength. these values are not part of the iterated
        // storage mapping
        delete $config.threshold;
        delete $config.validationLength;

        // emit the IterationIncreased event
        emit IterationIncreased(account, _newIteration);
    }

    /**
     * Checks if the module is initialized
     *
     * @param account the account to check
     *
     * @return true if the module is initialized, false otherwise
     */
    function isInitialized(address account) public view returns (bool) {
        // get storage reference to account config
        MFAConfig storage $config = accountConfig[account];
        // check if the threshold is not 0
        return $config.threshold != 0;
    }

    /**
     * Sets the threshold for the account
     * @dev this function does not check that the threshold is less than the number of validators
     * since this is infeasbile given the available data
     *
     * @param threshold the threshold to set
     */
    function setThreshold(uint8 threshold) external {
        // cache the account
        address account = msg.sender;
        // check if the module is initialized and revert if it is not
        if (!isInitialized(account)) revert NotInitialized(account);

        // get storage reference to account config
        MFAConfig storage $config = accountConfig[account];

        if ($config.validationLength < threshold) {
            revert InvalidThreshold($config.validationLength, threshold);
        }

        // check if threshold is 0 and revert if it is
        if (threshold == 0) revert ZeroThreshold();
        // set the threshold
        $config.threshold = threshold;

        emit ThesholdSet(account, threshold);
    }

    /**
     * Sets the data for a validator
     * @dev this function can be used to add a new validator or change the data for an existing one
     *
     * @param validatorAddress the address of the validator
     * @param id the id of the validator
     * @param newValidatorData the data to set for the validator
     */
    function setValidator(
        address validatorAddress,
        ValidatorId id,
        bytes calldata newValidatorData
    )
        external
    {
        // to prevent the user from overwriting an existing subvalidator configuration with 0
        // config, we check this
        if (newValidatorData.length == 0) revert InvalidValidatorData();

        // cache the account
        address account = msg.sender;
        // check if the module is initialized and revert if it is not
        if (!isInitialized(account)) revert NotInitialized(account);

        // get storage reference to account config
        MFAConfig storage $config = accountConfig[account];
        // cache the current iteration
        uint256 iteration = $config.iteration;

        // check that the subValidator is an attested validator and revert if it is not
        REGISTRY.checkForAccount({
            smartAccount: msg.sender,
            module: validatorAddress,
            moduleType: MODULE_TYPE_VALIDATOR
        });

        // get storage reference to subValidator config
        FlatBytesLib.Bytes storage $validator = $subValidatorData({
            account: account,
            iteration: iteration,
            subValidator: validatorAddress,
            id: id
        });

        // if this subvalidator is brand new, we have to iterate the validationLength counter.
        // should the validationData be new, but the subValidator already exist,
        // we don't need to do
        if ($validator.load().length == 0) {
            $config.validationLength += 1;
        }
        // set the subValidator data
        $validator.store(newValidatorData);

        if (newValidatorData.length == 0) {
            if ($config.validationLength < $config.threshold) {
                revert InvalidThreshold($config.validationLength, $config.threshold);
            }
        }

        // emit the ValidatorAdded event
        emit ValidatorAdded(account, validatorAddress, id, iteration);
    }

    /**
     * Removes a validator
     *
     * @param validatorAddress the address of the validator
     * @param id the id of the validator
     */
    function removeValidator(address validatorAddress, ValidatorId id) external {
        // cache the account
        address account = msg.sender;
        // check if the module is initialized and revert if it is not
        if (!isInitialized(account)) revert NotInitialized(account);

        // get storage reference to account config
        MFAConfig storage $config = accountConfig[account];
        // cache the current iteration
        uint256 iteration = $config.iteration;

        // get storage reference to subValidator config
        FlatBytesLib.Bytes storage $validator = $subValidatorData({
            account: account,
            iteration: iteration,
            subValidator: validatorAddress,
            id: id
        });

        if ($validator.load().length != 0) {
            $config.validationLength -= 1;
        }
        // delete the subValidator data
        $validator.clear();

        if ($config.validationLength < $config.threshold) {
            revert InvalidThreshold($config.validationLength, $config.threshold);
        }

        // emit the ValidatorRemoved event
        emit ValidatorRemoved(account, validatorAddress, id, iteration);
    }

    /**
     * Checks if a subValidator is configured for an account
     *
     * @param account the account to check
     * @param subValidator the subValidator to check
     * @param id the id of the subValidator
     *
     * @return true if the subValidator is configured, false otherwise
     */
    function isSubValidator(
        address account,
        address subValidator,
        ValidatorId id
    )
        external
        view
        returns (bool)
    {
        // get storage reference to account config
        MFAConfig storage $config = accountConfig[account];

        // get storage reference to subValidator config
        FlatBytesLib.Bytes storage $validator = $subValidatorData({
            account: account,
            iteration: $config.iteration,
            subValidator: subValidator,
            id: id
        });
        // check if the subValidator data is not empty
        return $validator.load().length != 0;
    }

    /*//////////////////////////////////////////////////////////////////////////
                                     MODULE LOGIC
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Validates a user operation
     *
     * @param userOp the user operation to validate
     * @param userOpHash the hash of the user operation
     *
     * @return ValidationData the validation data
     */
    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    )
        external
        virtual
        override
        returns (ValidationData)
    {
        // decode the validators
        Validator[] calldata validators = MultiFactorLib.decode(userOp.signature);

        // validate the signature
        bool isValid = _validateSignatureWithConfig(
            userOp.sender, validators, ECDSA.toEthSignedMessageHash(userOpHash)
        );

        if (isValid) {
            // return validation success if the signatures are valid
            return VALIDATION_SUCCESS;
        }
        // return validation failed otherwise
        return VALIDATION_FAILED;
    }

    /**
     * Validates an ERC-1271 signature
     *
     * @param hash the hash to validate
     * @param data the data to validate
     *
     * @return EIP1271_SUCCESS if the signature is valid, EIP1271_FAILED otherwise
     */
    function isValidSignatureWithSender(
        address,
        bytes32 hash,
        bytes calldata data
    )
        external
        view
        virtual
        override
        returns (bytes4)
    {
        // decode the validators
        Validator[] calldata validators = MultiFactorLib.decode(data);

        // validate the signature
        bool isValid = _validateSignatureWithConfig(msg.sender, validators, hash);

        if (isValid) {
            // return EIP1271_SUCCESS if the signatures are valid
            return EIP1271_SUCCESS;
        }
        // return EIP1271_FAILED otherwise
        return EIP1271_FAILED;
    }

    /*//////////////////////////////////////////////////////////////////////////
                                     INTERNAL
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Validates a signature with the current configuration
     *
     * @param account the account to validate the signature for
     * @param validators the validators to validate
     * @param hash the hash to validate
     *
     * @return true if the signature is valid, false otherwise
     */
    function _validateSignatureWithConfig(
        address account,
        Validator[] calldata validators,
        bytes32 hash
    )
        internal
        view
        returns (bool)
    {
        // cache the validators length
        uint256 validatorsLength = validators.length;
        // check if the validators length is 0 and return false if it is
        if (validatorsLength == 0) return false;

        // get storage reference to account config
        MFAConfig storage $config = accountConfig[account];
        // cache the current iteration
        uint256 iteration = $config.iteration;

        // count the number of valid signatures
        uint256 validCount;

        // iterate over the validators
        for (uint256 i; i < validatorsLength; i++) {
            // cache the validator
            Validator calldata validator = validators[i];

            // unpack the validator address and id
            (address validatorAddress, ValidatorId id) =
                MultiFactorLib.unpack(validator.packedValidatorAndId);

            // get storage reference to subValidator config
            FlatBytesLib.Bytes storage $validator = $subValidatorData({
                account: account,
                iteration: iteration,
                subValidator: validatorAddress,
                id: id
            });

            // check if the subValidator data is empty and return false if it is
            bytes memory validatorStorageData = $validator.load();
            if (validatorStorageData.length == 0) {
                return false;
            }

            // validate the signature
            bool isValid = IStatelessValidator(validatorAddress).validateSignatureWithData({
                hash: hash,
                signature: validator.data,
                data: validatorStorageData
            });

            if (isValid) {
                // increment the valid count if the signature is valid
                validCount++;
            }
        }

        // check if the valid count is greater than or equal to the threshold and return true if it
        // is
        if (validCount >= $config.threshold) return true;
    }

    /**
     * Gets the storage reference to a subValidator config
     *
     * @param account the account to get the config for
     * @param iteration the iteration to get the config for
     * @param subValidator the subValidator to get the config for
     * @param id the id of the subValidator
     *
     * @return $validatorData the storage reference to the subValidator config
     */
    function $subValidatorData(
        address account,
        uint256 iteration,
        address subValidator,
        ValidatorId id
    )
        internal
        view
        returns (FlatBytesLib.Bytes storage $validatorData)
    {
        // get storage reference to subValidator config
        return iterationToSubValidator[iteration][subValidator].subValidators[id][account];
    }

    /*//////////////////////////////////////////////////////////////////////////
                                     METADATA
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Returns the type of the module
     *
     * @param typeID type of the module
     *
     * @return true if the type is a module type, false otherwise
     */
    function isModuleType(uint256 typeID) external pure returns (bool) {
        return typeID == TYPE_VALIDATOR;
    }

    /**
     * Returns the name of the module
     *
     * @return name of the module
     */
    function name() external pure returns (string memory) {
        return "MultiFactor";
    }

    /**
     * Returns the version of the module
     *
     * @return version of the module
     */
    function version() external pure returns (string memory) {
        return "1.0.0";
    }
}
