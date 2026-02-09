// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import { ERC7579ValidatorBase } from "modulekit/Modules.sol";
import { PackedUserOperation, IAccountExecute } from "modulekit/external/ERC4337.sol";
import { SentinelList4337Lib, SENTINEL } from "sentinellist/SentinelList4337.sol";
import { CheckSignatures } from "checknsignatures/CheckNSignatures.sol";
import { IERC7579Account } from "modulekit/Accounts.sol";
import {
    ModeLib, CallType, ModeCode, CALLTYPE_SINGLE
} from "modulekit/accounts/common/lib/ModeLib.sol";
import { ExecutionLib } from "modulekit/accounts/erc7579/lib/ExecutionLib.sol";
import { LibSort } from "solady/utils/LibSort.sol";
import { ECDSA } from "solady/utils/ECDSA.sol";

/**
 * @title SocialRecovery
 * @dev Module that allows users to recover their account using a social recovery mechanism
 * @author Rhinestone
 */
contract SocialRecovery is ERC7579ValidatorBase {
    using LibSort for *;
    using SentinelList4337Lib for SentinelList4337Lib.SentinelList;

    /*//////////////////////////////////////////////////////////////////////////
                            CONSTANTS & STORAGE
    //////////////////////////////////////////////////////////////////////////*/

    event ModuleInitialized(address indexed account);
    event ModuleUninitialized(address indexed account);
    event GuardianAdded(address indexed account, address guardian);
    event GuardianRemoved(address indexed account, address guardian);
    event ThresholdSet(address indexed account, uint256 threshold);

    error UnsupportedOperation();
    error InvalidGuardian(address guardian);
    error NotSortedAndUnique();
    error MaxGuardiansReached();
    error ThresholdNotSet();
    error InvalidThreshold();
    error CannotRemoveGuardian();

    // maximum number of guardians per account
    uint256 constant MAX_GUARDIANS = 32;

    // account => guardians
    SentinelList4337Lib.SentinelList guardians;
    // account => threshold
    mapping(address account => uint256) public threshold;
    // account => guardianCount
    mapping(address => uint256) public guardianCount;

    /*//////////////////////////////////////////////////////////////////////////
                                     CONFIG
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Initializes the module with the threshold and guardians
     * @dev data is encoded as follows: abi.encode(threshold, guardians)
     *
     * @param data encoded data containing the threshold and guardians
     */
    function onInstall(bytes calldata data) external override {
        // get the threshold and guardians from the data
        (uint256 _threshold, address[] memory _guardians) = abi.decode(data, (uint256, address[]));

        // check that guardians are sorted and uniquified
        if (!_guardians.isSortedAndUniquified()) {
            revert NotSortedAndUnique();
        }

        // make sure the threshold is set
        if (_threshold == 0) {
            revert ThresholdNotSet();
        }

        // make sure the threshold is less than the number of guardians
        uint256 guardiansLength = _guardians.length;
        if (guardiansLength < _threshold) {
            revert InvalidThreshold();
        }

        // cache the account address
        address account = msg.sender;

        // check if max guardians is reached
        if (guardiansLength > MAX_GUARDIANS) {
            revert MaxGuardiansReached();
        }

        // set guardian count
        guardianCount[account] = guardiansLength;

        // set threshold
        threshold[account] = _threshold;

        // initialize the guardian list
        guardians.init(account);

        // add guardians to the list
        for (uint256 i = 0; i < guardiansLength; i++) {
            address _guardian = _guardians[i];
            if (_guardian == address(0)) {
                revert InvalidGuardian(_guardian);
            }
            guardians.push(account, _guardian);
        }

        // emit the ModuleInitialized event
        emit ModuleInitialized(account);
    }

    /**
     * Handles the uninstallation of the module and clears the threshold and guardians
     * @dev the data parameter is not used
     */
    function onUninstall(bytes calldata) external override {
        // cache the account address
        address account = msg.sender;

        // clear the guardians
        guardians.popAll(account);

        // delete the threshold
        threshold[account] = 0;

        // delete the guardian count
        guardianCount[account] = 0;

        // emit the ModuleUninitialized event
        emit ModuleUninitialized(account);
    }

    /**
     * Checks if the module is initialized
     *
     * @param smartAccount address of the smart account
     * @return true if the module is initialized, false otherwise
     */
    function isInitialized(address smartAccount) public view returns (bool) {
        return threshold[smartAccount] != 0;
    }

    /**
     * Sets the threshold for the account
     * @dev the function will revert if the module is not initialized
     *
     * @param _threshold uint256 threshold to set
     */
    function setThreshold(uint256 _threshold) external {
        // cache the account address
        address account = msg.sender;
        // check if the module is initialized and revert if it is not
        if (!isInitialized(account)) revert NotInitialized(account);

        // make sure the threshold is set
        if (_threshold == 0) {
            revert InvalidThreshold();
        }

        if (guardianCount[account] < _threshold) {
            revert InvalidThreshold();
        }

        // set the threshold
        threshold[account] = _threshold;

        // emit the ThresholdSet event
        emit ThresholdSet(account, _threshold);
    }

    /**
     * Adds a guardian to the account
     * @dev will revert if the guardian is already added
     *
     * @param guardian address of the guardian to add
     */
    function addGuardian(address guardian) external {
        // cache the account address
        address account = msg.sender;
        // check if the module is initialized and revert if it is not
        if (!isInitialized(account)) revert NotInitialized(account);

        // revert if the guardian is address(0)
        if (guardian == address(0)) {
            revert InvalidGuardian(guardian);
        }

        // check if max guardians is reached
        if (guardianCount[account] >= MAX_GUARDIANS) {
            revert MaxGuardiansReached();
        }

        // increment the guardian count
        guardianCount[account]++;

        // add the guardian to the list
        guardians.push(account, guardian);

        // emit the GuardianAdded event
        emit GuardianAdded(account, guardian);
    }

    /**
     * Removes a guardian from the account
     * @dev will revert if the guardian is not added or the previous guardian is invalid
     *
     * @param prevGuardian address of the previous guardian
     * @param guardian address of the guardian to remove
     */
    function removeGuardian(address prevGuardian, address guardian) external {
        // cache the account address
        address account = msg.sender;

        // check if an guardian can be removed
        if (guardianCount[account] == threshold[account]) {
            // if the guardian count is equal to the threshold, revert
            // this means that removing an guardian would make the threshold unreachable
            revert CannotRemoveGuardian();
        }

        // remove the guardian from the list
        guardians.pop(account, prevGuardian, guardian);

        // decrement the guardian count
        guardianCount[account]--;

        // emit the GuardianRemoved event
        emit GuardianRemoved(account, guardian);
    }

    /**
     * Gets the guardians for the account
     *
     * @param account address of the account
     *
     * @return guardiansArray array of guardians
     */
    function getGuardians(address account)
        external
        view
        returns (address[] memory guardiansArray)
    {
        // get the guardians from the list
        (guardiansArray,) = guardians.getEntriesPaginated(account, SENTINEL, MAX_GUARDIANS);
    }

    /*//////////////////////////////////////////////////////////////////////////
                                     MODULE LOGIC
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Validates a user operation
     *
     * @param userOp PackedUserOperation struct containing the UserOperation
     * @param userOpHash bytes32 hash of the UserOperation
     *
     * @return ValidationData the UserOperation validation result
     */
    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    )
        external
        view
        override
        returns (ValidationData)
    {
        // get the account
        address account = userOp.sender;

        // get the threshold and check that its set
        uint256 _threshold = threshold[account];
        if (_threshold == 0) {
            return VALIDATION_FAILED;
        }

        // recover the signers from the signatures
        address[] memory signers = CheckSignatures.recoverNSignatures(
            ECDSA.toEthSignedMessageHash(userOpHash), userOp.signature, _threshold
        );

        // sort and uniquify the signers to make sure a signer is not reused
        signers.sort();
        signers.uniquifySorted();

        // Check if the signers are guardians
        uint256 validSigners;
        for (uint256 i = 0; i < signers.length; i++) {
            if (guardians.contains(account, signers[i])) {
                validSigners++;
            }
        }

        // check if the execution is allowed
        bool isAllowedExecution;
        bytes4 selector = bytes4(userOp.callData[0:4]);
        if (selector == IERC7579Account.execute.selector) {
            // decode and check the execution
            // only single executions to installed validators are allowed
            isAllowedExecution = _decodeAndCheckExecution(account, userOp.callData);
        } else if (selector == IAccountExecute.executeUserOp.selector) {
            if (bytes4(userOp.callData[4:8]) == IERC7579Account.execute.selector) {
                // decode and check the execution
                // only single executions to installed validators are allowed
                isAllowedExecution = _decodeAndCheckExecution(account, userOp.callData[4:]);
            }
        }

        // check if the threshold is met and the execution is allowed and return the result
        if (validSigners >= _threshold && isAllowedExecution) {
            return VALIDATION_SUCCESS;
        }
        return VALIDATION_FAILED;
    }

    /**
     * Validates an ERC-1271 signature with the sender
     * @dev ERC-1271 not supported for SocialRecovery
     */
    function isValidSignatureWithSender(
        address,
        bytes32,
        bytes calldata
    )
        external
        pure
        override
        returns (bytes4)
    {
        revert UnsupportedOperation();
    }

    /*//////////////////////////////////////////////////////////////////////////
                                     INTERNAL
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Decodes and checks the execution
     *
     * @param account address of the account
     * @param callData bytes calldata containing the call data
     *
     * @return isAllowedExecution true if the execution is allowed, false otherwise
     */
    function _decodeAndCheckExecution(
        address account,
        bytes calldata callData
    )
        internal
        view
        returns (bool isAllowedExecution)
    {
        // get the mode and call type
        ModeCode mode = ModeCode.wrap(bytes32(callData[4:36]));
        CallType calltype = ModeLib.getCallType(mode);

        if (calltype == CALLTYPE_SINGLE) {
            // decode the calldata
            (address to,,) = ExecutionLib.decodeSingle(callData[100:]);

            // check if the module is installed as a validator
            return IERC7579Account(account).isModuleInstalled(TYPE_VALIDATOR, to, "");
        } else {
            return false;
        }
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
    function isModuleType(uint256 typeID) external pure override returns (bool) {
        return typeID == TYPE_VALIDATOR;
    }

    /**
     * Returns the name of the module
     *
     * @return name of the module
     */
    function name() external pure virtual returns (string memory) {
        return "SocialRecoveryValidator";
    }

    /**
     * Returns the version of the module
     *
     * @return version of the module
     */
    function version() external pure virtual returns (string memory) {
        return "1.0.0";
    }
}
