// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

/// @title ExecutionLib
/// @notice Library for encoding/decoding ERC-7579 execution data

/// @notice Struct representing a single execution
struct Execution {
    address target;
    uint256 value;
    bytes callData;
}

library ExecutionLib {
    /// @notice Decodes a single execution from calldata
    /// @param executionCalldata Encoded as: target (20 bytes) + value (32 bytes) + callData
    function decodeSingle(bytes calldata executionCalldata)
        internal
        pure
        returns (address target, uint256 value, bytes calldata callData)
    {
        target = address(bytes20(executionCalldata[:20]));
        value = uint256(bytes32(executionCalldata[20:52]));
        callData = executionCalldata[52:];
    }

    /// @notice Decodes a batch of executions from calldata
    /// @param executionCalldata ABI encoded Execution[]
    function decodeBatch(bytes calldata executionCalldata)
        internal
        pure
        returns (Execution[] calldata executions)
    {
        assembly {
            let ptr := add(executionCalldata.offset, calldataload(executionCalldata.offset))
            executions.offset := add(ptr, 0x20)
            executions.length := calldataload(ptr)
        }
    }

    /// @notice Encodes a single execution
    function encodeSingle(
        address target,
        uint256 value,
        bytes memory callData
    ) internal pure returns (bytes memory) {
        return abi.encodePacked(target, value, callData);
    }

    /// @notice Encodes a batch of executions
    function encodeBatch(Execution[] memory executions) internal pure returns (bytes memory) {
        return abi.encode(executions);
    }
}
