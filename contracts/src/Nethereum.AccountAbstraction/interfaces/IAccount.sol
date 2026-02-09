// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "./PackedUserOperation.sol";

/**
 * @title IAccount
 * @notice Standard ERC-4337 Account interface (unchanged from spec)
 * @dev All smart accounts must implement this interface
 */
interface IAccount {
    /**
     * @notice Validates a UserOperation
     * @param userOp The UserOperation to validate
     * @param userOpHash Hash of the UserOperation (includes EntryPoint address and chainId)
     * @param missingAccountFunds Amount the account needs to pay to EntryPoint for gas
     * @return validationData Packed validation data:
     *   - sigFailed (1 bit): 1 if signature validation failed
     *   - validUntil (48 bits): timestamp until which the signature is valid (0 = infinite)
     *   - validAfter (48 bits): timestamp after which the signature is valid
     */
    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash,
        uint256 missingAccountFunds
    ) external returns (uint256 validationData);
}

/**
 * @title IAccountExecute
 * @notice Optional execution interface for accounts
 */
interface IAccountExecute {
    function executeUserOp(PackedUserOperation calldata userOp, bytes32 userOpHash) external;

    function execute(address dest, uint256 value, bytes calldata data) external returns (bytes memory);

    /**
     * @notice Execute a batch of calls
     * @param dest Array of target addresses
     * @param value Array of ETH values
     * @param data Array of call data
     * @return Results of the calls
     */
    function executeBatch(
        address[] calldata dest,
        uint256[] calldata value,
        bytes[] calldata data
    ) external returns (bytes[] memory);
}
