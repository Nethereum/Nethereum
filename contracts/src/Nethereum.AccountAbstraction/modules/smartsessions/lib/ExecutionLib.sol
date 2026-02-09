// SPDX-License-Identifier: MIT
pragma solidity ^0.8.23;

import { Execution } from "erc7579/interfaces/IERC7579Account.sol";
import { CallType, ExecType, ModeCode as ExecutionMode } from "erc7579/lib/ModeLib.sol";

/**
 * Helper Library for decoding Execution calldata
 * malloc for memory allocation is bad for gas. use this assembly instead
 */
library ExecutionLib {
    error ERC7579DecodingError();

    function decodeUserOpCallData(bytes calldata userOpCallData)
        internal
        pure
        returns (bytes calldata erc7579ExecutionCalldata)
    {
        assembly {
            if lt(userOpCallData.length, 68) { revert(0, 0) }
            let baseOffset := add(userOpCallData.offset, 0x24) //skip 4 bytes of selector and 32 bytes of execution mode
            let calldataLoadOffset := calldataload(baseOffset)
            // check for potential overflow in calldataLoadOffset
            if gt(calldataLoadOffset, 0xffffffffffffffff) { revert(0, 0) }
            erc7579ExecutionCalldata.offset := add(baseOffset, calldataLoadOffset)
            erc7579ExecutionCalldata.length := calldataload(sub(erc7579ExecutionCalldata.offset, 0x20))
            if gt(erc7579ExecutionCalldata.length, 0xffffffffffffffff) { revert(0, 0) }

            let calldataBound := add(userOpCallData.offset, userOpCallData.length)
            // revert if erc7579ExecutionCalldata starts after userOp finishes and if erc7579ExecutionCalldata ends
            // after userOp finishes
            if gt(erc7579ExecutionCalldata.offset, calldataBound) { revert(0, 0) }
            if gt(add(erc7579ExecutionCalldata.offset, erc7579ExecutionCalldata.length), calldataBound) { revert(0, 0) }
        }
    }

    function get7579ExecutionMode(bytes calldata userOpCallData) internal pure returns (ExecutionMode mode) {
        mode = ExecutionMode.wrap(bytes32(userOpCallData[4:36]));
    }

    function get7579ExecutionTypes(bytes calldata userOpCallData)
        internal
        pure
        returns (CallType callType, ExecType execType)
    {
        ExecutionMode mode = ExecutionMode.wrap(bytes32(userOpCallData[4:36]));

        // solhint-disable-next-line no-inline-assembly
        assembly {
            callType := mode
            execType := shl(8, mode)
        }
    }

    /**
     * @notice Decode a batch of `Execution` executionBatch from a `bytes` calldata.
     * @dev code is copied from solady's LibERC7579.sol
     * https://github.com/Vectorized/solady/blob/740812cedc9a1fc11e17cb3d4569744367dedf19/src/accounts/LibERC7579.sol#L146
     *      Credits to Vectorized and the Solady Team
     */
    function decodeBatch(bytes calldata executionCalldata)
        internal
        pure
        returns (Execution[] calldata executionBatch)
    {
        /// @solidity memory-safe-assembly
        assembly {
            let u := calldataload(executionCalldata.offset)
            let s := add(executionCalldata.offset, u)
            let e := sub(add(executionCalldata.offset, executionCalldata.length), 0x20)
            executionBatch.offset := add(s, 0x20)
            executionBatch.length := calldataload(s)
            if or(shr(64, u), gt(add(s, shl(5, executionBatch.length)), e)) {
                mstore(0x00, 0xba597e7e) // `DecodingError()`.
                revert(0x1c, 0x04)
            }
            if executionBatch.length {
                // Perform bounds checks on the decoded `executionBatch`.
                // Loop runs out-of-gas if `executionBatch.length` is big enough to cause overflows.
                for { let i := executionBatch.length } 1 { } {
                    i := sub(i, 1)
                    let p := calldataload(add(executionBatch.offset, shl(5, i)))
                    let c := add(executionBatch.offset, p)
                    let q := calldataload(add(c, 0x40))
                    let o := add(c, q)
                    // forgefmt: disable-next-item
                    if or(shr(64, or(calldataload(o), or(p, q))),
                        or(gt(add(c, 0x40), e), gt(add(o, calldataload(o)), e))) {
                        mstore(0x00, 0xba597e7e) // `DecodingError()`.
                        revert(0x1c, 0x04)
                    }
                    if iszero(i) { break }
                }
            }
        }
    }

    function encodeBatch(Execution[] memory executions) internal pure returns (bytes memory callData) {
        callData = abi.encode(executions);
    }

    function decodeSingle(bytes calldata executionCalldata)
        internal
        pure
        returns (address target, uint256 value, bytes calldata callData)
    {
        target = address(bytes20(executionCalldata[0:20]));
        value = uint256(bytes32(executionCalldata[20:52]));
        callData = executionCalldata[52:];
    }

    function encodeSingle(
        address target,
        uint256 value,
        bytes memory callData
    )
        internal
        pure
        returns (bytes memory userOpCalldata)
    {
        userOpCalldata = abi.encodePacked(target, value, callData);
    }
}
