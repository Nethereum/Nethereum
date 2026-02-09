// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

/**
 * @title PackedUserOperation
 * @notice ERC-4337 v0.7 packed UserOperation structure
 */
struct PackedUserOperation {
    address sender;
    uint256 nonce;
    bytes initCode;
    bytes callData;
    bytes32 accountGasLimits;    // packed: verificationGasLimit (16 bytes) | callGasLimit (16 bytes)
    uint256 preVerificationGas;
    bytes32 gasFees;             // packed: maxPriorityFeePerGas (16 bytes) | maxFeePerGas (16 bytes)
    bytes paymasterAndData;
    bytes signature;
}

library UserOperationLib {
    bytes32 internal constant PACKED_USEROP_TYPEHASH = keccak256(
        "PackedUserOperation(address sender,uint256 nonce,bytes initCode,bytes callData,bytes32 accountGasLimits,uint256 preVerificationGas,bytes32 gasFees,bytes paymasterAndData)"
    );

    function unpackAccountGasLimits(bytes32 accountGasLimits) internal pure returns (uint128 verificationGasLimit, uint128 callGasLimit) {
        verificationGasLimit = uint128(bytes16(accountGasLimits));
        callGasLimit = uint128(uint256(accountGasLimits));
    }

    function unpackGasFees(bytes32 gasFees) internal pure returns (uint128 maxPriorityFeePerGas, uint128 maxFeePerGas) {
        maxPriorityFeePerGas = uint128(bytes16(gasFees));
        maxFeePerGas = uint128(uint256(gasFees));
    }

    function packAccountGasLimits(uint128 verificationGasLimit, uint128 callGasLimit) internal pure returns (bytes32) {
        return bytes32(bytes16(verificationGasLimit)) | bytes32(uint256(callGasLimit));
    }

    function packGasFees(uint128 maxPriorityFeePerGas, uint128 maxFeePerGas) internal pure returns (bytes32) {
        return bytes32(bytes16(maxPriorityFeePerGas)) | bytes32(uint256(maxFeePerGas));
    }

    function hash(PackedUserOperation calldata userOp) internal pure returns (bytes32) {
        return keccak256(abi.encode(
            PACKED_USEROP_TYPEHASH,
            userOp.sender,
            userOp.nonce,
            keccak256(userOp.initCode),
            keccak256(userOp.callData),
            userOp.accountGasLimits,
            userOp.preVerificationGas,
            userOp.gasFees,
            keccak256(userOp.paymasterAndData)
        ));
    }
}
