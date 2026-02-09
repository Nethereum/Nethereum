// SPDX-License-Identifier: MIT

pragma solidity ^0.8.23;

import "../DataTypes.sol";
import { IActionPolicy, IPolicy, VALIDATION_SUCCESS, VALIDATION_FAILED } from "../interfaces/IPolicy.sol";
import { IERC20 } from "forge-std/interfaces/IERC20.sol";
import { IERC165 } from "forge-std/interfaces/IERC165.sol";
import { EnumerableSet } from "../utils/EnumerableSet4337.sol";

contract ERC20SpendingLimitPolicy is IActionPolicy {
    using EnumerableSet for EnumerableSet.AddressSet;

    event TokenSpent(
        ConfigId id, address multiplexer, address token, address account, uint256 amount, uint256 remaining
    );

    error InvalidTokenAddress(address token);
    error InvalidLimit(uint256 limit);

    struct TokenPolicyData {
        uint256 alreadySpent;
        uint256 spendingLimit;
    }

    mapping(ConfigId id => mapping(address multiplexer => EnumerableSet.AddressSet tokensEnabled)) internal $tokens;
    mapping(
        ConfigId id
            => mapping(
                address mulitplexer => mapping(address token => mapping(address userOpSender => TokenPolicyData))
            )
    ) internal $policyData;

    function _getPolicy(
        ConfigId id,
        address userOpSender,
        address token
    )
        internal
        view
        returns (TokenPolicyData storage s)
    {
        if (token == address(0)) revert InvalidTokenAddress(token);
        s = $policyData[id][msg.sender][token][userOpSender];
    }

    function supportsInterface(bytes4 interfaceID) external pure override returns (bool) {
        return (
            interfaceID == type(IERC165).interfaceId || interfaceID == type(IPolicy).interfaceId
                || interfaceID == type(IActionPolicy).interfaceId
        );
    }

    /**
     * Initializes the policy to be used by given account through multiplexer (msg.sender) such as Smart Sessions.
     * Overwrites state.
     * @notice ATTENTION: This method is called during permission installation as part of the enabling policies flow.
     * A secure policy would minimize external calls from this method (ideally, to 0) to prevent passing control flow to
     * external contracts.
     */
    function initializeWithMultiplexer(address account, ConfigId configId, bytes calldata initData) external {
        (address[] memory tokens, uint256[] memory limits) = abi.decode(initData, (address[], uint256[]));
        EnumerableSet.AddressSet storage $t = $tokens[configId][msg.sender];

        uint256 length_i = $t.length(account);

        // if there's some inited tokens, clear storage first
        if (length_i > 0) {
            for (uint256 i; i < length_i; i++) {
                // for all tokens which have been inited for a given configId and mxer
                address token = $t.at(account, i);
                TokenPolicyData storage $ = _getPolicy({ id: configId, userOpSender: account, token: token });
                // clear limit and spent
                $.spendingLimit = 0;
                $.alreadySpent = 0;
            }
            // clear inited tokens
            $t.removeAll(account);
        }

        // set new
        for (uint256 i; i < tokens.length; i++) {
            address token = tokens[i];
            uint256 limit = limits[i];
            if (token == address(0)) revert InvalidTokenAddress(token);
            if (limit == 0) revert InvalidLimit(limit);
            TokenPolicyData storage $ = _getPolicy({ id: configId, userOpSender: account, token: token });
            // set limit
            $.spendingLimit = limit;
            // mark token as inited
            $t.add(account, token);
        }
        emit IPolicy.PolicySet(configId, msg.sender, account);
    }

    function _isTokenTransfer(
        address account,
        bytes calldata callData
    )
        internal
        pure
        returns (bool isTransfer, uint256 amount)
    {
        bytes4 functionSelector = bytes4(callData[0:4]);

        if (functionSelector == IERC20.approve.selector) {
            (, amount) = abi.decode(callData[4:], (address, uint256));
            return (true, amount);
        } else if (functionSelector == IERC20.transfer.selector) {
            (, amount) = abi.decode(callData[4:], (address, uint256));
            return (true, amount);
        } else if (functionSelector == IERC20.transferFrom.selector) {
            (,, amount) = abi.decode(callData[4:], (address, address, uint256));
            return (true, amount);
        }
        return (false, 0);
    }

    function checkAction(
        ConfigId id,
        address account,
        address target,
        uint256 value,
        bytes calldata callData
    )
        external
        override
        returns (uint256)
    {
        if (value != 0) return VALIDATION_FAILED;
        (bool isTokenTransfer, uint256 amount) = _isTokenTransfer(account, callData);
        if (!isTokenTransfer) return VALIDATION_FAILED;

        TokenPolicyData storage $ = _getPolicy({ id: id, userOpSender: account, token: target });

        uint256 spendingLimit = $.spendingLimit;
        uint256 alreadySpent = $.alreadySpent;

        uint256 newAmount = alreadySpent + amount;

        if (newAmount > spendingLimit) {
            return VALIDATION_FAILED;
        } else {
            $.alreadySpent = newAmount;

            emit TokenSpent(id, msg.sender, target, account, amount, spendingLimit - newAmount);
            return VALIDATION_SUCCESS;
        }
    }
}
