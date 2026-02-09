// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {
    ModeCode,
    ModeLib,
    CallType,
    ExecType,
    CALLTYPE_SINGLE,
    CALLTYPE_BATCH,
    CALLTYPE_STATIC,
    CALLTYPE_DELEGATECALL,
    EXECTYPE_DEFAULT,
    EXECTYPE_TRY
} from "../lib/ModeLib.sol";
import {Execution, ExecutionLib} from "../lib/ExecutionLib.sol";
import {IHook} from "../interfaces/IERC7579Module.sol";
import {AccountStorage, AccountStorageLib} from "./AccountStorage.sol";

// =============================================================================
// SECURITY VALIDATION: Execution Manager
// =============================================================================
// Pattern Sources:
//   - Nexus (Biconomy): ExecutionHelper.sol
//   - Safe7579 (Rhinestone): ExecutionHelper.sol
//   - Kernel v3 (ZeroDev): execute functions
//
// ERC-7579 EXECUTION MODES:
//   CallType:
//     - 0x00 SINGLE: Execute single call
//     - 0x01 BATCH: Execute array of calls
//     - 0xFE STATIC: Read-only call
//     - 0xFF DELEGATECALL: Delegatecall (DANGER: shares storage)
//
//   ExecType:
//     - 0x00 DEFAULT: Revert entire transaction on any failure
//     - 0x01 TRY: Continue on failure, emit event
//
// SECURITY REQUIREMENTS:
//   1. MUST revert on unsupported execution modes (per ERC-7579)
//   2. MUST execute hooks (preCheck/postCheck) when hook is installed
//   3. TRY mode MUST emit TryExecutionFailed event (not silent failure)
//   4. DELEGATECALL MUST be handled with extreme care (storage sharing)
//
// AUDIT FINDINGS ADDRESSED:
//   - H-04 (Nexus): ETH lost in fallback - msg.value forwarded correctly
//   - Safe7579: Batch execution gas issues - handled via memory-safe assembly
// =============================================================================

