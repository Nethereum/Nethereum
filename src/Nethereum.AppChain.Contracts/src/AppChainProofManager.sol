// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IVerifier} from "./IVerifier.sol";
import {IAuthority} from "./IAuthority.sol";
import {AppChainAnchor} from "./AppChainAnchor.sol";
import {ReentrancyGuard} from "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import {Pausable} from "@openzeppelin/contracts/utils/Pausable.sol";

/// @title AppChainProofManager — Per-block ZK proof verification with bond marketplace
/// @notice Authorized provers submit per-block ZK proofs. Anyone can request a proof by posting
/// a bond. Provers earn the bond on fulfillment. Requesters reclaim on timeout.
/// @dev Queries chain authority from AppChainAnchor via IAuthority interface.
/// Uses pull-payment pattern for all ETH transfers (no reentrancy risk on bond returns).
contract AppChainProofManager is ReentrancyGuard, Pausable {

    /// @notice Record of a verified per-block proof
    struct BlockProofRecord {
        address prover;
        uint64  timestamp;
        uint8   proofSystem;
        bool    verified;
    }

    struct ProofRequest {
        address requester;
        uint256 bond;
        uint256 deadline;
        bool    fulfilled;
    }

    uint256 public constant MIN_PROOF_WINDOW = 1 hours;
    uint256 public constant MAX_PROOF_WINDOW = 30 days;
    uint256 public constant MAX_PROOF_BOND = 100 ether;

    AppChainAnchor public immutable anchor;
    address public owner;

    mapping(uint64 => uint256) public proofBond;
    mapping(uint64 => uint256) public proofWindow;
    mapping(uint64 => mapping(uint64 => BlockProofRecord)) public blockProofs;
    mapping(uint64 => mapping(uint64 => ProofRequest)) public proofRequests;
    mapping(address => uint256) public pendingWithdrawals;

    event ProofBondChanged(uint64 indexed chainId, uint256 oldBond, uint256 newBond);
    event ProofWindowChanged(uint64 indexed chainId, uint256 oldWindow, uint256 newWindow);
    event BlockProofSubmitted(uint64 indexed chainId, uint64 indexed blockNumber, address prover, uint8 proofSystem);
    event ProofRequested(uint64 indexed chainId, uint64 indexed blockNumber, address requester, uint256 bond, uint256 deadline);
    event ProofRequestFulfilled(uint64 indexed chainId, uint64 indexed blockNumber, address prover, address requester);
    event ProofRequestExpired(uint64 indexed chainId, uint64 indexed blockNumber, address requester, uint256 compensation);
    event BondCredited(address indexed recipient, uint256 amount);
    event BondWithdrawn(address indexed recipient, uint256 amount);

    modifier onlyOwner() {
        require(msg.sender == owner, "Only owner");
        _;
    }

    constructor(address anchorContract) {
        anchor = AppChainAnchor(anchorContract);
        owner = msg.sender;
    }

    function _getAuthority(uint64 chainId) internal view returns (IAuthority) {
        return anchor.getChainAuthority(chainId);
    }

    // ═══════════════════════════════════════════
    //  CONFIGURATION (chain authority)
    // ═══════════════════════════════════════════

    function setProofBond(uint64 chainId, uint256 newBond) external {
        require(_getAuthority(chainId).canManageChain(chainId, msg.sender), "Not authorized");
        require(newBond <= MAX_PROOF_BOND, "Bond too high");
        uint256 oldBond = proofBond[chainId];
        proofBond[chainId] = newBond;
        emit ProofBondChanged(chainId, oldBond, newBond);
    }

    function setProofWindow(uint64 chainId, uint256 newWindow) external {
        require(_getAuthority(chainId).canManageChain(chainId, msg.sender), "Not authorized");
        require(newWindow >= MIN_PROOF_WINDOW, "Window too short");
        require(newWindow <= MAX_PROOF_WINDOW, "Window too long");
        uint256 oldWindow = proofWindow[chainId];
        proofWindow[chainId] = newWindow;
        emit ProofWindowChanged(chainId, oldWindow, newWindow);
    }

    // ═══════════════════════════════════════════
    //  DIRECT PROOF SUBMISSION (authorized prover)
    // ═══════════════════════════════════════════

    /// @notice Submit a per-block ZK proof (direct, no bond)
    /// @dev Verifies block inclusion via Merkle proof, then verifies ZK proof against committed state roots.
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param blockNumber The block being proven
    /// @param anchorEndBlock The endBlock of the anchor batch containing this block
    /// @param blockHash The block's header hash (must match Merkle commitment)
    /// @param preStateRoot State root before execution (must match Merkle commitment)
    /// @param postStateRoot State root after execution (must match Merkle commitment)
    /// @param merkleProof Sibling hashes proving block inclusion in the anchor's tree
    /// @param zkProof The ZK proof bytes
    /// @param proofSystem Which registered proof system to use for verification
    function submitBlockProof(
        uint64 chainId, uint64 blockNumber, uint64 anchorEndBlock,
        bytes32 blockHash, bytes32 preStateRoot, bytes32 postStateRoot,
        bytes32[] calldata merkleProof, bytes calldata zkProof, uint8 proofSystem
    ) external whenNotPaused {
        require(_getAuthority(chainId).canProve(chainId, msg.sender), "Not authorized");
        _verifyAndStoreProof(chainId, blockNumber, anchorEndBlock,
            blockHash, preStateRoot, postStateRoot, merkleProof, zkProof, proofSystem);
    }

    // ═══════════════════════════════════════════
    //  REQUESTED PROOF (with bond)
    // ═══════════════════════════════════════════

    /// @notice Request a per-block proof by posting a bond. Any authorized prover can fulfill.
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param blockNumber The block to prove
    function requestBlockProof(uint64 chainId, uint64 blockNumber) external payable nonReentrant whenNotPaused {
        uint256 bond = proofBond[chainId];
        require(bond > 0, "Proof bond not configured");
        require(msg.value == bond, "Send exact bond");

        (, , , , , , bool registered) = anchor.getAppChainConfig(chainId);
        require(registered, "Unknown chain");
        require(!blockProofs[chainId][blockNumber].verified, "Already proven");

        ProofRequest storage existing = proofRequests[chainId][blockNumber];
        require(existing.requester == address(0) || existing.fulfilled, "Request already pending");

        uint256 window = proofWindow[chainId];
        if (window == 0) window = 24 hours;

        proofRequests[chainId][blockNumber] = ProofRequest({
            requester: msg.sender, bond: msg.value,
            deadline: block.timestamp + window, fulfilled: false
        });

        emit ProofRequested(chainId, blockNumber, msg.sender, msg.value, block.timestamp + window);
    }

    /// @notice Fulfill a proof request — submit proof and claim the bond
    function fulfillBlockProof(
        uint64 chainId, uint64 blockNumber, uint64 anchorEndBlock,
        bytes32 blockHash, bytes32 preStateRoot, bytes32 postStateRoot,
        bytes32[] calldata merkleProof, bytes calldata zkProof, uint8 proofSystem
    ) external nonReentrant whenNotPaused {
        require(_getAuthority(chainId).canProve(chainId, msg.sender), "Not authorized");
        _verifyAndStoreProof(chainId, blockNumber, anchorEndBlock,
            blockHash, preStateRoot, postStateRoot, merkleProof, zkProof, proofSystem);
        _creditBondToProver(chainId, blockNumber);
    }

    function _creditBondToProver(uint64 chainId, uint64 blockNumber) internal {
        ProofRequest storage req = proofRequests[chainId][blockNumber];
        if (req.requester != address(0) && !req.fulfilled) {
            req.fulfilled = true;
            uint256 bondToReturn = req.bond;
            req.bond = 0;
            pendingWithdrawals[msg.sender] += bondToReturn;
            emit BondCredited(msg.sender, bondToReturn);
            emit ProofRequestFulfilled(chainId, blockNumber, msg.sender, req.requester);
        }
    }

    /// @notice Claim bond back when proof was not fulfilled within the window
    function claimProofTimeout(uint64 chainId, uint64 blockNumber) external nonReentrant {
        ProofRequest storage req = proofRequests[chainId][blockNumber];
        require(req.requester != address(0), "No request");
        require(!req.fulfilled, "Already fulfilled");
        require(block.timestamp > req.deadline, "Window still open");

        uint256 compensation = req.bond;
        address requester = req.requester;
        req.fulfilled = true;
        req.bond = 0;

        pendingWithdrawals[requester] += compensation;
        emit BondCredited(requester, compensation);
        emit ProofRequestExpired(chainId, blockNumber, requester, compensation);
    }

    // ═══════════════════════════════════════════
    //  PULL PAYMENT
    // ═══════════════════════════════════════════

    /// @notice Withdraw accumulated bond earnings or refunds (pull-payment pattern)
    function withdrawBond() external nonReentrant {
        uint256 amount = pendingWithdrawals[msg.sender];
        require(amount > 0, "Nothing to withdraw");
        pendingWithdrawals[msg.sender] = 0;
        (bool ok, ) = payable(msg.sender).call{value: amount}("");
        require(ok, "Transfer failed");
        emit BondWithdrawn(msg.sender, amount);
    }

    // ═══════════════════════════════════════════
    //  QUERIES
    // ═══════════════════════════════════════════

    function isBlockProven(uint64 chainId, uint64 blockNumber) external view returns (bool) {
        return blockProofs[chainId][blockNumber].verified;
    }

    function getBlockProof(uint64 chainId, uint64 blockNumber) external view returns (
        address prover, uint64 timestamp, uint8 proofSystem, bool verified
    ) {
        BlockProofRecord storage r = blockProofs[chainId][blockNumber];
        return (r.prover, r.timestamp, r.proofSystem, r.verified);
    }

    // ═══════════════════════════════════════════
    //  ADMIN
    // ═══════════════════════════════════════════

    function pause() external onlyOwner { _pause(); }
    function unpause() external onlyOwner { _unpause(); }

    // ═══════════════════════════════════════════
    //  INTERNAL
    // ═══════════════════════════════════════════

    function _verifyAndStoreProof(
        uint64 chainId, uint64 blockNumber, uint64 anchorEndBlock,
        bytes32 blockHash, bytes32 preStateRoot, bytes32 postStateRoot,
        bytes32[] calldata merkleProof, bytes calldata zkProof, uint8 proofSystem
    ) internal {
        require(!blockProofs[chainId][blockNumber].verified, "Already proven");

        require(
            anchor.verifyBlockInclusion(
                chainId, anchorEndBlock, blockNumber,
                blockHash, preStateRoot, postStateRoot, merkleProof
            ),
            "Block not in anchor"
        );

        _verifyZkProof(chainId, blockNumber, blockHash,
            preStateRoot, postStateRoot, zkProof, proofSystem);

        blockProofs[chainId][blockNumber] = BlockProofRecord({
            prover: msg.sender,
            timestamp: uint64(block.timestamp),
            proofSystem: proofSystem,
            verified: true
        });

        emit BlockProofSubmitted(chainId, blockNumber, msg.sender, proofSystem);
    }

    function _verifyZkProof(
        uint64 chainId, uint64 blockNumber, bytes32 blockHash,
        bytes32 preStateRoot, bytes32 postStateRoot,
        bytes calldata zkProof, uint8 proofSystem
    ) internal view {
        (address verifier, , bool exists) = anchor.proofSystems(proofSystem);
        require(exists, "Unknown proof system");

        (bytes32 genesisHash, , , uint8 minimumProofSystem, , , ) = anchor.getAppChainConfig(chainId);
        require(proofSystem >= minimumProofSystem, "Below minimum proof tier");

        uint256[] memory inputs = new uint256[](6);
        inputs[0] = uint256(genesisHash);
        inputs[1] = uint256(blockNumber);
        inputs[2] = uint256(blockHash);
        inputs[3] = uint256(preStateRoot);
        inputs[4] = uint256(postStateRoot);
        inputs[5] = uint256(proofSystem);

        require(IVerifier(verifier).verify(zkProof, inputs), "Proof verification failed");
    }
}
