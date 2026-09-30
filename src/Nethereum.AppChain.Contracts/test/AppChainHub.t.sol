// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "forge-std/Test.sol";
import "../src/AppChainHub.sol";

contract AppChainHubTest is Test {
    AppChainHub hub;

    address hubOwner;
    uint256 sequencerPk = 0xA11CE;
    address sequencer;
    address sender = address(0x3);
    address target = address(0x4);
    uint64 constant CHAIN_ID = 42;
    uint256 constant REG_FEE = 0.01 ether;
    uint256 constant MSG_FEE = 0.001 ether;
    uint256 constant HUB_BPS = 500;

    receive() external payable {}

    function setUp() public {
        hubOwner = address(this);
        sequencer = vm.addr(sequencerPk);
        hub = new AppChainHub(REG_FEE, MSG_FEE, HUB_BPS);

        bytes memory sig = _signRegistration(CHAIN_ID, hubOwner, sequencerPk);
        hub.registerAppChain{value: REG_FEE}(CHAIN_ID, sequencer, sig);
        hub.setAuthorizedSender(CHAIN_ID, sender, true);

        vm.deal(sender, 100 ether);
        vm.deal(hubOwner, 100 ether);
    }

    function _signRegistration(uint64 chainId, address owner, uint256 pk) internal pure returns (bytes memory) {
        bytes32 hash = keccak256(abi.encodePacked(chainId, owner));
        bytes32 ethHash = keccak256(abi.encodePacked("\x19Ethereum Signed Message:\n32", hash));
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(pk, ethHash);
        return abi.encodePacked(r, s, v);
    }

    function _sendOneMessage() internal {
        vm.prank(sender);
        hub.sendMessage{value: MSG_FEE}(1, CHAIN_ID, target, "hello");
    }

    // ═══════════════════════════════════════════
    //  REGISTRATION
    // ═══════════════════════════════════════════

    function test_Register_Success() public view {
        (, address seq, , , uint64 nextId, bool registered) = hub.getAppChainInfo(CHAIN_ID);
        assertTrue(registered);
        assertEq(seq, sequencer);
        assertEq(nextId, 1);
    }

    function test_Register_RevertDuplicate() public {
        vm.expectRevert("Already registered");
        hub.registerAppChain{value: REG_FEE}(CHAIN_ID, sequencer, _signRegistration(CHAIN_ID, hubOwner, sequencerPk));
    }

    function test_Register_RevertInsufficientFee() public {
        vm.expectRevert("Insufficient fee");
        hub.registerAppChain{value: REG_FEE - 1}(100, sequencer, _signRegistration(100, hubOwner, sequencerPk));
    }

    function test_Register_RevertZeroSequencer() public {
        vm.expectRevert("Invalid sequencer");
        hub.registerAppChain{value: REG_FEE}(100, address(0), "");
    }

    function test_Register_RevertWrongSignature() public {
        uint256 wrongPk = 0xDEAD;
        vm.expectRevert("Invalid sequencer signature");
        hub.registerAppChain{value: REG_FEE}(100, sequencer, _signRegistration(100, hubOwner, wrongPk));
    }

    // ═══════════════════════════════════════════
    //  SECURITY: ECDSA s-value malleability (EIP-2)
    // ═══════════════════════════════════════════

    function test_Register_RevertMalleableSignature() public {
        uint64 newChain = 200;
        bytes32 hash = keccak256(abi.encodePacked(newChain, hubOwner));
        bytes32 ethHash = keccak256(abi.encodePacked("\x19Ethereum Signed Message:\n32", hash));
        (uint8 v, bytes32 r, bytes32 s) = vm.sign(sequencerPk, ethHash);

        uint256 secp256k1n = 0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141;
        bytes32 sMalleable = bytes32(secp256k1n - uint256(s));
        uint8 vMalleable = v == 27 ? 28 : 27;

        vm.expectRevert("Invalid signature s");
        hub.registerAppChain{value: REG_FEE}(newChain, sequencer, abi.encodePacked(r, sMalleable, vMalleable));
    }

    // ═══════════════════════════════════════════
    //  MESSAGING: happy path + access control
    // ═══════════════════════════════════════════

    function test_SendMessage_StoresCorrectly() public {
        _sendOneMessage();

        (uint64 srcChain, address msgSender, , address msgTarget, bytes memory data, uint256 ts) = hub.getMessage(CHAIN_ID, 1);
        assertEq(srcChain, 1);
        assertEq(msgSender, sender);
        assertEq(msgTarget, target);
        assertEq(data, "hello");
        assertTrue(ts > 0);
    }

    function test_SendMessage_IncrementsMessageId() public {
        _sendOneMessage();
        (, , , , uint64 nextId, ) = hub.getAppChainInfo(CHAIN_ID);
        assertEq(nextId, 2);

        _sendOneMessage();
        (, , , , nextId, ) = hub.getAppChainInfo(CHAIN_ID);
        assertEq(nextId, 3);
    }

    function test_SendMessage_RevertUnauthorized() public {
        address rando = address(0x999);
        vm.deal(rando, 1 ether);
        vm.prank(rando);
        vm.expectRevert("Not authorized");
        hub.sendMessage{value: MSG_FEE}(1, CHAIN_ID, target, "hello");
    }

    function test_SendMessage_RevertInsufficientFee() public {
        vm.prank(sender);
        vm.expectRevert("Insufficient fee");
        hub.sendMessage{value: MSG_FEE - 1}(1, CHAIN_ID, target, "hello");
    }

    function test_SendMessage_RevertZeroTarget() public {
        vm.prank(sender);
        vm.expectRevert("Invalid target");
        hub.sendMessage{value: MSG_FEE}(1, CHAIN_ID, address(0), "hello");
    }

    function test_SendMessage_RevertUnregisteredChain() public {
        vm.prank(sender);
        vm.expectRevert("Target AppChain not registered");
        hub.sendMessage{value: MSG_FEE}(1, 999, target, "hello");
    }

    function test_SendMessage_RevertMessageTooLarge() public {
        vm.prank(sender);
        vm.expectRevert("Message too large");
        hub.sendMessage{value: MSG_FEE}(1, CHAIN_ID, target, new bytes(10241));
    }

    // ═══════════════════════════════════════════
    //  MESSAGING: fee accounting
    // ═══════════════════════════════════════════

    function test_SendMessage_FeesSplitCorrectly() public {
        uint256 hubBalBefore = hub.hubBalance();
        uint256 ownerBalBefore = hub.ownerBalances(CHAIN_ID);

        _sendOneMessage();

        uint256 expectedHubCut = MSG_FEE * HUB_BPS / 10000;
        uint256 expectedOwnerCut = MSG_FEE - expectedHubCut;

        assertEq(hub.hubBalance() - hubBalBefore, expectedHubCut);
        assertEq(hub.ownerBalances(CHAIN_ID) - ownerBalBefore, expectedOwnerCut);
    }

    function test_SendMessage_RefundsExactExcess() public {
        uint256 excess = 0.05 ether;
        uint256 balBefore = sender.balance;
        vm.prank(sender);
        hub.sendMessage{value: MSG_FEE + excess}(1, CHAIN_ID, target, "hello");
        assertEq(balBefore - sender.balance, MSG_FEE);
    }

    // ═══════════════════════════════════════════
    //  SECURITY: refund to smart contract wallet
    //  (validates call{value} instead of transfer)
    // ═══════════════════════════════════════════

    function test_SendMessage_RefundToContractWallet() public {
        ExpensiveReceiveWallet wallet = new ExpensiveReceiveWallet();
        vm.deal(address(wallet), 1 ether);
        hub.setAuthorizedSender(CHAIN_ID, address(wallet), true);

        vm.prank(address(wallet));
        hub.sendMessage{value: MSG_FEE + 0.01 ether}(1, CHAIN_ID, target, "hello");
        assertEq(address(wallet).balance, 1 ether - MSG_FEE);
    }

    function test_WithdrawFees_ToExpensiveWallet() public {
        _sendOneMessage();
        uint256 fees = hub.ownerBalances(CHAIN_ID);
        assertTrue(fees > 0);

        ExpensiveReceiveWallet wallet = new ExpensiveReceiveWallet();
        hub.transferAppChainOwnership(CHAIN_ID, address(wallet));

        vm.prank(address(wallet));
        hub.withdrawFees(CHAIN_ID);
        assertEq(address(wallet).balance, fees);
        assertEq(hub.ownerBalances(CHAIN_ID), 0);
    }

    // ═══════════════════════════════════════════
    //  SECURITY: reentrancy on withdrawals
    // ═══════════════════════════════════════════

    function test_WithdrawFees_ReentrantAttackFails() public {
        _sendOneMessage();
        uint256 fees = hub.ownerBalances(CHAIN_ID);
        assertTrue(fees > 0);

        ReentrantWithdrawer attacker = new ReentrantWithdrawer(hub, CHAIN_ID);
        hub.transferAppChainOwnership(CHAIN_ID, address(attacker));

        vm.prank(address(attacker));
        vm.expectRevert();
        hub.withdrawFees(CHAIN_ID);

        assertEq(hub.ownerBalances(CHAIN_ID), fees, "Balance must not change after failed reentrancy");
    }

    // ═══════════════════════════════════════════
    //  SECURITY: double-withdraw (balance zeroed)
    // ═══════════════════════════════════════════

    function test_WithdrawFees_RevertDoubleWithdraw() public {
        _sendOneMessage();
        hub.withdrawFees(CHAIN_ID);
        vm.expectRevert("No fees to withdraw");
        hub.withdrawFees(CHAIN_ID);
    }

    function test_WithdrawHubFees_RevertDoubleWithdraw() public {
        hub.withdrawHubFees();
        vm.expectRevert("No fees to withdraw");
        hub.withdrawHubFees();
    }

    // ═══════════════════════════════════════════
    //  ANCHORING
    // ═══════════════════════════════════════════

    function test_Anchor_Success() public {
        vm.prank(sequencer);
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");

        (bytes32 stateRoot, , , uint256 ts, ) = hub.getAnchor(CHAIN_ID, 10);
        assertEq(stateRoot, keccak256("s"));
        assertTrue(ts > 0);
    }

    function test_Anchor_RevertNotSequencer() public {
        vm.prank(sender);
        vm.expectRevert("Only sequencer");
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");
    }

    function test_Anchor_RevertBlockRegression() public {
        vm.prank(sequencer);
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");

        vm.prank(sequencer);
        vm.expectRevert("Block already anchored");
        hub.anchor(CHAIN_ID, 5, keccak256("s2"), keccak256("t2"), keccak256("r2"), 0, "");
    }

    function test_Anchor_RevertSameBlock() public {
        vm.prank(sequencer);
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");

        vm.prank(sequencer);
        vm.expectRevert("Block already anchored");
        hub.anchor(CHAIN_ID, 10, keccak256("s2"), keccak256("t2"), keccak256("r2"), 0, "");
    }

    function test_Anchor_CrossChainSpoofing_Reverts() public {
        uint64 otherChain = 99;
        bytes memory sig = _signRegistration(otherChain, hubOwner, sequencerPk);
        hub.registerAppChain{value: REG_FEE}(otherChain, sequencer, sig);

        vm.prank(sender);
        vm.expectRevert("Only sequencer");
        hub.anchor(otherChain, 1, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");
    }

    // ═══════════════════════════════════════════
    //  ACKNOWLEDGMENT
    // ═══════════════════════════════════════════

    function test_Acknowledge_Success() public {
        _sendOneMessage();
        vm.prank(sequencer);
        hub.acknowledgeMessages(CHAIN_ID, 1, keccak256("root"));

        (uint64 processed, bytes32 root) = hub.getMessageRootCheckpoint(CHAIN_ID);
        assertEq(processed, 1);
        assertEq(root, keccak256("root"));
    }

    function test_Acknowledge_RevertNotSequencer() public {
        _sendOneMessage();
        vm.prank(sender);
        vm.expectRevert("Only sequencer");
        hub.acknowledgeMessages(CHAIN_ID, 1, keccak256("root"));
    }

    function test_Acknowledge_RevertExceedsSentMessages() public {
        _sendOneMessage();
        vm.prank(sequencer);
        vm.expectRevert("processedUpToMessageId exceeds sent messages");
        hub.acknowledgeMessages(CHAIN_ID, 5, keccak256("root"));
    }

    function test_Acknowledge_RevertZeroRoot() public {
        _sendOneMessage();
        vm.prank(sequencer);
        vm.expectRevert("Invalid messages root");
        hub.acknowledgeMessages(CHAIN_ID, 1, bytes32(0));
    }

    function test_Acknowledge_RevertNoNewMessages() public {
        _sendOneMessage();
        vm.prank(sequencer);
        hub.acknowledgeMessages(CHAIN_ID, 1, keccak256("root1"));

        vm.prank(sequencer);
        vm.expectRevert("No new messages processed");
        hub.acknowledgeMessages(CHAIN_ID, 1, keccak256("root2"));
    }

    // ═══════════════════════════════════════════
    //  OWNERSHIP + EVENTS
    // ═══════════════════════════════════════════

    function test_TransferAppChainOwnership_EmitsEvent() public {
        address newOwner = address(0x777);
        vm.expectEmit(true, true, true, true);
        emit AppChainOwnershipTransferred(CHAIN_ID, hubOwner, newOwner);
        hub.transferAppChainOwnership(CHAIN_ID, newOwner);
    }

    function test_TransferAppChainOwnership_RevertZeroAddress() public {
        vm.expectRevert("Invalid owner");
        hub.transferAppChainOwnership(CHAIN_ID, address(0));
    }

    function test_TransferAppChainOwnership_OldOwnerLosesAccess() public {
        address newOwner = address(0x777);
        hub.transferAppChainOwnership(CHAIN_ID, newOwner);

        vm.expectRevert("Only AppChain owner");
        hub.setAuthorizedSender(CHAIN_ID, address(0x888), true);
    }

    event AppChainOwnershipTransferred(uint64 indexed chainId, address indexed oldOwner, address indexed newOwner);

    // ═══════════════════════════════════════════
    //  ADMIN ACCESS CONTROL
    // ═══════════════════════════════════════════

    function test_SetFees_RevertNonOwner() public {
        vm.prank(sender);
        vm.expectRevert("Only hub owner");
        hub.setRegistrationFee(1 ether);
    }

    function test_SetHubFeeBps_RevertOver100Percent() public {
        vm.expectRevert("Hub fee cannot exceed 100%");
        hub.setHubFeeBps(10001);
    }

    function test_TransferHubOwnership_RevertZeroAddress() public {
        vm.expectRevert("Invalid owner");
        hub.transferHubOwnership(address(0));
    }

    function test_TransferHubOwnership_OldOwnerLosesAccess() public {
        address newOwner = address(0x888);
        hub.transferHubOwnership(newOwner);

        vm.expectRevert("Only hub owner");
        hub.setRegistrationFee(999);
    }

    // ═══════════════════════════════════════════
    //  VIEW: pending message count
    // ═══════════════════════════════════════════

    function test_PendingMessageCount_Zero() public view {
        assertEq(hub.pendingMessageCount(CHAIN_ID), 0);
    }

    function test_PendingMessageCount_AfterSend() public {
        _sendOneMessage();
        _sendOneMessage();
        assertEq(hub.pendingMessageCount(CHAIN_ID), 2);
    }

    function test_PendingMessageCount_AfterAcknowledge() public {
        _sendOneMessage();
        _sendOneMessage();
        _sendOneMessage();

        vm.prank(sequencer);
        hub.acknowledgeMessages(CHAIN_ID, 2, keccak256("root"));

        assertEq(hub.pendingMessageCount(CHAIN_ID), 1);
    }

    function test_PendingMessageCount_UnregisteredChain() public view {
        assertEq(hub.pendingMessageCount(999), 0);
    }

    // ═══════════════════════════════════════════
    //  ATTACK VECTOR: registration excess fee refund
    // ═══════════════════════════════════════════

    function test_Register_RefundsExcessFee() public {
        uint64 newChain = 300;
        bytes memory sig = _signRegistration(newChain, hubOwner, sequencerPk);

        uint256 balBefore = hubOwner.balance;
        hub.registerAppChain{value: 1 ether}(newChain, sequencer, sig);
        uint256 spent = balBefore - hubOwner.balance;

        assertEq(spent, REG_FEE, "Should only charge registration fee, refund excess");
    }

    // ═══════════════════════════════════════════
    //  ATTACK VECTOR: anchor processedUpToMessageId=0 bypass
    // ═══════════════════════════════════════════

    function test_Anchor_ProcessedZero_DoesNotUpdateState() public {
        _sendOneMessage();
        vm.prank(sequencer);
        hub.acknowledgeMessages(CHAIN_ID, 1, keccak256("root"));

        vm.prank(sequencer);
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");

        (, , , uint64 lastProcessed, , ) = hub.getAppChainInfo(CHAIN_ID);
        assertEq(lastProcessed, 1, "processedUpToMessageId=0 must not reset lastProcessedMessageId");
    }

    // ═══════════════════════════════════════════
    //  ATTACK VECTOR: rogue sequencer without verifier
    // ═══════════════════════════════════════════

    function test_AnchorProof_NoVerifier_AcceptsAnyProof() public {
        vm.prank(sequencer);
        hub.anchor(CHAIN_ID, 10, keccak256("fake"), keccak256("fake"), keccak256("fake"), 0, "");
        assertTrue(hub.verifyAnchorProof(CHAIN_ID, 10, "anything"));
    }

    function test_AnchorProof_WithVerifier_Validates() public {
        MockHubVerifier v = new MockHubVerifier();
        hub.setVerifier(CHAIN_ID, address(v));

        vm.prank(sequencer);
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");

        assertTrue(hub.verifyAnchorProof(CHAIN_ID, 10, "valid"));
        assertFalse(hub.verifyAnchorProof(CHAIN_ID, 10, "bad"));
    }

    // ═══════════════════════════════════════════
    //  ATTACK VECTOR: sequencer key compromise
    //  (verify owner can rotate sequencer)
    // ═══════════════════════════════════════════

    function test_SequencerRotation_OldSequencerLosesAccess() public {
        address newSeq = address(0xBEEF);
        hub.setSequencer(CHAIN_ID, newSeq);

        vm.prank(sequencer);
        vm.expectRevert("Only sequencer");
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");

        vm.prank(newSeq);
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");
    }

    // ═══════════════════════════════════════════
    //  ATTACK VECTOR: authorized sender revocation
    // ═══════════════════════════════════════════

    // ═══════════════════════════════════════════
    //  ATTACK VECTOR: zero state root anchor
    // ═══════════════════════════════════════════

    function test_Anchor_RevertZeroStateRoot() public {
        vm.prank(sequencer);
        vm.expectRevert("Invalid state root");
        hub.anchor(CHAIN_ID, 10, bytes32(0), keccak256("t"), keccak256("r"), 0, "");
    }

    // ═══════════════════════════════════════════
    //  ATTACK VECTOR: fee griefing (set to max)
    // ═══════════════════════════════════════════

    function test_SetRegistrationFee_RevertExceedsMax() public {
        vm.expectRevert("Fee exceeds maximum");
        hub.setRegistrationFee(101 ether);
    }

    function test_SetMessageFee_RevertExceedsMax() public {
        vm.expectRevert("Fee exceeds maximum");
        hub.setMessageFee(101 ether);
    }

    function test_SetRegistrationFee_AllowsReasonableValue() public {
        hub.setRegistrationFee(1 ether);
        assertEq(hub.registrationFee(), 1 ether);
    }

    // ═══════════════════════════════════════════
    //  ATTACK VECTOR: authorized sender revocation
    // ═══════════════════════════════════════════

    function test_RevokeAuthorizedSender_BlocksMessages() public {
        hub.setAuthorizedSender(CHAIN_ID, sender, false);

        vm.prank(sender);
        vm.expectRevert("Not authorized");
        hub.sendMessage{value: MSG_FEE}(1, CHAIN_ID, target, "blocked");
    }

    // ═══════════════════════════════════════════════════
    //  ATTACK: CHAIN ID SQUATTING
    //  Bad actor tries to grab many chain IDs cheaply
    // ═══════════════════════════════════════════════════

    function test_Squatting_CostsRegistrationFeePerChain() public {
        address squatter = address(0xBAD);
        vm.deal(squatter, 100 ether);
        uint256 balBefore = squatter.balance;

        for (uint64 i = 100; i < 110; i++) {
            bytes memory sig = _signRegistration(i, squatter, sequencerPk);
            vm.prank(squatter);
            hub.registerAppChain{value: REG_FEE}(i, sequencer, sig);
        }

        assertEq(balBefore - squatter.balance, REG_FEE * 10, "Must pay fee per chain");
    }

    function test_Squatting_CannotRegisterWithoutSequencerConsent() public {
        address squatter = address(0xBAD);
        vm.deal(squatter, 1 ether);
        uint256 fakePk = 0xDEADBEEF;
        address fakeSeq = vm.addr(fakePk);

        bytes memory sig = _signRegistration(500, squatter, fakePk);

        vm.prank(squatter);
        hub.registerAppChain{value: REG_FEE}(500, fakeSeq, sig);
        (, address seq, , , , bool reg) = hub.getAppChainInfo(500);
        assertTrue(reg);
        assertEq(seq, fakeSeq);

        vm.prank(squatter);
        vm.expectRevert("Invalid sequencer signature");
        hub.registerAppChain{value: REG_FEE}(501, sequencer, sig);
    }

    // ═══════════════════════════════════════════════════
    //  ATTACK: MESSAGE SPAM / GRIEF OTHER CHAIN
    // ═══════════════════════════════════════════════════

    function test_MessageSpam_CostsFeePerMessage() public {
        uint256 balBefore = sender.balance;

        for (uint256 i = 0; i < 10; i++) {
            vm.prank(sender);
            hub.sendMessage{value: MSG_FEE}(1, CHAIN_ID, target, "spam");
        }

        assertEq(balBefore - sender.balance, MSG_FEE * 10, "Must pay per message");
    }

    function test_MessageSpam_UnauthorizedCannotFlood() public {
        address spammer = address(0x5BA3);
        vm.deal(address(spammer), 10 ether);

        vm.prank(address(spammer));
        vm.expectRevert("Not authorized");
        hub.sendMessage{value: MSG_FEE}(1, CHAIN_ID, target, "flood");
    }

    // ═══════════════════════════════════════════════════
    //  ATTACK: IMPERSONATE ANOTHER CHAIN'S OWNER
    // ═══════════════════════════════════════════════════

    function test_Impersonation_CannotManageOtherChain() public {
        uint64 victimChain = 99;
        address victim = address(0x41C0);
        vm.deal(victim, 1 ether);

        bytes memory sig = _signRegistration(victimChain, victim, sequencerPk);
        vm.prank(victim);
        hub.registerAppChain{value: REG_FEE}(victimChain, sequencer, sig);

        vm.prank(hubOwner);
        vm.expectRevert("Only AppChain owner");
        hub.setSequencer(victimChain, address(0xAC1D));

        vm.prank(hubOwner);
        vm.expectRevert("Only AppChain owner");
        hub.setAuthorizedSender(victimChain, address(0xAC1D), true);

        vm.prank(hubOwner);
        vm.expectRevert("Only AppChain owner");
        hub.transferAppChainOwnership(victimChain, hubOwner);
    }

    // ═══════════════════════════════════════════════════
    //  ATTACK: STEAL FEES FROM ANOTHER CHAIN
    // ═══════════════════════════════════════════════════

    function test_FeeTheft_CannotWithdrawOtherChainFees() public {
        _sendOneMessage();
        uint256 fees = hub.ownerBalances(CHAIN_ID);
        assertTrue(fees > 0);

        address thief = address(0x71EF);
        vm.prank(thief);
        vm.expectRevert("Only AppChain owner");
        hub.withdrawFees(CHAIN_ID);
    }

    // ═══════════════════════════════════════════════════
    //  ATTACK: FRONT-RUN CHAIN REGISTRATION
    //  Attacker sees pending registration and tries to steal chain ID
    // ═══════════════════════════════════════════════════

    function test_FrontRun_RequiresSequencerSignatureForSpecificOwner() public {
        uint64 targetChain = 777;
        address legitimateOwner = address(0x1E617);
        address frontRunner = address(0xF407);
        vm.deal(legitimateOwner, 1 ether);
        vm.deal(frontRunner, 1 ether);

        bytes memory sig = _signRegistration(targetChain, legitimateOwner, sequencerPk);

        vm.prank(frontRunner);
        vm.expectRevert("Invalid sequencer signature");
        hub.registerAppChain{value: REG_FEE}(targetChain, sequencer, sig);

        vm.prank(legitimateOwner);
        hub.registerAppChain{value: REG_FEE}(targetChain, sequencer, sig);
        (address owner, , , , , ) = hub.getAppChainInfo(targetChain);
        assertEq(owner, legitimateOwner);
    }

    // ═══════════════════════════════════════════════════
    //  ATTACK: NO DEREGISTRATION — PERMANENT SQUATTING
    //  Once registered, chain ID is taken forever
    // ═══════════════════════════════════════════════════

    function test_NoDeregistration_ChainIdPermanent() public {
        (, , , , , bool reg) = hub.getAppChainInfo(CHAIN_ID);
        assertTrue(reg, "Chain must stay registered - no deregistration function exists");
    }

    // ═══════════════════════════════════════════════════
    //  FUNCTIONAL: FULL E2E MESSAGING LIFECYCLE
    //  Register → Authorize → Send → Anchor → Acknowledge → Verify
    // ═══════════════════════════════════════════════════

    function test_E2E_FullMessagingLifecycle() public {
        uint64 chainA = 100;
        uint64 chainB = CHAIN_ID;
        address bridgeOperator = address(0xB41D6E);
        vm.deal(bridgeOperator, 10 ether);

        bytes memory sigA = _signRegistration(chainA, hubOwner, sequencerPk);
        hub.registerAppChain{value: REG_FEE}(chainA, sequencer, sigA);
        hub.setAuthorizedSender(chainB, bridgeOperator, true);

        vm.prank(bridgeOperator);
        hub.sendMessage{value: MSG_FEE}(chainA, chainB, target, "cross-chain payload");

        (uint64 src, address msgSender, , address msgTarget, bytes memory data, ) = hub.getMessage(chainB, 1);
        assertEq(src, chainA);
        assertEq(msgSender, bridgeOperator);
        assertEq(msgTarget, target);
        assertEq(data, "cross-chain payload");

        bytes32 msgRoot = keccak256("merkle-root-of-processed-messages");
        vm.prank(sequencer);
        hub.acknowledgeMessages(chainB, 1, msgRoot);

        (uint64 ackId, bytes32 root) = hub.getMessageRootCheckpoint(chainB);
        assertEq(ackId, 1);
        assertEq(root, msgRoot);

        vm.prank(sequencer);
        hub.anchor(chainB, 100, keccak256("state"), keccak256("tx"), keccak256("rx"), 1, "");

        (, , , uint64 lastProcessed, , ) = hub.getAppChainInfo(chainB);
        assertEq(lastProcessed, 1);

        assertEq(hub.pendingMessageCount(chainB), 0);
    }

    // ═══════════════════════════════════════════════════
    //  FUNCTIONAL: MULTI-MESSAGE ORDERING
    // ═══════════════════════════════════════════════════

    function test_E2E_MessageOrdering() public {
        for (uint256 i = 0; i < 5; i++) {
            vm.prank(sender);
            hub.sendMessage{value: MSG_FEE}(1, CHAIN_ID, target, abi.encodePacked("msg", i));
        }

        for (uint64 i = 1; i <= 5; i++) {
            (uint64 src, , , , bytes memory data, ) = hub.getMessage(CHAIN_ID, i);
            assertEq(src, 1);
            assertEq(data, abi.encodePacked("msg", uint256(i - 1)));
        }

        assertEq(hub.pendingMessageCount(CHAIN_ID), 5);

        vm.prank(sequencer);
        hub.acknowledgeMessages(CHAIN_ID, 3, keccak256("root-batch1"));
        assertEq(hub.pendingMessageCount(CHAIN_ID), 2);

        vm.prank(sequencer);
        hub.acknowledgeMessages(CHAIN_ID, 5, keccak256("root-batch2"));
        assertEq(hub.pendingMessageCount(CHAIN_ID), 0);
    }

    // ═══════════════════════════════════════════════════
    //  FUNCTIONAL: FEE DISTRIBUTION ACROSS MULTIPLE CHAINS
    // ═══════════════════════════════════════════════════

    function test_E2E_FeeDistribution() public {
        uint64 chainX = 200;
        uint64 chainY = 201;
        address ownerX = address(0xAAA);
        address ownerY = address(0xBBB);
        vm.deal(ownerX, 10 ether);
        vm.deal(ownerY, 10 ether);
        vm.deal(sender, 100 ether);

        bytes memory sigX = _signRegistration(chainX, ownerX, sequencerPk);
        vm.prank(ownerX);
        hub.registerAppChain{value: REG_FEE}(chainX, sequencer, sigX);
        vm.prank(ownerX);
        hub.setAuthorizedSender(chainX, sender, true);

        bytes memory sigY = _signRegistration(chainY, ownerY, sequencerPk);
        vm.prank(ownerY);
        hub.registerAppChain{value: REG_FEE}(chainY, sequencer, sigY);
        vm.prank(ownerY);
        hub.setAuthorizedSender(chainY, sender, true);

        vm.prank(sender);
        hub.sendMessage{value: MSG_FEE}(1, chainX, target, "toX");
        vm.prank(sender);
        hub.sendMessage{value: MSG_FEE}(1, chainX, target, "toX2");
        vm.prank(sender);
        hub.sendMessage{value: MSG_FEE}(1, chainY, target, "toY");

        uint256 expectedOwnerCut = MSG_FEE - (MSG_FEE * HUB_BPS / 10000);
        assertEq(hub.ownerBalances(chainX), expectedOwnerCut * 2, "Chain X gets 2 message fees");
        assertEq(hub.ownerBalances(chainY), expectedOwnerCut * 1, "Chain Y gets 1 message fee");

        uint256 totalHubCut = (MSG_FEE * HUB_BPS / 10000) * 3 + REG_FEE * 3;
        assertEq(hub.hubBalance(), totalHubCut, "Hub gets cut from all messages + all registrations");

        vm.prank(ownerX);
        hub.withdrawFees(chainX);
        assertEq(hub.ownerBalances(chainX), 0);
        assertEq(ownerX.balance, 10 ether - REG_FEE + expectedOwnerCut * 2);
    }

    // ═══════════════════════════════════════════════════
    //  FUNCTIONAL: ANCHOR VERIFICATION
    // ═══════════════════════════════════════════════════

    function test_E2E_AnchorVerification() public {
        bytes32 sr = keccak256("state");
        bytes32 tr = keccak256("tx");
        bytes32 rr = keccak256("receipt");

        vm.prank(sequencer);
        hub.anchor(CHAIN_ID, 10, sr, tr, rr, 0, "");

        assertTrue(hub.verifyAnchor(CHAIN_ID, 10, sr, tr, rr));
        assertFalse(hub.verifyAnchor(CHAIN_ID, 10, keccak256("wrong"), tr, rr));
        assertFalse(hub.verifyAnchor(CHAIN_ID, 10, sr, keccak256("wrong"), rr));
        assertFalse(hub.verifyAnchor(CHAIN_ID, 10, sr, tr, keccak256("wrong")));
        assertFalse(hub.verifyAnchor(CHAIN_ID, 99, sr, tr, rr));
    }

    // ═══════════════════════════════════════════════════
    //  PLUGGABLE AUTHORITY: replaces single sequencer
    // ═══════════════════════════════════════════════════

    function test_Authority_OverridesSequencer() public {
        RotatingAuthority auth = new RotatingAuthority();
        auth.addValidator(CHAIN_ID, address(0xA1));
        auth.addValidator(CHAIN_ID, address(0xA2));

        hub.setAuthority(CHAIN_ID, IAuthority(address(auth)));

        vm.prank(sequencer);
        vm.expectRevert("Not authorized by authority");
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");

        vm.prank(address(0xA1));
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");
    }

    function test_Authority_ValidatorRotation() public {
        RotatingAuthority auth = new RotatingAuthority();
        auth.addValidator(CHAIN_ID, address(0xA1));
        hub.setAuthority(CHAIN_ID, IAuthority(address(auth)));

        vm.prank(address(0xA1));
        hub.anchor(CHAIN_ID, 10, keccak256("s1"), keccak256("t1"), keccak256("r1"), 0, "");

        auth.removeValidator(CHAIN_ID, address(0xA1));
        auth.addValidator(CHAIN_ID, address(0xA2));

        vm.prank(address(0xA1));
        vm.expectRevert("Not authorized by authority");
        hub.anchor(CHAIN_ID, 20, keccak256("s2"), keccak256("t2"), keccak256("r2"), 0, "");

        vm.prank(address(0xA2));
        hub.anchor(CHAIN_ID, 20, keccak256("s2"), keccak256("t2"), keccak256("r2"), 0, "");
    }

    function test_Authority_AcknowledgeAlsoUsesAuthority() public {
        RotatingAuthority auth = new RotatingAuthority();
        auth.addValidator(CHAIN_ID, address(0xA1));
        hub.setAuthority(CHAIN_ID, IAuthority(address(auth)));

        _sendOneMessage();

        vm.prank(sequencer);
        vm.expectRevert("Not authorized by authority");
        hub.acknowledgeMessages(CHAIN_ID, 1, keccak256("root"));

        vm.prank(address(0xA1));
        hub.acknowledgeMessages(CHAIN_ID, 1, keccak256("root"));
    }

    function test_Authority_ClearAuthority_FallsBackToSequencer() public {
        RotatingAuthority auth = new RotatingAuthority();
        auth.addValidator(CHAIN_ID, address(0xA1));
        hub.setAuthority(CHAIN_ID, IAuthority(address(auth)));

        vm.prank(sequencer);
        vm.expectRevert("Not authorized by authority");
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");

        hub.setAuthority(CHAIN_ID, IAuthority(address(0)));

        vm.prank(sequencer);
        hub.anchor(CHAIN_ID, 10, keccak256("s"), keccak256("t"), keccak256("r"), 0, "");
    }

    function test_Authority_OnlyOwnerCanSet() public {
        RotatingAuthority auth = new RotatingAuthority();
        vm.prank(sender);
        vm.expectRevert("Only AppChain owner");
        hub.setAuthority(CHAIN_ID, IAuthority(address(auth)));
    }
}

contract RotatingAuthority is IAuthority {
    mapping(uint64 => mapping(address => bool)) public validators;

    function addValidator(uint64 chainId, address v) external {
        validators[chainId][v] = true;
    }

    function removeValidator(uint64 chainId, address v) external {
        validators[chainId][v] = false;
    }

    function canSubmitAnchor(uint64 chainId, address caller) external view returns (bool) {
        return validators[chainId][caller];
    }

    function canProve(uint64 chainId, address caller) external view returns (bool) {
        return validators[chainId][caller];
    }

    function canManageChain(uint64, address) external pure returns (bool) {
        return false;
    }
}

contract ExpensiveReceiveWallet {
    uint256 public dummy;
    receive() external payable {
        for (uint256 i = 0; i < 10; i++) {
            dummy += 1;
        }
    }
}

contract MockHubVerifier is IProofVerifier {
    function verify(bytes32, bytes calldata proof) external pure returns (bool) {
        return keccak256(proof) == keccak256("valid");
    }
}

contract ReentrantWithdrawer {
    AppChainHub hub;
    uint64 chainId;
    bool attacked;

    constructor(AppChainHub _hub, uint64 _chainId) {
        hub = _hub;
        chainId = _chainId;
    }

    receive() external payable {
        if (!attacked) {
            attacked = true;
            hub.withdrawFees(chainId);
        }
    }
}