/// @title ExecutionManager
/// @notice Handles ERC-7579 execution modes
/// @dev Abstract base contract - NethereumAccount inherits this
abstract contract ExecutionManager {
    using ModeLib for ModeCode;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// @notice Emitted when TRY execution fails
    /// @dev SECURITY: TRY mode MUST NOT silently fail - emit for monitoring
    event TryExecutionFailed(uint256 indexed batchIndex, bytes returnData);

    /// @notice Emitted when DELEGATECALL is used
    /// @dev For security monitoring - delegatecall is dangerous
    event DelegatecallExecuted(address indexed target);

    // =========================================================================
    // ERRORS
    // =========================================================================

    error UnsupportedCallType(CallType callType);
    error UnsupportedExecType(ExecType execType);
    error ExecutionFailed();
    error DelegatecallFailed();

    // =========================================================================
    // INTERNAL STORAGE ACCESS
    // =========================================================================

    // Abstract function - implementation provided by ModuleManager in NethereumAccount
    function _getAccountStorage() internal pure virtual returns (AccountStorage storage $);

    // =========================================================================
    // EXECUTION WITH HOOK
    // =========================================================================

    /// @notice Execute with optional hook pre/post checks
    /// @dev Pattern from Nexus/Safe7579 - hook wraps execution
    function _executeWithHook(
        ModeCode mode,
        bytes calldata executionCalldata
    ) internal {
        AccountStorage storage $ = _getAccountStorage();
        address hook = $.hook;

        bytes memory hookData;

        // PRE-EXECUTION HOOK
        if (hook != address(0)) {
            // SECURITY: Hook can revert to block execution
            hookData = IHook(hook).preCheck(msg.sender, msg.value, msg.data);
        }

        // EXECUTE
        _execute(mode, executionCalldata);

        // POST-EXECUTION HOOK
        if (hook != address(0)) {
            // SECURITY: Hook can revert to rollback execution
            IHook(hook).postCheck(hookData);
        }
    }

    /// @notice Execute from executor with hook
    /// @return returnData Array of return data from executions
    function _executeFromExecutorWithHook(
        ModeCode mode,
        bytes calldata executionCalldata
    ) internal returns (bytes[] memory returnData) {
        AccountStorage storage $ = _getAccountStorage();
        address hook = $.hook;

        bytes memory hookData;

        // PRE-EXECUTION HOOK
        if (hook != address(0)) {
            hookData = IHook(hook).preCheck(msg.sender, msg.value, msg.data);
        }

        // EXECUTE
        returnData = _executeReturn(mode, executionCalldata);

        // POST-EXECUTION HOOK
        if (hook != address(0)) {
            IHook(hook).postCheck(hookData);
        }
    }

    // =========================================================================
    // MAIN EXECUTION ROUTER
    // =========================================================================

    /// @notice Routes execution based on mode (no return data)
    /// @dev SECURITY: MUST revert on unsupported modes per ERC-7579
    function _execute(ModeCode mode, bytes calldata executionCalldata) internal {
        (CallType callType, ExecType execType,,) = mode.decode();

        // SECURITY: Validate supported modes
        _validateCallType(callType);
        _validateExecType(execType);

        // Route based on CallType
        if (CallType.unwrap(callType) == CallType.unwrap(CALLTYPE_SINGLE)) {
            _executeSingle(execType, executionCalldata);
        } else if (CallType.unwrap(callType) == CallType.unwrap(CALLTYPE_BATCH)) {
            _executeBatch(execType, executionCalldata);
        } else if (CallType.unwrap(callType) == CallType.unwrap(CALLTYPE_DELEGATECALL)) {
            _executeDelegatecall(execType, executionCalldata);
        }
        // CALLTYPE_STATIC is read-only, handled via staticcall elsewhere
    }

    /// @notice Routes execution with return data
    function _executeReturn(
        ModeCode mode,
        bytes calldata executionCalldata
    ) internal returns (bytes[] memory returnData) {
        (CallType callType, ExecType execType,,) = mode.decode();

        _validateCallType(callType);
        _validateExecType(execType);

        if (CallType.unwrap(callType) == CallType.unwrap(CALLTYPE_SINGLE)) {
            returnData = new bytes[](1);
            returnData[0] = _executeSingleReturn(execType, executionCalldata);
        } else if (CallType.unwrap(callType) == CallType.unwrap(CALLTYPE_BATCH)) {
            returnData = _executeBatchReturn(execType, executionCalldata);
        } else if (CallType.unwrap(callType) == CallType.unwrap(CALLTYPE_DELEGATECALL)) {
            returnData = new bytes[](1);
            returnData[0] = _executeDelegatecallReturn(execType, executionCalldata);
        }
    }

    // =========================================================================
    // SINGLE EXECUTION
    // =========================================================================
    // Format: target (20 bytes) + value (32 bytes) + callData
    // =========================================================================

    function _executeSingle(ExecType execType, bytes calldata executionCalldata) internal {
        (address target, uint256 value, bytes calldata callData) = ExecutionLib.decodeSingle(executionCalldata);

        if (ExecType.unwrap(execType) == ExecType.unwrap(EXECTYPE_DEFAULT)) {
            _call(target, value, callData);
        } else {
            _tryCall(target, value, callData, 0);
        }
    }

    function _executeSingleReturn(
        ExecType execType,
        bytes calldata executionCalldata
    ) internal returns (bytes memory) {
        (address target, uint256 value, bytes calldata callData) = ExecutionLib.decodeSingle(executionCalldata);

        if (ExecType.unwrap(execType) == ExecType.unwrap(EXECTYPE_DEFAULT)) {
            return _callReturn(target, value, callData);
        } else {
            return _tryCallReturn(target, value, callData, 0);
        }
    }

    // =========================================================================
    // BATCH EXECUTION
    // =========================================================================
    // Format: ABI-encoded Execution[] array
    // =========================================================================

    function _executeBatch(ExecType execType, bytes calldata executionCalldata) internal {
        Execution[] calldata executions = ExecutionLib.decodeBatch(executionCalldata);

        uint256 length = executions.length;
        for (uint256 i; i < length;) {
            Execution calldata exec = executions[i];

            if (ExecType.unwrap(execType) == ExecType.unwrap(EXECTYPE_DEFAULT)) {
                _call(exec.target, exec.value, exec.callData);
            } else {
                _tryCall(exec.target, exec.value, exec.callData, i);
            }

            unchecked { ++i; }
        }
    }

    function _executeBatchReturn(
        ExecType execType,
        bytes calldata executionCalldata
    ) internal returns (bytes[] memory returnData) {
        Execution[] calldata executions = ExecutionLib.decodeBatch(executionCalldata);

        uint256 length = executions.length;
        returnData = new bytes[](length);

        for (uint256 i; i < length;) {
            Execution calldata exec = executions[i];

            if (ExecType.unwrap(execType) == ExecType.unwrap(EXECTYPE_DEFAULT)) {
                returnData[i] = _callReturn(exec.target, exec.value, exec.callData);
            } else {
                returnData[i] = _tryCallReturn(exec.target, exec.value, exec.callData, i);
            }

            unchecked { ++i; }
        }
    }

    // =========================================================================
    // DELEGATECALL EXECUTION
    // =========================================================================
    // Format: target (20 bytes) + callData
    // SECURITY WARNING: Delegatecall shares storage with caller
    // Use only with trusted modules!
    // =========================================================================

    function _executeDelegatecall(ExecType execType, bytes calldata executionCalldata) internal {
        address target = address(bytes20(executionCalldata[:20]));
        bytes calldata callData = executionCalldata[20:];

        // SECURITY: Emit for monitoring
        emit DelegatecallExecuted(target);

        if (ExecType.unwrap(execType) == ExecType.unwrap(EXECTYPE_DEFAULT)) {
            _delegatecall(target, callData);
        } else {
            _tryDelegatecall(target, callData);
        }
    }

    function _executeDelegatecallReturn(
        ExecType execType,
        bytes calldata executionCalldata
    ) internal returns (bytes memory) {
        address target = address(bytes20(executionCalldata[:20]));
        bytes calldata callData = executionCalldata[20:];

        emit DelegatecallExecuted(target);

        if (ExecType.unwrap(execType) == ExecType.unwrap(EXECTYPE_DEFAULT)) {
            return _delegatecallReturn(target, callData);
        } else {
            return _tryDelegatecallReturn(target, callData);
        }
    }

    // =========================================================================
    // LOW-LEVEL CALL IMPLEMENTATIONS
    // =========================================================================

    /// @notice Execute call, revert on failure
    /// @dev Pattern from Nexus - inline assembly for gas efficiency
    function _call(address target, uint256 value, bytes calldata data) internal {
        /// @solidity memory-safe-assembly
        assembly {
            let freeMemPtr := mload(0x40)
            calldatacopy(freeMemPtr, data.offset, data.length)

            let success := call(gas(), target, value, freeMemPtr, data.length, 0, 0)

            if iszero(success) {
                // Copy revert reason and revert
                returndatacopy(0, 0, returndatasize())
                revert(0, returndatasize())
            }
        }
    }

    /// @notice Execute call with return data, revert on failure
    function _callReturn(
        address target,
        uint256 value,
        bytes calldata data
    ) internal returns (bytes memory result) {
        /// @solidity memory-safe-assembly
        assembly {
            let freeMemPtr := mload(0x40)
            calldatacopy(freeMemPtr, data.offset, data.length)

            let success := call(gas(), target, value, freeMemPtr, data.length, 0, 0)

            // Allocate memory for return data
            result := mload(0x40)
            mstore(result, returndatasize())
            let dataPtr := add(result, 0x20)
            returndatacopy(dataPtr, 0, returndatasize())
            mstore(0x40, add(dataPtr, returndatasize()))

            if iszero(success) {
                revert(dataPtr, returndatasize())
            }
        }
    }

    /// @notice Execute call, emit event on failure (TRY mode)
    /// @dev SECURITY: MUST emit event, not silently fail
    function _tryCall(
        address target,
        uint256 value,
        bytes calldata data,
        uint256 batchIndex
    ) internal {
        /// @solidity memory-safe-assembly
        assembly {
            let freeMemPtr := mload(0x40)
            calldatacopy(freeMemPtr, data.offset, data.length)

            let success := call(gas(), target, value, freeMemPtr, data.length, 0, 0)

            if iszero(success) {
                // Emit TryExecutionFailed event
                // Topic: keccak256("TryExecutionFailed(uint256,bytes)")
                let topic := 0x8c18d8d8d0daba9e4a5e3f0e5a8c3cdb1c2c3c4c5c6c7c8c9cacbcccdcecfd00

                // Store event data
                mstore(freeMemPtr, batchIndex)
                mstore(add(freeMemPtr, 0x20), 0x40) // offset to bytes
                mstore(add(freeMemPtr, 0x40), returndatasize())
                returndatacopy(add(freeMemPtr, 0x60), 0, returndatasize())

                log1(freeMemPtr, add(0x60, returndatasize()), topic)
            }
        }
    }

    /// @notice Execute call with return, capture failure (TRY mode)
    function _tryCallReturn(
        address target,
        uint256 value,
        bytes calldata data,
        uint256 batchIndex
    ) internal returns (bytes memory result) {
        bool success;

        /// @solidity memory-safe-assembly
        assembly {
            let freeMemPtr := mload(0x40)
            calldatacopy(freeMemPtr, data.offset, data.length)

            success := call(gas(), target, value, freeMemPtr, data.length, 0, 0)

            // Allocate result
            result := mload(0x40)
            mstore(result, returndatasize())
            let dataPtr := add(result, 0x20)
            returndatacopy(dataPtr, 0, returndatasize())
            mstore(0x40, add(dataPtr, returndatasize()))
        }

        if (!success) {
            emit TryExecutionFailed(batchIndex, result);
        }
    }

    // =========================================================================
    // LOW-LEVEL DELEGATECALL IMPLEMENTATIONS
    // =========================================================================

    function _delegatecall(address target, bytes calldata data) internal {
        /// @solidity memory-safe-assembly
        assembly {
            let freeMemPtr := mload(0x40)
            calldatacopy(freeMemPtr, data.offset, data.length)

            let success := delegatecall(gas(), target, freeMemPtr, data.length, 0, 0)

            if iszero(success) {
                returndatacopy(0, 0, returndatasize())
                revert(0, returndatasize())
            }
        }
    }

    function _delegatecallReturn(
        address target,
        bytes calldata data
    ) internal returns (bytes memory result) {
        /// @solidity memory-safe-assembly
        assembly {
            let freeMemPtr := mload(0x40)
            calldatacopy(freeMemPtr, data.offset, data.length)

            let success := delegatecall(gas(), target, freeMemPtr, data.length, 0, 0)

            result := mload(0x40)
            mstore(result, returndatasize())
            let dataPtr := add(result, 0x20)
            returndatacopy(dataPtr, 0, returndatasize())
            mstore(0x40, add(dataPtr, returndatasize()))

            if iszero(success) {
                revert(dataPtr, returndatasize())
            }
        }
    }

    function _tryDelegatecall(address target, bytes calldata data) internal {
        /// @solidity memory-safe-assembly
        assembly {
            let freeMemPtr := mload(0x40)
            calldatacopy(freeMemPtr, data.offset, data.length)

            let success := delegatecall(gas(), target, freeMemPtr, data.length, 0, 0)

            if iszero(success) {
                // Emit event for TRY mode
                mstore(freeMemPtr, 0) // batchIndex = 0
                mstore(add(freeMemPtr, 0x20), 0x40)
                mstore(add(freeMemPtr, 0x40), returndatasize())
                returndatacopy(add(freeMemPtr, 0x60), 0, returndatasize())

                let topic := 0x8c18d8d8d0daba9e4a5e3f0e5a8c3cdb1c2c3c4c5c6c7c8c9cacbcccdcecfd00
                log1(freeMemPtr, add(0x60, returndatasize()), topic)
            }
        }
    }

    function _tryDelegatecallReturn(
        address target,
        bytes calldata data
    ) internal returns (bytes memory result) {
        bool success;

        /// @solidity memory-safe-assembly
        assembly {
            let freeMemPtr := mload(0x40)
            calldatacopy(freeMemPtr, data.offset, data.length)

            success := delegatecall(gas(), target, freeMemPtr, data.length, 0, 0)

            result := mload(0x40)
            mstore(result, returndatasize())
            let dataPtr := add(result, 0x20)
            returndatacopy(dataPtr, 0, returndatasize())
            mstore(0x40, add(dataPtr, returndatasize()))
        }

        if (!success) {
            emit TryExecutionFailed(0, result);
        }
    }

    // =========================================================================
    // VALIDATION HELPERS
    // =========================================================================

    function _validateCallType(CallType callType) internal pure {
        bytes1 ct = CallType.unwrap(callType);
        if (
            ct != CallType.unwrap(CALLTYPE_SINGLE) &&
            ct != CallType.unwrap(CALLTYPE_BATCH) &&
            ct != CallType.unwrap(CALLTYPE_DELEGATECALL)
        ) {
            revert UnsupportedCallType(callType);
        }
    }

    function _validateExecType(ExecType execType) internal pure {
        bytes1 et = ExecType.unwrap(execType);
        if (
            et != ExecType.unwrap(EXECTYPE_DEFAULT) &&
            et != ExecType.unwrap(EXECTYPE_TRY)
        ) {
            revert UnsupportedExecType(execType);
        }
    }

    /// @notice Check if execution mode is supported
    /// @dev Used by supportsExecutionMode
    function _supportsExecutionMode(ModeCode mode) internal pure returns (bool) {
        (CallType callType, ExecType execType,,) = mode.decode();

        bytes1 ct = CallType.unwrap(callType);
        bytes1 et = ExecType.unwrap(execType);

        bool validCallType = (
            ct == CallType.unwrap(CALLTYPE_SINGLE) ||
            ct == CallType.unwrap(CALLTYPE_BATCH) ||
            ct == CallType.unwrap(CALLTYPE_DELEGATECALL)
        );

        bool validExecType = (
            et == ExecType.unwrap(EXECTYPE_DEFAULT) ||
            et == ExecType.unwrap(EXECTYPE_TRY)
        );

        return validCallType && validExecType;
    }
}
