// SPDX-License-Identifier: MIT

pragma solidity ^0.8.23;

import "../DataTypes.sol";
import {
    IUserOpPolicy,
    IActionPolicy,
    I1271Policy,
    IPolicy,
    VALIDATION_SUCCESS,
    VALIDATION_FAILED
} from "../interfaces/IPolicy.sol";
import { IERC165 } from "forge-std/interfaces/IERC165.sol";
import { SubModuleLib } from "../lib/SubModuleLib.sol";
import { EnumerableSet } from "../utils/EnumerableSet4337.sol";
import { PackedUserOperation } from "modulekit/external/ERC4337.sol";

contract SudoPolicy is IUserOpPolicy, IActionPolicy, I1271Policy {
    using EnumerableSet for EnumerableSet.Bytes32Set;

    event SudoPolicyInstalledMultiplexer(address indexed account, address indexed multiplexer, ConfigId indexed id);
    event SudoPolicyUninstalledAllAccount(address indexed account);
    event SudoPolicySet(address indexed account, address indexed multiplexer, ConfigId indexed id);
    event SudoPolicyRemoved(address indexed account, address indexed multiplexer, ConfigId indexed id);

    mapping(address account => bool isInitialized) internal $initialized;
    mapping(address multiplexer => EnumerableSet.Bytes32Set configIds) internal $enabledConfigs;

    /**
     * Initializes the policy to be used by given account through multiplexer (msg.sender) such as Smart Sessions.
     * Overwrites state.
     * @notice ATTENTION: This method is called during permission installation as part of the enabling policies flow.
     * A secure policy would minimize external calls from this method (ideally, to 0) to prevent passing control flow to
     * external contracts.
     */
    function initializeWithMultiplexer(address account, ConfigId configId, bytes calldata /*initData*/ ) external {
        $enabledConfigs[msg.sender].add(account, ConfigId.unwrap(configId));
        emit IPolicy.PolicySet(configId, msg.sender, account);
    }

    function checkUserOpPolicy(
        ConfigId, /*id*/
        PackedUserOperation calldata /*userOp*/
    )
        external
        pure
        returns (uint256)
    {
        return VALIDATION_SUCCESS;
    }

    function checkAction(
        ConfigId, /*id*/
        address, /*account*/
        address, /*target*/
        uint256, /*value*/
        bytes calldata /*data*/
    )
        external
        pure
        override
        returns (uint256)
    {
        return VALIDATION_SUCCESS;
    }

    function check1271SignedAction(
        ConfigId id,
        address requestSender,
        address account,
        bytes32 hash,
        bytes calldata signature
    )
        external
        pure
        returns (bool)
    {
        return true;
    }

    function supportsInterface(bytes4 interfaceID) external pure override returns (bool) {
        return interfaceID == type(IUserOpPolicy).interfaceId || interfaceID == type(IActionPolicy).interfaceId
            || interfaceID == type(I1271Policy).interfaceId || interfaceID == type(IERC165).interfaceId
            || interfaceID == type(IPolicy).interfaceId;
    }
}
