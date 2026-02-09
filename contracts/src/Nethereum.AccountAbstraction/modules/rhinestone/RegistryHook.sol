// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import { ERC7579HookDestruct } from "modulekit/Modules.sol";
import { IERC7484 } from "modulekit/Interfaces.sol";
import { MODULE_TYPE_EXECUTOR } from "modulekit/accounts/common/interfaces/IERC7579Module.sol";

/**
 * @title RegistryHook
 * @dev Module that allows querying of a Registry on module installation
 * @author Rhinestone
 */
contract RegistryHook is ERC7579HookDestruct {
    /*//////////////////////////////////////////////////////////////////////////
                            CONSTANTS & STORAGE
    //////////////////////////////////////////////////////////////////////////*/

    event RegistrySet(address indexed smartAccount, address registry);

    // account => registry
    mapping(address account => address) public registry;

    /*//////////////////////////////////////////////////////////////////////////
                                     CONFIG
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Initializes the module with the registry
     * @dev data is encoded as follows: abi.encodePacked(registry)
     *
     * @param data encoded data containing the registry
     */
    function onInstall(bytes calldata data) external override {
        // cache the account address
        address account = msg.sender;
        // check if the module is already initialized and revert if it is
        if (isInitialized(account)) revert ModuleAlreadyInitialized(account);

        // decode the registry
        address registryAddress = address(uint160(bytes20(data[0:20])));

        // set the registry
        registry[account] = registryAddress;

        // emit the RegistrySet event
        emit RegistrySet({ smartAccount: account, registry: registryAddress });
    }

    /**
     * Handles the uninstallation of the module and clears the registry
     * @dev the data parameter is not used
     */
    function onUninstall(bytes calldata) external override {
        // delete the registry
        delete registry[msg.sender];

        // clear the trusted forwarder
        clearTrustedForwarder();

        // emit the RegistrySet event
        emit RegistrySet({ smartAccount: msg.sender, registry: address(0) });
    }

    /**
     * Checks if the module is initialized
     *
     * @param smartAccount address of the smart account
     * @return true if the module is initialized, false otherwise
     */
    function isInitialized(address smartAccount) public view returns (bool) {
        return registry[smartAccount] != address(0);
    }

    /**
     * Sets the registry for the smart account
     *
     * @param _registry address of the registry
     */
    function setRegistry(address _registry) external {
        // cache the account address
        address account = msg.sender;
        // check if the module is initialized and revert if it is not
        if (!isInitialized(account)) revert NotInitialized(account);

        // set the registry
        registry[account] = _registry;
        // emit the RegistrySet event
        emit RegistrySet(account, _registry);
    }

    /*//////////////////////////////////////////////////////////////////////////
                                     INTERNAL
    //////////////////////////////////////////////////////////////////////////*/

    /**
     * Called when a module is installed on the account
     *
     * @param moduleType type of the module
     * @param module address of the module
     */
    function onInstallModule(
        address account,
        address,
        uint256 moduleType,
        address module,
        bytes calldata
    )
        internal
        virtual
        override
        returns (bytes memory)
    {
        // query the registry using stored attesters
        IERC7484(registry[account]).checkForAccount({
            smartAccount: account,
            module: module,
            moduleType: moduleType
        });
    }

    /**
     * Called when an executor executes a transaction
     *
     * @param msgSender the executor
     */
    function onExecuteFromExecutor(
        address account,
        address msgSender,
        address,
        uint256,
        bytes calldata
    )
        internal
        virtual
        override
        returns (bytes memory)
    {
        // query the registry using stored attesters
        IERC7484(registry[account]).checkForAccount({
            smartAccount: account,
            module: msgSender,
            moduleType: MODULE_TYPE_EXECUTOR
        });
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
        return typeID == TYPE_HOOK;
    }

    /**
     * Returns the name of the module
     *
     * @return name of the module
     */
    function name() external pure virtual returns (string memory) {
        return "RegistryHook";
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
