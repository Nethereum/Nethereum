// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IAuthority} from "../IAuthority.sol";

/// @title StakeWeightedAuthority — EXAMPLE: Stake-weighted validator election
/// @notice MOCK IMPLEMENTATION FOR REFERENCE ONLY — NOT AUDITED FOR PRODUCTION
/// @dev Demonstrates how a stake-weighted election integrates with IAuthority.
/// In production, add proper randomness (VRF), slashing conditions, unbonding periods,
/// and delegation support.
///
/// Usage:
///   1. Deploy and register as authority
///   2. Validators stake ETH via stake()
///   3. Highest-staked validator is the current leader (simplified — production would use
///      randomised election per epoch)
contract StakeWeightedAuthority is IAuthority {

    struct Validator {
        uint256 stake;
        bool active;
    }

    address public owner;
    mapping(uint64 => mapping(address => Validator)) public validators;
    mapping(uint64 => address[]) public validatorList;
    mapping(uint64 => address) public currentLeader;
    mapping(uint64 => uint256) public epochEndBlock;
    uint256 public constant EPOCH_LENGTH = 100;
    uint256 public constant MIN_STAKE = 0.1 ether;

    event Staked(uint64 indexed chainId, address indexed validator, uint256 amount);
    event Unstaked(uint64 indexed chainId, address indexed validator, uint256 amount);
    event LeaderElected(uint64 indexed chainId, address indexed leader, uint256 epoch);

    constructor() { owner = msg.sender; }

    function stake(uint64 chainId) external payable {
        require(msg.value >= MIN_STAKE, "Below minimum stake");
        Validator storage v = validators[chainId][msg.sender];
        if (!v.active) {
            v.active = true;
            validatorList[chainId].push(msg.sender);
        }
        v.stake += msg.value;
        emit Staked(chainId, msg.sender, msg.value);
    }

    function electLeader(uint64 chainId) external {
        require(block.number >= epochEndBlock[chainId], "Epoch not ended");

        address[] storage vList = validatorList[chainId];
        require(vList.length > 0, "No validators");

        address best = vList[0];
        uint256 bestStake = validators[chainId][vList[0]].stake;
        for (uint256 i = 1; i < vList.length; i++) {
            uint256 s = validators[chainId][vList[i]].stake;
            if (s > bestStake) {
                best = vList[i];
                bestStake = s;
            }
        }

        currentLeader[chainId] = best;
        epochEndBlock[chainId] = block.number + EPOCH_LENGTH;
        emit LeaderElected(chainId, best, block.number / EPOCH_LENGTH);
    }

    function canSubmitAnchor(uint64 chainId, address caller) external view returns (bool) {
        return currentLeader[chainId] == caller;
    }

    function canProve(uint64 chainId, address caller) external view returns (bool) {
        return validators[chainId][caller].active;
    }

    function canManageChain(uint64, address caller) external view returns (bool) {
        return caller == owner;
    }

    function validatorCount(uint64 chainId) external view returns (uint256) {
        return validatorList[chainId].length;
    }
}
