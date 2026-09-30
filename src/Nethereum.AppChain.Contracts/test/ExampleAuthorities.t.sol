// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "forge-std/Test.sol";
import "../src/examples/CliqueAuthority.sol";
import "../src/examples/MultisigAuthority.sol";
import "../src/examples/StakeWeightedAuthority.sol";
import "../src/examples/DAOAuthority.sol";

// ═══════════════════════════════════════════════════════
//  CLIQUE AUTHORITY
// ═══════════════════════════════════════════════════════

contract CliqueAuthorityTest is Test {
    CliqueAuthority auth;
    uint64 constant CHAIN = 42;
    address v1 = address(0xA1);
    address v2 = address(0xA2);
    address v3 = address(0xA3);

    function setUp() public {
        auth = new CliqueAuthority();
        auth.addSigner(CHAIN, v1);
        auth.addSigner(CHAIN, v2);
        auth.addSigner(CHAIN, v3);
    }

    function test_RoundRobin_CorrectTurn() public {
        vm.roll(0);
        assertTrue(auth.canSubmitAnchor(CHAIN, v1));
        assertFalse(auth.canSubmitAnchor(CHAIN, v2));
        assertFalse(auth.canSubmitAnchor(CHAIN, v3));

        vm.roll(1);
        assertFalse(auth.canSubmitAnchor(CHAIN, v1));
        assertTrue(auth.canSubmitAnchor(CHAIN, v2));

        vm.roll(2);
        assertTrue(auth.canSubmitAnchor(CHAIN, v3));

        vm.roll(3);
        assertTrue(auth.canSubmitAnchor(CHAIN, v1));
    }

    function test_RoundRobin_AllSignersCanProve() public {
        assertTrue(auth.canProve(CHAIN, v1));
        assertTrue(auth.canProve(CHAIN, v2));
        assertTrue(auth.canProve(CHAIN, v3));
        assertFalse(auth.canProve(CHAIN, address(0x999)));
    }

    function test_AddSigner_RevertDuplicate() public {
        vm.expectRevert("Already a signer");
        auth.addSigner(CHAIN, v1);
    }

    function test_RemoveSigner_ReducesRotation() public {
        auth.removeSigner(CHAIN, v2);
        assertEq(auth.signerCount(CHAIN), 2);

        vm.roll(0);
        assertTrue(auth.canSubmitAnchor(CHAIN, v1));
        vm.roll(1);
        assertTrue(auth.canSubmitAnchor(CHAIN, v3));
        vm.roll(2);
        assertTrue(auth.canSubmitAnchor(CHAIN, v1));
    }

    function test_RemoveSigner_RemovedCannotSubmit() public {
        auth.removeSigner(CHAIN, v1);
        for (uint256 i = 0; i < 10; i++) {
            vm.roll(i);
            assertFalse(auth.canSubmitAnchor(CHAIN, v1));
        }
    }

    function test_RemoveSigner_RevertNotSigner() public {
        vm.expectRevert("Not a signer");
        auth.removeSigner(CHAIN, address(0x999));
    }

    function test_NoSigners_RejectAll() public {
        auth.removeSigner(CHAIN, v1);
        auth.removeSigner(CHAIN, v2);
        auth.removeSigner(CHAIN, v3);
        assertFalse(auth.canSubmitAnchor(CHAIN, v1));
    }

    function test_OnlyOwner_AddRemove() public {
        vm.prank(v1);
        vm.expectRevert("Only owner");
        auth.addSigner(CHAIN, address(0xBEEF));

        vm.prank(v1);
        vm.expectRevert("Only owner");
        auth.removeSigner(CHAIN, v2);
    }

    function test_IsolatedPerChain() public {
        uint64 otherChain = 99;
        auth.addSigner(otherChain, address(0xB1));

        vm.roll(0);
        assertTrue(auth.canSubmitAnchor(CHAIN, v1));
        assertTrue(auth.canSubmitAnchor(otherChain, address(0xB1)));
        assertFalse(auth.canSubmitAnchor(otherChain, v1));
    }
}

// ═══════════════════════════════════════════════════════
//  MULTISIG AUTHORITY
// ═══════════════════════════════════════════════════════

