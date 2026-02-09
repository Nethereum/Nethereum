// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "./PackedUserOperation.sol";

interface IAccountModule {
    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) external returns (uint256 validationData);

    function preExecute(
        address target,
        uint256 value,
        bytes calldata data
    ) external;

    function postExecute(
        address target,
        uint256 value,
        bytes calldata data
    ) external;

    function onOwnerChanged(
        address oldOwner,
        address newOwner
    ) external;

    function moduleId() external pure returns (bytes32);

    function version() external pure returns (uint256);

    function supportsInterface(bytes4 interfaceId) external view returns (bool);

    function isSecurityCritical() external view returns (bool);
}

interface IValidationModule is IAccountModule {
    function isValidSignature(
        bytes32 hash,
        bytes calldata signature
    ) external view returns (bool);
}

interface IExecutionModule is IAccountModule {
    function canExecute(
        address caller,
        address target,
        uint256 value,
        bytes calldata data
    ) external view returns (bool);
}
