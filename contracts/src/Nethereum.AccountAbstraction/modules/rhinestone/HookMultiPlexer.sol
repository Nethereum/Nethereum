// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import { ERC7579ModuleBase } from "modulekit/Modules.sol";
import { ERC7484RegistryAdapter } from "modulekit/Modules.sol";
import { IHook as IERC7579Hook } from "modulekit/accounts/common/interfaces/IERC7579Module.sol";
import { SigHookInit, SignatureHooks, Config, HookType, HookAndContext } from "./HookMultiPlexerDataTypes.sol";
import { Execution } from "modulekit/accounts/erc7579/lib/ExecutionLib.sol";
import { HookMultiPlexerLib } from "./HookMultiPlexerLib.sol";
import { LibSort } from "solady/utils/LibSort.sol";
import { IERC7484 } from "modulekit/Interfaces.sol";

/**
 * @title HookMultiPlexer
 * @dev A module that allows to add multiple hooks to a smart account
 * @author Rhinestone
 */
contract HookMultiPlexer is ERC7579ModuleBase, IERC7579Hook, ERC7484RegistryAdapter {
    using HookMultiPlexerLib for *;
    using LibSort for uint256[];
    using LibSort for address[];

    error UnsupportedHookType(HookType hookType);

    event HookAdded(address indexed account, address indexed hook, HookType hookType);
    event SigHookAdded(
        address indexed account, address indexed hook, HookType hookType, bytes4 sig
    );

    event HookRemoved(address indexed account, address indexed hook, HookType hookType);
    event SigHookRemoved(
        address indexed account, address indexed hook, HookType hookType, bytes4 sig
    );
    event AccountInitialized(address indexed account);
    event AccountUninitialized(address indexed account);

    /*´:°•.°+.*•´.*:˚.°*.˚•´.°:°•.°•.*•´.*:˚.°*.˚•´.°:°•.°+.*•´.*:*/
    /*                          Storage                           */
    /*.•°:°.´+˚.*°.˚:*.´•*.+°.•°:´*.´•*.•°.•°:°.´:•˚°.*°.˚:*.´+°.•*/
    mapping(address account => Config config) internal accountConfig;

    /**
     * Contract constructor
     * @dev sets the registry as an immutable variable
     *
     * @param _registry The registry address
     */
    constructor(IERC7484 _registry) ERC7484RegistryAdapter(_registry) { }

    modifier onlySupportedHookType(HookType hookType) {
        if (uint8(hookType) <= uint8(HookType.TARGET_SIG)) {
            _;
        } else {
            revert UnsupportedHookType(hookType);
        }
    }

    /*´:°•.°+.*•´.*:˚.°*.˚•´.°:°•.°•.*•´.*:˚.°*.˚•´.°:°•.°+.*•´.*:*/
    /*                           CONFIG                           */
    /*.•°:°.´+˚.*°.˚:*.´•*.+°.•°:´*.´•*.•°.•°:°.´:•˚°.*°.˚:*.´+°.•*/

    /**
     * Initializes the module with the hooks
     * @dev data is encoded as follows: abi.encode(
     *      address[] globalHooks,
     *      address[] valueHooks,
     *      address[] delegatecallHooks,
     *      SigHookInit[] sigHooks,
     *      SigHookInit[] targetSigHooks
     * )
     *
     * @param data encoded data containing the hooks
     */
    function onInstall(bytes calldata data) external override {
        // check if the module is already initialized and revert if it is
        if (isInitialized(msg.sender)) revert ModuleAlreadyInitialized(msg.sender);

        // decode the hook arrays
        (
            address[] calldata globalHooks,
            address[] calldata valueHooks,
            address[] calldata delegatecallHooks,
            SigHookInit[] calldata sigHooks,
            SigHookInit[] calldata targetSigHooks
        ) = data.decodeOnInstall();

        // cache the storage config
        Config storage $config = $getConfig({ account: msg.sender });

        globalHooks.requireSortedAndUnique();
        $config.hooks[HookType.GLOBAL] = globalHooks;

        valueHooks.requireSortedAndUnique();
        $config.hooks[HookType.VALUE] = valueHooks;

        delegatecallHooks.requireSortedAndUnique();
        $config.hooks[HookType.DELEGATECALL] = delegatecallHooks;

        // storeSelectorHooks function is used to uniquify and sstore sig specific hooks
        $config.sigHooks[HookType.SIG].storeSelectorHooks(sigHooks);
        $config.sigHooks[HookType.TARGET_SIG].storeSelectorHooks(targetSigHooks);

        $config.initialized = true;

        emit AccountInitialized(msg.sender);
    }

    /**
     * Uninstalls the module
     * @dev deletes all the hooks
     */
    function onUninstall(bytes calldata) external override {
        // cache the storage config
        Config storage $config = $getConfig({ account: msg.sender });

        delete $config.hooks[HookType.GLOBAL];
        delete $config.hooks[HookType.DELEGATECALL];
        delete $config.hooks[HookType.VALUE];
        $config.sigHooks[HookType.SIG].deleteHooks();
        $config.sigHooks[HookType.TARGET_SIG].deleteHooks();
        $config.initialized = false;

        emit AccountUninitialized(msg.sender);
    }

    /**
     * Checks if the module is initialized
     * @dev short curcuiting the check for efficiency
     *
     * @param smartAccount address of the smart account
     *
     * @return true if the module is initialized, false otherwise
     */
    function isInitialized(address smartAccount) public view override returns (bool) {
        Config storage $config = $getConfig({ account: smartAccount });
        return $config.initialized;
    }

    /**
     * Returns the hooks for the account
     * @dev this function is not optimized and should only be used when calling from offchain
     *
     * @param smartAccount address of the account
     *
     * @return hooks array of hooks
     */
    function getHooks(address smartAccount) external view returns (address[] memory hooks) {
        // cache the storage config
        Config storage $config = $getConfig({ account: smartAccount });

        // get the global hooks
        hooks = $config.hooks[HookType.GLOBAL];
        // get the delegatecall hooks
        hooks.join($config.hooks[HookType.DELEGATECALL]);
        // get the value hooks
        hooks.join($config.hooks[HookType.VALUE]);

        hooks.join($config.sigHooks[HookType.SIG]);
        hooks.join($config.sigHooks[HookType.TARGET_SIG]);

        // sort the hooks
        hooks.insertionSort();
        // uniquify the hooks
        hooks.uniquifySorted();
    }

    /**
     * Adds a hook to the account
     * @dev this function will not revert if the hook is already added
     *
     * @param hook address of the hook
     * @param hookType type of the hook
     */
    function addHook(address hook, HookType hookType) external onlySupportedHookType(hookType) {
        // check if the module is initialized and revert if it is not
        if (!isInitialized(msg.sender)) revert NotInitialized(msg.sender);

        // check if the hook is attested to on the registry
        REGISTRY.checkForAccount({ smartAccount: msg.sender, module: hook, moduleType: TYPE_HOOK });

        // store subhook
        $getConfig({ account: msg.sender }).hooks[hookType].push(hook);

        emit HookAdded(msg.sender, hook, hookType);
    }

    /**
     * Adds a sig hook to the account
     * @dev this function will not revert if the hook is already added
     *
     * @param hook address of the hook
     * @param sig bytes4 of the sig
     * @param hookType type of the hook
     */
    function addSigHook(
        address hook,
        bytes4 sig,
        HookType hookType
    )
        external
        onlySupportedHookType(hookType)
    {
        // check if the module is initialized and revert if it is not
        if (!isInitialized(msg.sender)) revert NotInitialized(msg.sender);

        // check if the hook is attested to on the registry
        REGISTRY.checkForAccount({ smartAccount: msg.sender, module: hook, moduleType: TYPE_HOOK });

        // cache the storage config
        Config storage $config = $getConfig({ account: msg.sender });

        $config.sigHooks[hookType].sigHooks[sig].push(hook);
        $config.sigHooks[hookType].allSigs.pushUnique(sig);

        emit SigHookAdded(msg.sender, hook, hookType, sig);
    }

    /**
     * Removes a hook from the account
     *
     * @param hook address of the hook
     * @param hookType type of the hook
     */
    function removeHook(address hook, HookType hookType) external {
        // cache the storage config
        Config storage $config = $getConfig({ account: msg.sender });
        $config.hooks[hookType].popAddress(hook);

        emit HookRemoved(msg.sender, hook, hookType);
    }

    /**
     * Removes a sig hook from the account
     *
     * @param hook address of the hook
     * @param sig bytes4 of the sig
     * @param hookType type of the hook
     */
    function removeSigHook(address hook, bytes4 sig, HookType hookType) external {
        // check if the module is initialized and revert if it is not
        if (!isInitialized(msg.sender)) revert NotInitialized(msg.sender);

        // cache the storage config
        Config storage $config = $getConfig({ account: msg.sender });
        SignatureHooks storage $sigHooks = $config.sigHooks[hookType];

        uint256 length = $sigHooks.sigHooks[sig].length;
        $sigHooks.sigHooks[sig].popAddress(hook);
        if (length == 1) {
            $sigHooks.allSigs.popBytes4(sig);
        }
        emit SigHookRemoved(msg.sender, hook, hookType, sig);
    }

    /*´:°•.°+.*•´.*:˚.°*.˚•´.°:°•.°•.*•´.*:˚.°*.˚•´.°:°•.°+.*•´.*:*/
    /*                      MODULE LOGIC                          */
    /*.•°:°.´+˚.*°.˚:*.´•*.+°.•°:´*.´•*.•°.•°:°.´:•˚°.*°.˚:*.´+°.•*/

    /**
     * Checks if the transaction is valid
     * @dev this function is called before the transaction is executed
     *
     * @param msgSender address of the sender
     * @param msgValue value of the transaction
     * @param msgData data of the transaction
     *
     * @return hookData data of the hooks
     */
    function preCheck(
        address msgSender,
        uint256 msgValue,
        bytes calldata msgData
    )
        external
        virtual
        override
        returns (bytes memory hookData)
    {
        // cache the storage config
        Config storage $config = $getConfig({ account: msg.sender });
        // get the call data selector
        bytes4 callDataSelector = bytes4(msgData[:4]);

        address[] memory hooks = $config.hooks[HookType.GLOBAL];
        hooks.join($config.sigHooks[HookType.SIG].sigHooks[callDataSelector]);

        // if the msgData that is hooked contains an execution
        //          (see IERC7579 execute() and executeFromExecutor())
        // we have to inspect the execution data, and if relevant, add:
        //  - value hooks
        //  - target sig hooks
        //  - delegatecall hooks
        // should the msgData not be an execution (i.e. IERC7579 installModule() or fallback Module
        // this can be skipped
        if (callDataSelector.isExecution()) {
            hooks.appendExecutionHook({ $config: $config, msgData: msgData });
        }

        // sort the hooks
        hooks.insertionSort();
        // uniquify the hooks
        hooks.uniquifySorted();

        // call all subhooks and return the subhooks with their context datas
        return abi.encode(
            hooks.preCheckSubHooks({ msgSender: msgSender, msgValue: msgValue, msgData: msgData })
        );
    }

    /**
     * Checks if the transaction is valid
     * @dev this function is called after the transaction is executed
     *
     * @param hookData data of the hooks
     */
    function postCheck(bytes calldata hookData) external override {
        // create the hooks and contexts array
        HookAndContext[] calldata hooksAndContexts;

        // decode the hookData
        assembly ("memory-safe") {
            let dataPointer := add(hookData.offset, calldataload(hookData.offset))
            hooksAndContexts.offset := add(dataPointer, 0x20)
            hooksAndContexts.length := calldataload(dataPointer)
        }

        // get the length of the hooks
        uint256 length = hooksAndContexts.length;
        for (uint256 i; i < length; i++) {
            // cache the hook and context
            HookAndContext calldata hookAndContext = hooksAndContexts[i];
            // call postCheck on each hook
            hookAndContext.hook.postCheckSubHook({ preCheckContext: hookAndContext.context });
        }
    }

    /**
     * Gets the config for the account
     *
     * @param account address of the account
     *
     * @return config storage config
     */
    function $getConfig(address account) internal view returns (Config storage) {
        return accountConfig[account];
    }

    /**
     * Returns the type of the module
     *
     * @param typeID type of the module
     *
     * @return true if the type is a module type, false otherwise
     */
    function isModuleType(uint256 typeID) external pure virtual override returns (bool) {
        return typeID == TYPE_HOOK;
    }

    /**
     * Returns the name of the module
     *
     * @return name of the module
     */
    function name() external pure virtual returns (string memory) {
        return "HookMultiPlexer";
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