contract MultisigAuthorityTest is Test {
    MultisigAuthority auth;
    address s1 = address(0xA1);
    address s2 = address(0xA2);
    address s3 = address(0xA3);
    uint64 constant CHAIN = 42;

    function setUp() public {
        address[] memory signers = new address[](3);
        signers[0] = s1;
        signers[1] = s2;
        signers[2] = s3;
        auth = new MultisigAuthority(signers, 2);
    }

    function test_Threshold_NotMetWithOne() public {
        bytes32 opHash = keccak256(abi.encodePacked("anchor", CHAIN, s1, block.number / 100));

        vm.prank(s1);
        auth.approve(opHash);

        assertFalse(auth.isApproved(opHash));
        assertFalse(auth.canSubmitAnchor(CHAIN, s1));
    }

    function test_Threshold_MetWithTwo() public {
        bytes32 opHash = keccak256(abi.encodePacked("anchor", CHAIN, s1, block.number / 100));

        vm.prank(s1);
        auth.approve(opHash);
        vm.prank(s2);
        auth.approve(opHash);

        assertTrue(auth.isApproved(opHash));
        assertTrue(auth.canSubmitAnchor(CHAIN, s1));
    }

    function test_NonSigner_CannotApprove() public {
        bytes32 opHash = keccak256("test");
        vm.prank(address(0x999));
        vm.expectRevert("Not a signer");
        auth.approve(opHash);
    }

    function test_DoubleApproval_Reverts() public {
        bytes32 opHash = keccak256("test");
        vm.prank(s1);
        auth.approve(opHash);
        vm.prank(s1);
        vm.expectRevert("Already approved");
        auth.approve(opHash);
    }

    function test_NonSigner_CannotSubmitAnchor() public {
        assertFalse(auth.canSubmitAnchor(CHAIN, address(0x999)));
    }

    function test_Constructor_RevertZeroThreshold() public {
        address[] memory signers = new address[](2);
        signers[0] = s1;
        signers[1] = s2;
        vm.expectRevert("Invalid threshold");
        new MultisigAuthority(signers, 0);
    }

    function test_Constructor_RevertThresholdExceedsSigners() public {
        address[] memory signers = new address[](2);
        signers[0] = s1;
        signers[1] = s2;
        vm.expectRevert("Invalid threshold");
        new MultisigAuthority(signers, 3);
    }

    function test_Constructor_RevertDuplicateSigner() public {
        address[] memory signers = new address[](2);
        signers[0] = s1;
        signers[1] = s1;
        vm.expectRevert("Duplicate signer");
        new MultisigAuthority(signers, 1);
    }
}

// ═══════════════════════════════════════════════════════
//  STAKE WEIGHTED AUTHORITY
// ═══════════════════════════════════════════════════════

contract StakeWeightedAuthorityTest is Test {
    StakeWeightedAuthority auth;
    address v1 = address(0xA1);
    address v2 = address(0xA2);
    uint64 constant CHAIN = 42;

    function setUp() public {
        auth = new StakeWeightedAuthority();
        vm.deal(v1, 10 ether);
        vm.deal(v2, 10 ether);
    }

    function test_Stake_RegistersValidator() public {
        vm.prank(v1);
        auth.stake{value: 1 ether}(CHAIN);
        assertEq(auth.validatorCount(CHAIN), 1);
        assertTrue(auth.canProve(CHAIN, v1));
    }

    function test_Stake_RevertBelowMinimum() public {
        vm.prank(v1);
        vm.expectRevert("Below minimum stake");
        auth.stake{value: 0.01 ether}(CHAIN);
    }

    function test_ElectLeader_HighestStakeWins() public {
        vm.prank(v1);
        auth.stake{value: 1 ether}(CHAIN);
        vm.prank(v2);
        auth.stake{value: 5 ether}(CHAIN);

        auth.electLeader(CHAIN);

        assertTrue(auth.canSubmitAnchor(CHAIN, v2));
        assertFalse(auth.canSubmitAnchor(CHAIN, v1));
    }

    function test_ElectLeader_RevertNoValidators() public {
        vm.expectRevert("No validators");
        auth.electLeader(CHAIN);
    }

    function test_ElectLeader_RevertEpochNotEnded() public {
        vm.prank(v1);
        auth.stake{value: 1 ether}(CHAIN);
        auth.electLeader(CHAIN);

        vm.expectRevert("Epoch not ended");
        auth.electLeader(CHAIN);
    }

    function test_ElectLeader_NewEpochAfterBlocks() public {
        vm.prank(v1);
        auth.stake{value: 1 ether}(CHAIN);
        auth.electLeader(CHAIN);
        assertTrue(auth.canSubmitAnchor(CHAIN, v1));

        vm.prank(v2);
        auth.stake{value: 10 ether}(CHAIN);

        vm.roll(block.number + 100);
        auth.electLeader(CHAIN);
        assertTrue(auth.canSubmitAnchor(CHAIN, v2));
        assertFalse(auth.canSubmitAnchor(CHAIN, v1));
    }

    function test_NoLeader_BeforeElection() public {
        vm.prank(v1);
        auth.stake{value: 1 ether}(CHAIN);
        assertFalse(auth.canSubmitAnchor(CHAIN, v1));
    }

    function test_AdditionalStake_Accumulates() public {
        vm.prank(v1);
        auth.stake{value: 1 ether}(CHAIN);
        vm.prank(v1);
        auth.stake{value: 2 ether}(CHAIN);
        assertEq(auth.validatorCount(CHAIN), 1);

        (uint256 stake, ) = auth.validators(CHAIN, v1);
        assertEq(stake, 3 ether);
    }
}

