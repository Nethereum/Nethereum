// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "forge-std/Test.sol";
import "../src/AppChainAnchor.sol";
import "../src/SimpleAuthority.sol";
import "../src/MockProofVerifier.sol";

library TestMerkle {
    function buildRoot(bytes32[] memory leaves) internal pure returns (bytes32) {
        uint256 n = leaves.length;
        require(n > 0);
        while (n > 1) {
            uint256 next = 0;
            for (uint256 i = 0; i < n; i += 2) {
                if (i + 1 < n) {
                    leaves[next] = _hashPair(leaves[i], leaves[i + 1]);
                } else {
                    leaves[next] = leaves[i];
                }
                next++;
            }
            n = next;
        }
        return leaves[0];
    }

    function getProof(bytes32[] memory leaves, uint256 index) internal pure returns (bytes32[] memory) {
        uint256 n = leaves.length;
        bytes32[] memory proof = new bytes32[](32);
        uint256 proofLen = 0;
        uint256 idx = index;
        bytes32[] memory layer = new bytes32[](n);
        for (uint256 i = 0; i < n; i++) layer[i] = leaves[i];

        while (n > 1) {
            uint256 pairIdx = idx % 2 == 0 ? idx + 1 : idx - 1;
            if (pairIdx < n) {
                proof[proofLen++] = layer[pairIdx];
            }
            uint256 next = 0;
            for (uint256 i = 0; i < n; i += 2) {
                if (i + 1 < n) {
                    layer[next] = _hashPair(layer[i], layer[i + 1]);
                } else {
                    layer[next] = layer[i];
                }
                next++;
            }
            n = next;
            idx = idx / 2;
        }
        bytes32[] memory trimmed = new bytes32[](proofLen);
        for (uint256 i = 0; i < proofLen; i++) trimmed[i] = proof[i];
        return trimmed;
    }

    function _hashPair(bytes32 a, bytes32 b) private pure returns (bytes32) {
        return a < b
            ? keccak256(abi.encodePacked(a, b))
            : keccak256(abi.encodePacked(b, a));
    }
}

