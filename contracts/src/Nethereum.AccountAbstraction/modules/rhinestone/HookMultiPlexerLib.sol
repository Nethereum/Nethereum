// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import { IHook as IERC7579Hook } from "modulekit/accounts/common/interfaces/IERC7579Module.sol";
import { Config, SigHookInit, HookAndContext, HookType, SignatureHooks } from "./HookMultiPlexerDataTypes.sol";
import { LibSort } from "solady/utils/LibSort.sol";
import { IERC7579Account } from "modulekit/accounts/common/interfaces/IERC7579Account.sol";

import { ExecutionLib, Execution } from "modulekit/accounts/erc7579/lib/ExecutionLib.sol";
import {
    ModeLib,
    CallType,
    ModeCode,
    CALLTYPE_SINGLE,
    CALLTYPE_BATCH,
    CALLTYPE_DELEGATECALL
} from "modulekit/accounts/common/lib/ModeLib.sol";

uint256 constant EXEC_OFFSET = 100;

/**
 * @title HookMultiPlexerLib
 * @dev Library for multiplexing hooks
 * @author Rhinestone
 */
library HookMultiPlexerLib {
    error SubHookPreCheckError(address subHook);
    error SubHookPostCheckError(address subHook);
    error HooksNotSorted();

    using LibSort for uint256[];
    using HookMultiPlexerLib for *;

    /**
     * Prechecks a list of subhooks
     *
     * @param subHooks array of sub-hooks
     * @param msgSender sender of the transaction
     * @param msgValue value of the transaction
     * @param msgData data of the transaction
     *
     * @return hookAndContexts array of hook and context
     */
    function preCheckSubHooks(
        address[] memory subHooks,
        address msgSender,
        uint256 msgValue,
        bytes calldata msgData
    )
        internal
        returns (HookAndContext[] memory hookAndContexts)
    {
        // cache the length of the subhooks
        uint256 length = subHooks.length;
        // initialize the contexts array
        hookAndContexts = new HookAndContext[](length);
        for (uint256 i; i < length; i++) {
            // cache the subhook
            address subHook = subHooks[i];
            // precheck the subhook and return the context
            hookAndContexts[i] = HookAndContext({
                hook: subHook,
                context: preCheckSubHook(subHook, msgSender, msgValue, msgData)
            });
        }
    }

    /**
     * Prechecks a single subhook
     *
     * @param subHook sub-hook
     * @param msgSender sender of the transaction
     * @param msgValue value of the transaction
     * @param msgData data of the transaction
     *
     * @return preCheckContext pre-check context
     */
    function preCheckSubHook(
        address subHook,
        address msgSender,
        uint256 msgValue,
        bytes calldata msgData
    )
        internal
        returns (bytes memory preCheckContext)
    {
        // precheck the subhook
        bool success;
        (success, preCheckContext) = address(subHook).call(
            abi.encodePacked(
                abi.encodeCall(IERC7579Hook.preCheck, (msgSender, msgValue, msgData)),
                address(this),
                msg.sender
            )
        );
        // revert if the subhook precheck fails
        if (!success) revert SubHookPreCheckError(subHook);
    }

    /**
     * Postchecks a single subhook
     *
     * @param subHook sub-hook
     * @param preCheckContext pre-check context
     */
    function postCheckSubHook(address subHook, bytes calldata preCheckContext) internal {
        bytes memory data = abi.encodePacked(
            IERC7579Hook.postCheck.selector, preCheckContext, address(this), msg.sender
        );
        // postcheck the subhook
        (bool success,) = address(subHook).call(data);
        // revert if the subhook postcheck fails
        if (!success) revert SubHookPostCheckError(subHook);
    }

    /**
     * Joins two arrays
     *
     * @param a first array
     * @param b second array
     */
    function join(address[] memory a, address[] memory b) internal pure {
        // cache the lengths of the arrays
        uint256 aLength = a.length;
        uint256 bLength = b.length;
        uint256 totalLength = aLength + bLength;

        // if both arrays are empty, return an empty array
        if (totalLength == 0) {
            return;
        } else if (bLength == 0) {
            return;
        }

        // initialize the joined array
        uint256 offset;
        assembly {
            mstore(a, totalLength)
            offset := add(b, 0x20)
        }

        for (uint256 i; i < bLength; i++) {
            // join the arrays
            address next;
            assembly {
                next := mload(add(offset, mul(i, 0x20)))
            }
            a[aLength + i] = next;
        }
    }

    function join(address[] memory hooks, SignatureHooks storage $sigHooks) internal view {
        uint256 sigsLength = $sigHooks.allSigs.length;
        // iterate over the sigs
        for (uint256 i; i < sigsLength; i++) {
            // get the sig hooks
            hooks.join($sigHooks.sigHooks[$sigHooks.allSigs[i]]);
        }
    }

    /**
     * Ensures that an array is sorted and unique
     *
     * @param array array to check
     */
    function requireSortedAndUnique(address[] calldata array) internal pure {
        // cache the length of the array
        uint256 length = array.length;
        for (uint256 i = 1; i < length; i++) {
            // revert if the array is not sorted
            if (array[i - 1] >= array[i]) {
                revert HooksNotSorted();
            }
        }
    }

    /**
     * Gets the index of an element in an array
     *
     * @param array array to search
     * @param element element to find
     *
     * @return index index of the element
     */
    function indexOf(address[] storage array, address element) internal view returns (uint256) {
        // cache the length of the array
        uint256 length = array.length;
        for (uint256 i; i < length; i++) {
            // return the index of the element
            if (array[i] == element) {
                return i;
            }
        }
        // return the maximum value if the element is not found
        return type(uint256).max;
    }

    /**
     * Gets the index of an element in an array
     *
     * @param array array to search
     * @param element element to find
     *
     * @return index index of the element
     */
    function indexOf(bytes4[] storage array, bytes4 element) internal view returns (uint256) {
        // cache the length of the array
        uint256 length = array.length;
        for (uint256 i; i < length; i++) {
            // return the index of the element
            if (array[i] == element) {
                return i;
            }
        }
        // return the maximum value if the element is not found
        return type(uint256).max;
    }

    /**
     * Pushes a unique element to an array
     *
     * @param array array to push to
     * @param element element to push
     */
    function pushUnique(bytes4[] storage array, bytes4 element) internal {
        // cache the length of the array
        uint256 index = indexOf(array, element);
        if (index == type(uint256).max) {
            array.push(element);
        }
    }

    /**
     * Pops a bytes4 element from an array
     *
     * @param array array to pop from
     * @param element element to pop
     */
    function popBytes4(bytes4[] storage array, bytes4 element) internal {
        uint256 index = indexOf(array, element);
        if (index == type(uint256).max) {
            return;
        }
        array[index] = array[array.length - 1];
        array.pop();
    }

    /**
     * Pops an address from an array
     *
     * @param array array to pop from
     * @param element element to pop
     */
    function popAddress(address[] storage array, address element) internal {
        uint256 index = indexOf(array, element);
        if (index == type(uint256).max) {
            return;
        }
        array[index] = array[array.length - 1];
        array.pop();
    }

    /**
     * Decodes the onInstall data
     *
     * @param onInstallData onInstall data
     *
     * @return globalHooks array of global hooks
     * @return valueHooks array of value hooks
     * @return delegatecallHooks array of delegatecall hooks
     * @return sigHooks array of sig hooks
     * @return targetSigHooks array of target sig hooks
     */
    function decodeOnInstall(bytes calldata onInstallData)
        internal
        pure
        returns (
            address[] calldata globalHooks,
            address[] calldata valueHooks,
            address[] calldata delegatecallHooks,
            SigHookInit[] calldata sigHooks,
            SigHookInit[] calldata targetSigHooks
        )
    {
        // saves 2000 gas when 1 hook per type used
        // (
        //     address[] memory globalHooks,
        //     address[] memory valueHooks,
        //     address[] memory delegatecallHooks,
        //     SigHookInit[] memory sigHooks,
        //     SigHookInit[] memory targetSigHooks
        // ) = abi.decode(data, (address[], address[], address[], SigHookInit[], SigHookInit[]));
        assembly ("memory-safe") {
            let offset := onInstallData.offset
            let baseOffset := offset

            let dataPointer := add(baseOffset, calldataload(offset))
            globalHooks.offset := add(dataPointer, 0x20)
            globalHooks.length := calldataload(dataPointer)
            offset := add(offset, 0x20)

            dataPointer := add(baseOffset, calldataload(offset))
            valueHooks.offset := add(dataPointer, 0x20)
            valueHooks.length := calldataload(dataPointer)
            offset := add(offset, 0x20)

            dataPointer := add(baseOffset, calldataload(offset))
            delegatecallHooks.offset := add(dataPointer, 0x20)
            delegatecallHooks.length := calldataload(dataPointer)
            offset := add(offset, 0x20)

            dataPointer := add(baseOffset, calldataload(offset))
            sigHooks.offset := add(dataPointer, 0x20)
            sigHooks.length := calldataload(dataPointer)
            offset := add(offset, 0x20)

            dataPointer := add(baseOffset, calldataload(offset))
            targetSigHooks.offset := add(dataPointer, 0x20)
            targetSigHooks.length := calldataload(dataPointer)
        }
    }

    function storeSelectorHooks(
        SignatureHooks storage $sigHooks,
        SigHookInit[] calldata newSigHooks
    )
        internal
    {
        // cache the length of the sig hooks
        uint256 length = newSigHooks.length;
        // array to store the sigs
        uint256[] memory sigs = new uint256[](length);
        // iterate over the sig hooks
        for (uint256 i; i < length; i++) {
            // cache the sig hook
            SigHookInit calldata _sigHook = newSigHooks[i];
            // require the subhooks to be unique
            _sigHook.subHooks.requireSortedAndUnique();
            // add the sig to the sigs array
            sigs[i] = uint256(bytes32(_sigHook.sig));
            // set the sig hooks
            $sigHooks.sigHooks[_sigHook.sig] = _sigHook.subHooks;
        }

        // sort the sigs
        sigs.insertionSort();
        // uniquify the sigs
        sigs.uniquifySorted();

        // add the sigs to the sigs array
        length = sigs.length;
        for (uint256 i; i < length; i++) {
            $sigHooks.allSigs.push(bytes4(bytes32(sigs[i])));
        }
    }

    function deleteHooks(SignatureHooks storage $sigHooks) internal {
        uint256 length = $sigHooks.allSigs.length;
        // iterate over the sigs
        for (uint256 i; i < length; i++) {
            // delete the sig hooks
            delete $sigHooks.sigHooks[$sigHooks.allSigs[i]];
        }
        delete $sigHooks.allSigs;
    }

    /**
     * Checks if the callDataSelector is an execution
     *
     * @param callDataSelector bytes4 of the callDataSelector
     *
     * @return true if the callDataSelector is an execution, false otherwise
     */
    function isExecution(bytes4 callDataSelector) internal pure returns (bool) {
        // check if the callDataSelector is an execution
        return callDataSelector == IERC7579Account.execute.selector
            || callDataSelector == IERC7579Account.executeFromExecutor.selector;
    }

    function appendExecutionHook(
        address[] memory hooks,
        Config storage $config,
        bytes calldata msgData
    )
        internal
        view
    {
        // get the length of the execution callData
        uint256 paramLen = uint256(bytes32(msgData[EXEC_OFFSET - 32:EXEC_OFFSET]));

        // get the mode and calltype
        ModeCode mode = ModeCode.wrap(bytes32(msgData[4:36]));
        CallType calltype = ModeLib.getCallType(mode);

        if (calltype == CALLTYPE_SINGLE) {
            // decode the execution
            (, uint256 value, bytes calldata callData) =
                ExecutionLib.decodeSingle(msgData[EXEC_OFFSET:EXEC_OFFSET + paramLen]);

            // if there is a value, we need to check the value hooks
            if (value != 0) {
                hooks.join($config.hooks[HookType.VALUE]);
            }

            // if there is callData, we need to check the targetSigHooks
            if (callData.length > 4) {
                hooks.join($config.sigHooks[HookType.TARGET_SIG].sigHooks[bytes4(callData[:4])]);
            }
        } else if (calltype == CALLTYPE_BATCH) {
            // decode the batch
            hooks.join(
                _getFromBatch({
                    $config: $config,
                    executions: ExecutionLib.decodeBatch(msgData[EXEC_OFFSET:EXEC_OFFSET + paramLen])
                })
            );
        } else if (calltype == CALLTYPE_DELEGATECALL) {
            // get the delegatecall hooks
            hooks.join($config.hooks[HookType.DELEGATECALL]);
        }
    }

    /**
     * Gets the hooks from the batch
     *
     * @param $config storage config
     * @param executions array of executions
     *
     * @return allHooks array of hooks
     */
    function _getFromBatch(
        Config storage $config,
        Execution[] calldata executions
    )
        internal
        view
        returns (address[] memory allHooks)
    {
        // check if the targetSigHooks are enabled
        bool targetSigHooksEnabled = $config.sigHooks[HookType.TARGET_SIG].allSigs.length != 0;
        // get the length of the executions
        uint256 length = executions.length;

        // casting bytes4 functionSigs in here. We are using uint256, since thats the native type
        // in LibSort
        uint256[] memory targetSigsInBatch = new uint256[](length);
        // variable to check if any of the executions have a value
        bool batchHasValue;
        // iterate over the executions
        for (uint256 i; i < length; i++) {
            // cache the execution
            Execution calldata execution = executions[i];
            // value only has to be checked once. If there is a value in any of the executions,
            // value hooks are used
            if (!batchHasValue && execution.value != 0) {
                // set the flag
                batchHasValue = true;
                // get the value hooks
                allHooks = $config.hooks[HookType.VALUE];
                // If targetSigHooks are not enabled, we can stop here and return
                if (!targetSigHooksEnabled) return allHooks;
            }
            // if there is callData, we need to check the targetSigHooks
            if (execution.callData.length > 4) {
                targetSigsInBatch[i] = uint256(bytes32(execution.callData[:4]));
            }
        }
        // If targetSigHooks are not enabled, we can stop here and return
        if (!targetSigHooksEnabled) return allHooks;

        // we only want to sload the targetSigHooks once
        targetSigsInBatch.insertionSort();
        targetSigsInBatch.uniquifySorted();

        // cache the length of the targetSigsInBatch
        length = targetSigsInBatch.length;
        for (uint256 i; i < length; i++) {
            // downcast the functionSig to bytes4
            bytes4 targetSelector = bytes4(bytes32(targetSigsInBatch[i]));

            // get the targetSigHooks
            address[] storage _targetHooks =
                $config.sigHooks[HookType.TARGET_SIG].sigHooks[targetSelector];

            // if there are none, continue
            if (_targetHooks.length == 0) continue;
            if (allHooks.length == 0) {
                // set the targetHooks if there are no other hooks
                allHooks = _targetHooks;
            } else {
                // join the targetHooks with the other hooks
                allHooks.join(_targetHooks);
            }
        }
    }
}
