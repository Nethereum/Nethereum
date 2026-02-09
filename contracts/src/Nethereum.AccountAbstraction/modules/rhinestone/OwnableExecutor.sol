// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import { IERC7579Account } from "modulekit/accounts/common/interfaces/IERC7579Account.sol";
import { ERC7579ExecutorBase } from "modulekit/Modules.sol";
import { ModeLib } from "modulekit/accounts/common/lib/ModeLib.sol";
import { SentinelListLib, SENTINEL } from "sentinellist/SentinelList.sol";

/**
 * @title OwnableExecutor
 * @dev Module that allows users to designate an owner that can execute transactions on their behalf
 * and pays for gas
 * @author Rhinestone
 */
contract OwnableExecutor is ERC7579ExecutorBase {
    using SentinelListLib for SentinelListLib.SentinelList;

    /*//////////////////////////////////////////////////////////////////////////
                            CONSTANTS & STORAGE
    //////////////////////////////////////////////////////////////////////////*/

    event ModuleInitialized(address indexed account, address owner);
    event ModuleUninitialized(address indexed account);
    event OwnerAdded(address indexed account, address owner);
    event OwnerRemoved(address indexed account, address owner);

    error UnauthorizedAccess();
    error InvalidOwner(address owner);

    // account => owners
    mapping(address subAccount => SentinelListLib.SentinelList) accountOwners;
    // account => ownerCount
    mapping(address => uint256) public ownerCount;

    /*//////////////////////////////////////////////////////////////////////////
                                     CONFIG
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Initializes the module with the owner
     * @dev data is encoded as follows: abi.encodePacked(owner)
     *
     * @param data encoded data containing the owner
     */
    function onInstall(bytes calldata data) external override {
        // cache the account address
        address account = msg.sender;

        // decode the owner
        address owner = address(bytes20(data[0:20]));
        // revert if the owner is address(0)
        if (owner == address(0)) {
            revert InvalidOwner(owner);
        }

        // initialize the linked list
        accountOwners[account].init();
        // add the owner to the linked list
        accountOwners[account].push(owner);

        // set the owner count
        ownerCount[account] = 1;

        emit ModuleInitialized(account, owner);
    }

    /**
     * Handles the uninstallation of the module and clears the owners
     * @dev the data parameter is not used
     */
    function onUninstall(bytes calldata) external override {
        // clear the owners
        accountOwners[msg.sender].popAll();

        // clear the owner count
        ownerCount[msg.sender] = 0;

        emit ModuleUninitialized(msg.sender);
    }

    /**
     * Checks if the module is initialized
     *
     * @param smartAccount address of the smart account
     * @return true if the module is initialized, false otherwise
     */
    function isInitialized(address smartAccount) public view returns (bool) {
        // check if the linked list is initialized for the smart account
        return accountOwners[smartAccount].alreadyInitialized();
    }

    /**
     * Adds an owner to the account
     * @dev will revert if the owner is already added
     *
     * @param owner address of the owner to add
     */
    function addOwner(address owner) external {
        // cache the account address
        address account = msg.sender;
        // check if the module is initialized and revert if it is not
        if (!isInitialized(account)) revert NotInitialized(account);

        // revert if the owner is address(0)
        if (owner == address(0)) {
            revert InvalidOwner(owner);
        }

        // add the owner to the linked list
        accountOwners[account].push(owner);

        // increment the owner count
        ownerCount[account]++;

        emit OwnerAdded(account, owner);
    }

    /**
     * Removes an owner from the account
     * @dev will revert if the owner is not added or the previous owner is invalid
     *
     * @param prevOwner address of the previous owner
     * @param owner address of the owner to remove
     */
    function removeOwner(address prevOwner, address owner) external {
        // remove the owner
        accountOwners[msg.sender].pop(prevOwner, owner);

        // decrement the owner count
        ownerCount[msg.sender]--;

        emit OwnerRemoved(msg.sender, owner);
    }

    /**
     * Returns the owners of the account
     *
     * @param account address of the account
     *
     * @return ownersArray array of owners
     */
    function getOwners(address account) external view returns (address[] memory ownersArray) {
        // gets the owners from the linked list
        (ownersArray,) = accountOwners[account].getEntriesPaginated(SENTINEL, ownerCount[account]);
    }

    /*//////////////////////////////////////////////////////////////////////////
                                     MODULE LOGIC
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Executes a transaction on the owned account
     *
     * @param ownedAccount address of the account to execute the transaction on
     * @param callData encoded data containing the transaction to execute
     */
    function executeOnOwnedAccount(
        address ownedAccount,
        bytes calldata callData
    )
        external
        payable
    {
        // check if the sender is an owner
        if (!accountOwners[ownedAccount].contains(msg.sender)) {
            revert UnauthorizedAccess();
        }

        // execute the transaction on the owned account
        IERC7579Account(ownedAccount).executeFromExecutor{ value: msg.value }(
            ModeLib.encodeSimpleSingle(), callData
        );
    }

    /**
     * Executes a batch of transactions on the owned account
     *
     * @param ownedAccount address of the account to execute the transaction on
     * @param callData encoded data containing the transactions to execute
     */
    function executeBatchOnOwnedAccount(
        address ownedAccount,
        bytes calldata callData
    )
        external
        payable
    {
        // check if the sender is an owner
        if (!accountOwners[ownedAccount].contains(msg.sender)) {
            revert UnauthorizedAccess();
        }

        // execute the batch of transaction on the owned account
        IERC7579Account(ownedAccount).executeFromExecutor{ value: msg.value }(
            ModeLib.encodeSimpleBatch(), callData
        );
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
        return typeID == TYPE_EXECUTOR;
    }

    /**
     * Returns the name of the module
     *
     * @return name of the module
     */
    function name() external pure virtual returns (string memory) {
        return "OwnableExecutor";
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