contract AppChainAnchorTest is Test {
    using TestMerkle for bytes32[];

    event OwnershipTransferred(address indexed oldOwner, address indexed newOwner);

    AppChainAnchor anchor;
    SimpleAuthority authority;
    MockProofVerifier verifier;

    address owner;
    address operator = address(0x1);
    address challenger = address(0x2);

    bytes32 genesisHash = keccak256("test-chain");
    bytes32 genesisStateRoot = keccak256("genesis-state");
    uint64 constant TEST_CHAIN_ID = 31337;

    function setUp() public {
        owner = address(this);
        authority = new SimpleAuthority(owner);
        anchor = new AppChainAnchor();
        verifier = new MockProofVerifier();

        anchor.registerSchema(1, keccak256("blake3"), 1, 0);
        anchor.registerProofSystem(0, address(0), false);
        anchor.registerProofSystem(1, address(verifier), true);

        anchor.registerAppChain(
            TEST_CHAIN_ID, genesisHash, 1, genesisStateRoot, 0, 1, IAuthority(address(authority))
        );
        authority.setOperator(TEST_CHAIN_ID, operator);

        vm.deal(operator, 100 ether);
        vm.deal(challenger, 100 ether);
    }

    function _makeAnchor(uint64 start, uint64 end) internal view returns (AppChainAnchor.AggregatedAnchor memory) {
        (, bytes32 prevHash, , ) = anchor.getLatestAnchor(TEST_CHAIN_ID);
        return AppChainAnchor.AggregatedAnchor({
            chainId: TEST_CHAIN_ID,
            genesisHash: genesisHash,
            startBlock: start,
            endBlock: end,
            anchorVersion: 1,
            proofSystem: 0,
            endBlockHash: keccak256(abi.encode("block", end)),
            previousAnchorHash: prevHash,
            blockHashesRoot: keccak256(abi.encode("hashes", start, end)),
            postStateRoot: keccak256(abi.encode("state", end)),
            manifestHash: keccak256(abi.encode("manifest", start, end))
        });
    }

    function _makeProvenAnchor(uint64 start, uint64 end) internal view returns (AppChainAnchor.AggregatedAnchor memory) {
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(start, end);
        a.proofSystem = 1;
        return a;
    }

    function _buildMockProof(AppChainAnchor.AggregatedAnchor memory a) internal pure returns (bytes memory) {
        bytes memory proof = new bytes(256);
        bytes32 commitment = keccak256(abi.encode(a.postStateRoot, a.endBlockHash));
        assembly { mstore(add(proof, 32), commitment) }
        return proof;
    }

    function _submitAnchor(uint64 start, uint64 end) internal returns (AppChainAnchor.AggregatedAnchor memory) {
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(start, end);
        vm.prank(operator);
        anchor.submitAnchor(a, "");
        return a;
    }

    // ═══════════════════════════════════════════
    //  REGISTRATION
    // ═══════════════════════════════════════════

    function test_RegisterAppChain_Success() public view {
        (, , , , , , bool reg) = anchor.getAppChainConfig(TEST_CHAIN_ID);
        assertTrue(reg);
    }

    function test_RegisterAppChain_RevertNonOwner() public {
        vm.prank(operator);
        vm.expectRevert("Only owner");
        anchor.registerAppChain(99999, keccak256("new"), 1, genesisStateRoot, 0, 1, IAuthority(address(authority)));
    }

    function test_RegisterAppChain_RevertDuplicateChainId() public {
        vm.expectRevert("ChainId already registered");
        anchor.registerAppChain(TEST_CHAIN_ID, keccak256("different"), 1, genesisStateRoot, 0, 1, IAuthority(address(authority)));
    }

    function test_RegisterAppChain_RevertDuplicateGenesis() public {
        vm.expectRevert("Genesis already registered");
        anchor.registerAppChain(99998, genesisHash, 1, genesisStateRoot, 0, 1, IAuthority(address(authority)));
    }

    function test_RegisterAppChain_RevertZeroAuthority() public {
        vm.expectRevert("Invalid authority");
        anchor.registerAppChain(99997, keccak256("new2"), 1, genesisStateRoot, 0, 1, IAuthority(address(0)));
    }

    function test_RegisterSchema_SimplifiedToExistsFlag() public {
        anchor.registerSchema(2, bytes32(0), 0, 0);
        assertTrue(anchor.schemaExists(2));
    }

    // ═══════════════════════════════════════════
    //  ANCHOR SUBMISSION
    // ═══════════════════════════════════════════

    function test_SubmitAnchor_Success() public {
        _submitAnchor(1, 5);
        (uint64 end, , , ) = anchor.getLatestAnchor(TEST_CHAIN_ID);
        assertEq(end, 5);
    }

    function test_SubmitAnchor_RevertNonOperator() public {
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(1, 5);
        vm.prank(challenger);
        vm.expectRevert("Not authorized");
        anchor.submitAnchor(a, "");
    }

    function test_SubmitAnchor_RevertGap() public {
        _submitAnchor(1, 5);
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(7, 10);
        a.startBlock = 7;
        vm.prank(operator);
        vm.expectRevert("Gap in block range");
        anchor.submitAnchor(a, "");
    }

    function test_SubmitAnchor_RevertBrokenChain() public {
        _submitAnchor(1, 5);
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(6, 10);
        a.previousAnchorHash = bytes32(0);
        vm.prank(operator);
        vm.expectRevert("Broken anchor chain");
        anchor.submitAnchor(a, "");
    }

    function test_SubmitAnchor_RevertProofTooLarge() public {
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(1, 5);
        vm.prank(operator);
        vm.expectRevert("Proof too large");
        anchor.submitAnchor(a, new bytes(131073));
    }

    function test_SubmitAnchor_StoresBlockHashesRoot() public {
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(1, 5);
        vm.prank(operator);
        anchor.submitAnchor(a, "");
        assertEq(anchor.blockHashesRoots(TEST_CHAIN_ID, 5), a.blockHashesRoot);
    }

    function test_SubmitAnchor_ConditionalCommitment_ProofSystem0() public {
        _submitAnchor(1, 5);
        assertEq(anchor.anchorCommitments(TEST_CHAIN_ID, 5), bytes32(0));
    }

    function test_SubmitAnchor_ConditionalCommitment_ProofSystem1() public {
        anchor.raiseMinimumProofSystem(TEST_CHAIN_ID, 1);
        AppChainAnchor.AggregatedAnchor memory a = _makeProvenAnchor(1, 5);
        vm.prank(operator);
        anchor.submitAnchor(a, _buildMockProof(a));
        assertTrue(anchor.anchorCommitments(TEST_CHAIN_ID, 5) != bytes32(0));
    }

    function test_SubmitAnchor_ContiguousBatches() public {
        _submitAnchor(1, 5);
        _submitAnchor(6, 10);
        (uint64 end, , , ) = anchor.getLatestAnchor(TEST_CHAIN_ID);
        assertEq(end, 10);
    }

    // ═══════════════════════════════════════════
    //  PROOF VERIFICATION (batch level)
    // ═══════════════════════════════════════════

    function test_SubmitAnchor_WithProof_Success() public {
        anchor.raiseMinimumProofSystem(TEST_CHAIN_ID, 1);
        AppChainAnchor.AggregatedAnchor memory a = _makeProvenAnchor(1, 5);
        vm.prank(operator);
        anchor.submitAnchor(a, _buildMockProof(a));
        (uint64 end, , , ) = anchor.getLatestAnchor(TEST_CHAIN_ID);
        assertEq(end, 5);
    }

    function test_SubmitAnchor_WithProof_RevertEmpty() public {
        anchor.raiseMinimumProofSystem(TEST_CHAIN_ID, 1);
        AppChainAnchor.AggregatedAnchor memory a = _makeProvenAnchor(1, 5);
        vm.prank(operator);
        vm.expectRevert("Proof verification failed");
        anchor.submitAnchor(a, "");
    }

    function test_SubmitAnchor_WithProof_RevertZeroProof() public {
        anchor.raiseMinimumProofSystem(TEST_CHAIN_ID, 1);
        AppChainAnchor.AggregatedAnchor memory a = _makeProvenAnchor(1, 5);
        vm.prank(operator);
        vm.expectRevert("Proof verification failed");
        anchor.submitAnchor(a, new bytes(256));
    }

    // ═══════════════════════════════════════════
    //  BLOCK INCLUSION VERIFICATION
    // ═══════════════════════════════════════════

    function test_VerifyBlockInclusion_StoresRoot() public {
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(1, 5);
        vm.prank(operator);
        anchor.submitAnchor(a, "");
        assertEq(anchor.blockHashesRoots(TEST_CHAIN_ID, 5), a.blockHashesRoot);
        assertTrue(a.blockHashesRoot != bytes32(0));
    }

    function test_VerifyBlockInclusion_RevertNoAnchor() public {
        bytes32[] memory proof = new bytes32[](0);
        vm.expectRevert("No anchor at this block");
        anchor.verifyBlockInclusion(TEST_CHAIN_ID, 99, 1, bytes32(0), bytes32(0), bytes32(0), proof);
    }

    // ═══════════════════════════════════════════
    //  ACCESS CONTROL
    // ═══════════════════════════════════════════

    function test_TransferOperator_ViaAuthority() public {
        vm.prank(operator);
        authority.setOperator(TEST_CHAIN_ID, challenger);
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(1, 5);
        vm.prank(operator);
        vm.expectRevert("Not authorized");
        anchor.submitAnchor(a, "");
        vm.prank(challenger);
        anchor.submitAnchor(a, "");
    }

    function test_TransferOperator_RevertNonOperator() public {
        vm.prank(challenger);
        vm.expectRevert("Not authorized");
        authority.setOperator(TEST_CHAIN_ID, challenger);
    }

    function test_SetOperator_OwnerOverride() public {
        authority.setOperator(TEST_CHAIN_ID, challenger);
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(1, 5);
        vm.prank(challenger);
        anchor.submitAnchor(a, "");
    }

    function test_SetChainAuthority_Upgrade() public {
        SimpleAuthority newAuth = new SimpleAuthority(owner);
        newAuth.setOperator(TEST_CHAIN_ID, challenger);
        vm.prank(operator);
        anchor.setChainAuthority(TEST_CHAIN_ID, IAuthority(address(newAuth)));
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(1, 5);
        vm.prank(challenger);
        anchor.submitAnchor(a, "");
    }

    function test_TransferOwnership_EmitsEvent() public {
        vm.expectEmit(true, true, false, false);
        emit OwnershipTransferred(owner, challenger);
        anchor.transferOwnership(challenger);
    }

    // ═══════════════════════════════════════════
    //  GRADUATION
    // ═══════════════════════════════════════════

    function test_RaiseMinimumProofSystem_Success() public {
        _submitAnchor(1, 5);
        anchor.raiseMinimumProofSystem(TEST_CHAIN_ID, 1);
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(6, 10);
        vm.prank(operator);
        vm.expectRevert("Below minimum proof tier");
        anchor.submitAnchor(a, "");
    }

    function test_RaiseMinimumProofSystem_RevertLower() public {
        anchor.raiseMinimumProofSystem(TEST_CHAIN_ID, 1);
        vm.expectRevert("Can only raise");
        anchor.raiseMinimumProofSystem(TEST_CHAIN_ID, 0);
    }

    function test_RaiseMinimumProofSystem_RevertUnregistered() public {
        vm.expectRevert("Proof system not registered");
        anchor.raiseMinimumProofSystem(TEST_CHAIN_ID, 2);
    }

    function test_RaiseMinimumAnchorVersion_RevertUnregistered() public {
        vm.expectRevert("Schema not registered");
        anchor.raiseMinimumAnchorVersion(TEST_CHAIN_ID, 2);
    }

    // ═══════════════════════════════════════════
    //  PAUSE
    // ═══════════════════════════════════════════

    function test_Pause_BlocksSubmitAnchor() public {
        anchor.pause();
        AppChainAnchor.AggregatedAnchor memory a = _makeAnchor(1, 5);
        vm.prank(operator);
        vm.expectRevert(abi.encodeWithSignature("EnforcedPause()"));
        anchor.submitAnchor(a, "");
    }

    function test_Unpause_AllowsSubmitAnchor() public {
        anchor.pause();
        anchor.unpause();
        _submitAnchor(1, 5);
        (uint64 end, , , ) = anchor.getLatestAnchor(TEST_CHAIN_ID);
        assertEq(end, 5);
    }

    function test_Pause_RevertNonOwner() public {
        vm.prank(operator);
        vm.expectRevert("Only owner");
        anchor.pause();
    }

    // ═══════════════════════════════════════════
    //  E2E: RICH MERKLE TREE + BLOCK INCLUSION
    // ═══════════════════════════════════════════

    struct BlockData {
        bytes32 blockHash;
        bytes32 preState;
        bytes32 postState;
    }

    function _verifyBlock(uint64 endBlock, uint64 blockNum, BlockData memory b, bytes32[] memory leaves, uint256 idx) internal view returns (bool) {
        bytes32[] memory leavesCopy = new bytes32[](leaves.length);
        for (uint256 i = 0; i < leaves.length; i++) leavesCopy[i] = leaves[i];
        bytes32[] memory proof = leavesCopy.getProof(idx);
        return anchor.verifyBlockInclusion(TEST_CHAIN_ID, endBlock, blockNum, b.blockHash, b.preState, b.postState, proof);
    }

    function test_E2E_BuildTree_SubmitAnchor_VerifyBlockInclusion() public {
        uint64 endBlock = 5;
        bytes32 prevState = genesisStateRoot;

        bytes32[] memory leaves = new bytes32[](5);
        BlockData[] memory blocks = new BlockData[](5);

        for (uint64 i = 0; i < 5; i++) {
            uint64 blockNum = 1 + i;
            bytes32 bHash = keccak256(abi.encode("realblock", blockNum));
            bytes32 pState = keccak256(abi.encode("realstate", blockNum));

            blocks[i] = BlockData(bHash, prevState, pState);
            leaves[i] = keccak256(abi.encodePacked(blockNum, bHash, prevState, pState));
            prevState = pState;
        }

        bytes32[] memory leavesCopy = new bytes32[](5);
        for (uint256 i = 0; i < 5; i++) leavesCopy[i] = leaves[i];
        bytes32 merkleRoot = leavesCopy.buildRoot();

        (, bytes32 prevHash, , ) = anchor.getLatestAnchor(TEST_CHAIN_ID);
        vm.prank(operator);
        anchor.submitAnchor(AppChainAnchor.AggregatedAnchor({
            chainId: TEST_CHAIN_ID, genesisHash: genesisHash,
            startBlock: 1, endBlock: endBlock, anchorVersion: 1, proofSystem: 0,
            endBlockHash: blocks[4].blockHash, previousAnchorHash: prevHash,
            blockHashesRoot: merkleRoot, postStateRoot: blocks[4].postState,
            manifestHash: keccak256("manifest")
        }), "");

        assertEq(anchor.blockHashesRoots(TEST_CHAIN_ID, endBlock), merkleRoot);

        // Block 3 (index 2) — should verify
        assertTrue(_verifyBlock(endBlock, 3, blocks[2], leaves, 2), "Block 3 must verify");

        // Block 1 (index 0) — should verify
        assertTrue(_verifyBlock(endBlock, 1, blocks[0], leaves, 0), "Block 1 must verify");

        // Block 5 (index 4) — should verify
        assertTrue(_verifyBlock(endBlock, 5, blocks[4], leaves, 4), "Block 5 must verify");

        // Wrong postStateRoot — should NOT verify
        BlockData memory wrongState = BlockData(blocks[2].blockHash, blocks[2].preState, keccak256("wrong"));
        assertFalse(_verifyBlock(endBlock, 3, wrongState, leaves, 2), "Wrong state must NOT verify");

        // Wrong blockNumber — should NOT verify
        assertFalse(_verifyBlock(endBlock, 99, blocks[2], leaves, 2), "Wrong blockNum must NOT verify");
    }

    // ═══════════════════════════════════════════
    //  SECURITY: registerProofSystem validation
    // ═══════════════════════════════════════════

    function test_RegisterProofSystem_RevertZeroVerifierWhenProofRequired() public {
        vm.expectRevert("Verifier required when proof needed");
        anchor.registerProofSystem(5, address(0), true);
    }

    function test_RegisterProofSystem_AllowZeroVerifierWhenNoProof() public {
        anchor.registerProofSystem(6, address(0), false);
    }

    function test_RegisterProofSystem_CanUpdateVerifier() public {
        MockProofVerifier v2 = new MockProofVerifier();
        anchor.registerProofSystem(1, address(v2), true);

        (address newVerifier, , ) = anchor.proofSystems(1);
        assertEq(newVerifier, address(v2));
    }

    function test_RegisterProofSystem_CanDisableBySettingNoProof() public {
        anchor.registerProofSystem(1, address(0), false);

        (, bool requiresProof, ) = anchor.proofSystems(1);
        assertFalse(requiresProof);
    }
}