// ═══════════════════════════════════════════════════════
//  DAO AUTHORITY
// ═══════════════════════════════════════════════════════

contract DAOAuthorityTest is Test {
    DAOAuthority auth;
    address token = address(0x7041);
    address proposer = address(0xA1);
    address voter1 = address(0xA2);
    address voter2 = address(0xA3);
    address voter3 = address(0xA4);
    address newSeq = address(0xBEEF);
    uint64 constant CHAIN = 42;
    uint256 constant VOTING_PERIOD = 3 days;
    uint256 constant TIMELOCK = 1 days;

    function setUp() public {
        auth = new DAOAuthority(token, VOTING_PERIOD, TIMELOCK);
    }

    function test_Propose_CreatesProposal() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);
        assertEq(id, 1);

        (address p, address seq, uint64 chain, , , , , bool executed) = auth.proposals(id);
        assertEq(p, proposer);
        assertEq(seq, newSeq);
        assertEq(chain, CHAIN);
        assertFalse(executed);
    }

    function test_Vote_RecordsCorrectly() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);

        vm.prank(voter1);
        auth.vote(id, true);
        vm.prank(voter2);
        auth.vote(id, true);
        vm.prank(voter3);
        auth.vote(id, false);

        (, , , uint256 votesFor, uint256 votesAgainst, , , ) = auth.proposals(id);
        assertEq(votesFor, 2);
        assertEq(votesAgainst, 1);
    }

    function test_Vote_RevertAfterDeadline() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);

        vm.warp(block.timestamp + VOTING_PERIOD + 1);
        vm.prank(voter1);
        vm.expectRevert("Voting ended");
        auth.vote(id, true);
    }

    function test_Vote_RevertDoubleVote() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);

        vm.prank(voter1);
        auth.vote(id, true);
        vm.prank(voter1);
        vm.expectRevert("Already voted");
        auth.vote(id, true);
    }

    function test_Execute_AfterTimelock() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);

        vm.prank(voter1);
        auth.vote(id, true);

        vm.warp(block.timestamp + VOTING_PERIOD + TIMELOCK + 1);
        auth.execute(id);

        assertTrue(auth.canSubmitAnchor(CHAIN, newSeq));
        assertFalse(auth.canSubmitAnchor(CHAIN, proposer));
    }

    function test_Execute_RevertBeforeTimelock() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);

        vm.prank(voter1);
        auth.vote(id, true);

        vm.warp(block.timestamp + VOTING_PERIOD);
        vm.expectRevert("Timelock not expired");
        auth.execute(id);
    }

    function test_Execute_RevertNotPassed() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);

        vm.prank(voter1);
        auth.vote(id, false);

        vm.warp(block.timestamp + VOTING_PERIOD + TIMELOCK + 1);
        vm.expectRevert("Proposal not passed");
        auth.execute(id);
    }

    function test_Execute_RevertDouble() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);

        vm.prank(voter1);
        auth.vote(id, true);

        vm.warp(block.timestamp + VOTING_PERIOD + TIMELOCK + 1);
        auth.execute(id);

        vm.expectRevert("Already executed");
        auth.execute(id);
    }

    function test_FullGovernanceCycle() public {
        assertFalse(auth.canSubmitAnchor(CHAIN, newSeq));

        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);

        vm.prank(voter1);
        auth.vote(id, true);
        vm.prank(voter2);
        auth.vote(id, true);
        vm.prank(voter3);
        auth.vote(id, false);

        vm.warp(block.timestamp + VOTING_PERIOD + TIMELOCK + 1);
        auth.execute(id);

        assertTrue(auth.canSubmitAnchor(CHAIN, newSeq));

        vm.prank(newSeq);
        auth.setAuthorizedProver(CHAIN, address(0xA40), true);
        assertTrue(auth.canProve(CHAIN, address(0xA40)));
    }

    function test_ElectedSequencer_CanManageChain() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);
        vm.prank(voter1);
        auth.vote(id, true);
        vm.warp(block.timestamp + VOTING_PERIOD + TIMELOCK + 1);
        auth.execute(id);

        assertTrue(auth.canManageChain(CHAIN, newSeq));
        assertFalse(auth.canManageChain(CHAIN, proposer));
    }

    function test_SetAuthorizedProver_OnlyElectedSequencer() public {
        vm.prank(proposer);
        uint256 id = auth.propose(CHAIN, newSeq);
        vm.prank(voter1);
        auth.vote(id, true);
        vm.warp(block.timestamp + VOTING_PERIOD + TIMELOCK + 1);
        auth.execute(id);

        vm.prank(proposer);
        vm.expectRevert("Only elected sequencer");
        auth.setAuthorizedProver(CHAIN, address(0x123), true);
    }
}
