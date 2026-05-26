// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IAuthority} from "../IAuthority.sol";

/// @title DAOAuthority — EXAMPLE: Token governance with timelock
/// @notice MOCK IMPLEMENTATION FOR REFERENCE ONLY — NOT AUDITED FOR PRODUCTION
/// @dev Demonstrates how DAO governance integrates with IAuthority.
/// In production, use OpenZeppelin Governor + TimelockController, add proper
/// voting power snapshots, delegation, and quorum calculation.
///
/// Usage:
///   1. Deploy with governance token and timelock
///   2. Call hub.setAuthority(chainId, address(this))
///   3. Token holders propose + vote + execute sequencer changes via governance
contract DAOAuthority is IAuthority {

    struct Proposal {
        address proposer;
        address newSequencer;
        uint64 chainId;
        uint256 votesFor;
        uint256 votesAgainst;
        uint256 deadline;
        uint256 executeAfter;
        bool executed;
    }

    address public governanceToken;
    uint256 public votingPeriod;
    uint256 public timelockDelay;
    uint256 public proposalCount;

    mapping(uint256 => Proposal) public proposals;
    mapping(uint256 => mapping(address => bool)) public hasVoted;
    mapping(uint64 => address) public electedSequencers;
    mapping(uint64 => mapping(address => bool)) public authorizedProvers;

    event ProposalCreated(uint256 indexed id, uint64 indexed chainId, address newSequencer, uint256 deadline);
    event Voted(uint256 indexed id, address indexed voter, bool support);
    event ProposalExecuted(uint256 indexed id, uint64 indexed chainId, address newSequencer);

    constructor(address _token, uint256 _votingPeriod, uint256 _timelockDelay) {
        governanceToken = _token;
        votingPeriod = _votingPeriod;
        timelockDelay = _timelockDelay;
    }

    function propose(uint64 chainId, address newSequencer) external returns (uint256) {
        proposalCount++;
        proposals[proposalCount] = Proposal({
            proposer: msg.sender,
            newSequencer: newSequencer,
            chainId: chainId,
            votesFor: 0,
            votesAgainst: 0,
            deadline: block.timestamp + votingPeriod,
            executeAfter: block.timestamp + votingPeriod + timelockDelay,
            executed: false
        });
        emit ProposalCreated(proposalCount, chainId, newSequencer, block.timestamp + votingPeriod);
        return proposalCount;
    }

    function vote(uint256 proposalId, bool support) external {
        Proposal storage p = proposals[proposalId];
        require(block.timestamp < p.deadline, "Voting ended");
        require(!hasVoted[proposalId][msg.sender], "Already voted");
        hasVoted[proposalId][msg.sender] = true;

        if (support) {
            p.votesFor++;
        } else {
            p.votesAgainst++;
        }
        emit Voted(proposalId, msg.sender, support);
    }

    function execute(uint256 proposalId) external {
        Proposal storage p = proposals[proposalId];
        require(!p.executed, "Already executed");
        require(block.timestamp >= p.executeAfter, "Timelock not expired");
        require(p.votesFor > p.votesAgainst, "Proposal not passed");

        p.executed = true;
        electedSequencers[p.chainId] = p.newSequencer;
        emit ProposalExecuted(proposalId, p.chainId, p.newSequencer);
    }

    function setAuthorizedProver(uint64 chainId, address prover, bool authorized) external {
        require(msg.sender == electedSequencers[chainId], "Only elected sequencer");
        authorizedProvers[chainId][prover] = authorized;
    }

    function canSubmitAnchor(uint64 chainId, address caller) external view returns (bool) {
        return electedSequencers[chainId] == caller;
    }

    function canProve(uint64 chainId, address caller) external view returns (bool) {
        return authorizedProvers[chainId][caller] || electedSequencers[chainId] == caller;
    }

    function canManageChain(uint64 chainId, address caller) external view returns (bool) {
        return electedSequencers[chainId] == caller;
    }
}
