// SPDX-License-Identifier: MIT

pragma solidity ^0.8.27;

import "../DataTypes.sol";
import { IActionPolicy, IPolicy, VALIDATION_SUCCESS, VALIDATION_FAILED } from "../interfaces/IPolicy.sol";
import { SubModuleLib } from "../lib/SubModuleLib.sol";
import { IERC165 } from "forge-std/interfaces/IERC165.sol";

error PolicyNotInitialized(ConfigId id, address mxer, address account);
error ValueLimitExceeded(ConfigId id, uint256 value, uint256 limit);

struct ActionConfig {
    uint256 valueLimitPerUse;
    ParamRules paramRules;
}

struct ParamRules {
    uint256 length;
    ParamRule[16] rules;
}

struct ParamRule {
    ParamCondition condition;
    uint64 offset;
    bool isLimited;
    bytes32 ref;
    LimitUsage usage;
}

struct LimitUsage {
    uint256 limit;
    uint256 used;
}

enum ParamCondition {
    EQUAL,
    GREATER_THAN,
    LESS_THAN,
    GREATER_THAN_OR_EQUAL,
    LESS_THAN_OR_EQUAL,
    NOT_EQUAL,
    IN_RANGE
}

/**
 * @title UniActionPolicy: Universal Action Policy
 * @dev A policy that allows defining custom rules for actions based on function signatures.
 * Rules can be configured for function arguments with conditions.
 * So the argument is compared to a reference value against the the condition.
 * Also, rules feature usage limits for arguments.
 * For example, you can limit not just max amount for a transfer,
 * but also limit the total amount to be transferred within a permission.
 * Limit is uint256 so you can control any kind of numerable params.
 *
 * If you need to deal with dynamic-length arguments, such as bytes, please refer to
 * https://docs.soliditylang.org/en/v0.8.24/abi-spec.html#function-selector-and-argument-encoding
 * to learn more about how dynamic arguments are represented in the calldata
 * and which offsets should be used to access them.
 */
contract UniActionPolicy is IActionPolicy {
    enum Status {
        NA,
        Live,
        Deprecated
    }

    using SubModuleLib for bytes;
    using UniActionLib for *;

    mapping(ConfigId id => mapping(address msgSender => mapping(address userOpSender => ActionConfig))) public
        actionConfigs;

    /**
     * @dev Checks if the action is allowed based on the args rules defined in the policy.
     */
    function checkAction(
        ConfigId id,
        address account,
        address,
        uint256 value,
        bytes calldata data
    )
        external
        returns (uint256)
    {
        ActionConfig storage config = actionConfigs[id][msg.sender][account];
        require(config.paramRules.length > 0, PolicyNotInitialized(id, msg.sender, account));
        require(value <= config.valueLimitPerUse, ValueLimitExceeded(id, value, config.valueLimitPerUse));
        uint256 length = config.paramRules.length;
        for (uint256 i = 0; i < length; i++) {
            if (!config.paramRules.rules[i].check(data)) return VALIDATION_FAILED;
        }

        return VALIDATION_SUCCESS;
    }

    function _initPolicy(ConfigId id, address mxer, address opSender, bytes calldata _data) internal {
        ActionConfig memory config = abi.decode(_data, (ActionConfig));
        actionConfigs[id][mxer][opSender].fill(config);
    }

    /**
     * Initializes the policy to be used by given account through multiplexer (msg.sender) such as Smart Sessions.
     * Overwrites state.
     * @notice ATTENTION: This method is called during permission installation as part of the enabling policies flow.
     * A secure policy would minimize external calls from this method (ideally, to 0) to prevent passing control flow to
     * external contracts.
     */
    function initializeWithMultiplexer(address account, ConfigId configId, bytes calldata initData) external {
        _initPolicy(configId, msg.sender, account, initData);
        emit IPolicy.PolicySet(configId, msg.sender, account);
    }

    function supportsInterface(bytes4 interfaceID) external pure override returns (bool) {
        return (
            interfaceID == type(IERC165).interfaceId || interfaceID == type(IPolicy).interfaceId
                || interfaceID == type(IActionPolicy).interfaceId
        );
    }
}

library UniActionLib {
    /**
     * @dev parses the function arg from the calldata based on the offset
     * and compares it to the reference value based on the condition.
     * Also checks if the limit is reached/exceeded.
     */
    function check(ParamRule storage rule, bytes calldata data) internal returns (bool) {
        bytes32 param = bytes32(data[4 + rule.offset:4 + rule.offset + 32]);

        // CHECK ParamCondition
        if (rule.condition == ParamCondition.EQUAL && param != rule.ref) {
            return false;
        } else if (rule.condition == ParamCondition.GREATER_THAN && param <= rule.ref) {
            return false;
        } else if (rule.condition == ParamCondition.LESS_THAN && param >= rule.ref) {
            return false;
        } else if (rule.condition == ParamCondition.GREATER_THAN_OR_EQUAL && param < rule.ref) {
            return false;
        } else if (rule.condition == ParamCondition.LESS_THAN_OR_EQUAL && param > rule.ref) {
            return false;
        } else if (rule.condition == ParamCondition.NOT_EQUAL && param == rule.ref) {
            return false;
        } else if (rule.condition == ParamCondition.IN_RANGE) {
            // in this case rule.ref is abi.encodePacked(uint128(min), uint128(max))
            if (
                param < (rule.ref >> 128)
                    || param > (rule.ref & 0x00000000000000000000000000000000ffffffffffffffffffffffffffffffff)
            ) {
                return false;
            }
        }

        // CHECK PARAM LIMIT
        if (rule.isLimited) {
            if (rule.usage.used + uint256(param) > rule.usage.limit) {
                return false;
            }
            rule.usage.used += uint256(param);
        }
        return true;
    }

    function fill(ActionConfig storage $config, ActionConfig memory config) internal {
        $config.valueLimitPerUse = config.valueLimitPerUse;
        $config.paramRules.length = config.paramRules.length;
        for (uint256 i; i < config.paramRules.length; i++) {
            $config.paramRules.rules[i] = config.paramRules.rules[i];
        }
    }
}

/**
 * Further development:
 *
 *   - Add compound value limit.
 *     struct ActionConfig {
 *         uint256 valueLimitPerUse;
 *         uint256 totalValueLimit;
 *         uint256 valueUsed;
 *         ParamRules paramRules;
 *     }
 *
 *     - Add param relations.
 *
 *     Add this to ActionConfig => Relation[] paramRelations;
 *         struct Relation {
 *             address verifier;
 *             bytes4 selector;
 *             bytes1 argsAmount;
 *             uint64[4] offsets;
 *             bytes32 context;
 *         }
 *     Add checking for relations.
 */
