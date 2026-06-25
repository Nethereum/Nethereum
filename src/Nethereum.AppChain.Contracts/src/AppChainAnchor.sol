// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import {IVerifier} from "./IVerifier.sol";
import {IAuthority} from "./IAuthority.sol";
import {MerkleProof} from "@openzeppelin/contracts/utils/cryptography/MerkleProof.sol";
import {Pausable} from "@openzeppelin/contracts/utils/Pausable.sol";

/// @title AppChainAnchor — L1 commitment registry for EVM AppChains
/// @notice Stores sequential batch anchors with per-block Merkle roots. Each chain references
/// an IAuthority for governance. Supports block inclusion verification via OpenZeppelin MerkleProof.
/// @dev Registry-level admin (owner) manages schemas, proof systems, and graduation.
/// Per-chain operations delegate to the chain's IAuthority.
contract AppChainAnchor is Pausable {

    /// @notice Proof system tiers — mirrors C# AnchoringOnChainProofSystem enum
    enum ProofSystem {
        NoProof,           // 0 — commitment only, no verification
        StarkHashOffChain, // 1 — STARK hash on-chain, proof in blobs, off-chain verification
        SnarkOnChain       // 2 — SNARK proof, on-chain IVerifier.verify()
    }

    /// @notice Batch anchor data submitted by the operator
    struct AggregatedAnchor {
        uint64  chainId;
        bytes32 genesisHash;
        uint64  startBlock;
        uint64  endBlock;
        uint8   anchorVersion;
        uint8   proofSystem;
        bytes32 endBlockHash;
        bytes32 previousAnchorHash;
        bytes32 blockHashesRoot;
        bytes32 postStateRoot;
        bytes32 manifestHash;
    }

    struct AnchorState {
        uint64  endBlock;
        bytes32 endBlockHash;
        bytes32 postStateRoot;
        bytes32 manifestHash;
    }

    struct AppChainConfig {
        uint64  chainId;
        bytes32 genesisHash;
        uint64  genesisBlock;
        bytes32 genesisStateRoot;
        uint8   minimumProofSystem;
        uint8   minimumAnchorVersion;
        IAuthority authority;
        bool    registered;
    }

    struct ProofSystemConfig {
        address verifier;
        bool    requiresProof;
        bool    exists;
    }

    uint256 public constant MAX_PROOF_SIZE = 131072;

    address public owner;

    mapping(uint64 => AppChainConfig)    public appChains;
    mapping(bytes32 => uint64)           public chainIdByGenesis;
    mapping(uint64 => AnchorState)       public latestAnchor;
    mapping(uint8 => bool)               public schemaExists;
    mapping(uint8 => ProofSystemConfig)  public proofSystems;
    mapping(uint64 => mapping(uint64 => bytes32)) public anchorCommitments;
    mapping(uint64 => mapping(uint64 => bytes32)) public blockHashesRoots;

    event AppChainRegistered(
        uint64 indexed chainId, bytes32 indexed genesisHash,
        uint64 genesisBlock, bytes32 genesisStateRoot,
        uint8 minimumProofSystem, uint8 minimumAnchorVersion, address authority
    );
    event AnchorSubmitted(
        uint64 indexed chainId, uint64 indexed startBlock,
        uint64 indexed endBlock, AggregatedAnchor anchor
    );
    event ChainAuthorityChanged(uint64 indexed chainId, address indexed oldAuthority, address indexed newAuthority);
    event SchemaRegistered(uint8 indexed version, bytes32 hashFunction, uint8 trieType, uint8 stateModel);
    event ProofSystemRegistered(uint8 indexed proofSystem, address verifier, bool requiresProof);
    event MinimumProofSystemRaised(uint64 indexed chainId, uint8 oldFloor, uint8 newFloor);
    event MinimumAnchorVersionRaised(uint64 indexed chainId, uint8 oldFloor, uint8 newFloor);
    event OwnershipTransferred(address indexed oldOwner, address indexed newOwner);

    modifier onlyOwner() {
        require(msg.sender == owner, "Only owner");
        _;
    }

    constructor() { owner = msg.sender; }

    // ═══════════════════════════════════════════
    //  REGISTRATION (registry owner)
    // ═══════════════════════════════════════════

    /// @notice Register a new AppChain in the registry
    /// @param chainId The chain's EIP-155 identifier (primary key, must be unique)
    /// @param genesisHash Cryptographic identity of the chain's genesis (prevents re-registration)
    /// @param genesisBlock The first block number (must be > 0)
    /// @param genesisStateRoot State root at genesis
    /// @param minimumProofSystem Minimum proof system tier required for anchors
    /// @param minimumAnchorVersion Minimum anchor schema version required
    /// @param authority The IAuthority contract governing this chain's operations
    function registerAppChain(
        uint64 chainId, bytes32 genesisHash, uint64 genesisBlock,
        bytes32 genesisStateRoot, uint8 minimumProofSystem,
        uint8 minimumAnchorVersion, IAuthority authority
    ) external onlyOwner {
        require(chainId > 0, "Invalid chainId");
        require(genesisHash != bytes32(0), "Invalid genesis hash");
        require(genesisBlock > 0, "Genesis block must be > 0");
        require(address(authority) != address(0), "Invalid authority");
        require(!appChains[chainId].registered, "ChainId already registered");
        require(chainIdByGenesis[genesisHash] == 0, "Genesis already registered");

        appChains[chainId] = AppChainConfig({
            chainId: chainId, genesisHash: genesisHash,
            genesisBlock: genesisBlock, genesisStateRoot: genesisStateRoot,
            minimumProofSystem: minimumProofSystem,
            minimumAnchorVersion: minimumAnchorVersion,
            authority: authority, registered: true
        });
        chainIdByGenesis[genesisHash] = chainId;

        latestAnchor[chainId] = AnchorState({
            endBlock: genesisBlock - 1, endBlockHash: bytes32(0),
            postStateRoot: genesisStateRoot, manifestHash: bytes32(0)
        });

        emit AppChainRegistered(chainId, genesisHash, genesisBlock, genesisStateRoot,
            minimumProofSystem, minimumAnchorVersion, address(authority));
    }

    function registerSchema(uint8 version, bytes32 hashFunction, uint8 trieType, uint8 stateModel) external onlyOwner {
        require(!schemaExists[version], "Already registered");
        schemaExists[version] = true;
        emit SchemaRegistered(version, hashFunction, trieType, stateModel);
    }

    function registerProofSystem(uint8 proofSystem, address verifier, bool requiresProof) external onlyOwner {
        require(!requiresProof || verifier != address(0), "Verifier required when proof needed");
        proofSystems[proofSystem] = ProofSystemConfig(verifier, requiresProof, true);
        emit ProofSystemRegistered(proofSystem, verifier, requiresProof);
    }

    // ═══════════════════════════════════════════
    //  ANCHOR SUBMISSION (per-chain authority)
    // ═══════════════════════════════════════════

    /// @notice Submit a batch anchor for an AppChain
    /// @dev Validates contiguity, chain integrity, proof system requirements, and authority.
    /// Stores blockHashesRoot for Merkle verification and anchorCommitment for proof fulfillment.
    /// @param a The anchor data including block range, state roots, and Merkle root
    /// @param proof Optional ZK proof bytes (required if proof system demands it)
    function submitAnchor(AggregatedAnchor calldata a, bytes calldata proof) external whenNotPaused {
        require(proof.length <= MAX_PROOF_SIZE, "Proof too large");

        AppChainConfig storage cfg = appChains[a.chainId];
        require(cfg.registered, "Unknown chain");
        require(cfg.authority.canSubmitAnchor(a.chainId, msg.sender), "Not authorized");
        require(a.genesisHash == cfg.genesisHash, "Genesis mismatch");
        require(a.proofSystem >= cfg.minimumProofSystem, "Below minimum proof tier");
        require(a.anchorVersion >= cfg.minimumAnchorVersion, "Below minimum anchor version");
        require(schemaExists[a.anchorVersion], "Unknown anchor version");

        AnchorState storage prev = latestAnchor[a.chainId];
        require(a.startBlock == prev.endBlock + 1, "Gap in block range");
        require(a.previousAnchorHash == prev.endBlockHash, "Broken anchor chain");
        require(a.endBlock >= a.startBlock, "Empty range");

        ProofSystemConfig memory ps = proofSystems[a.proofSystem];
        require(ps.exists, "Unknown proof system");
        if (ps.requiresProof) {
            require(
                IVerifier(ps.verifier).verify(
                    proof, _buildBatchPublicInputs(a, prev.postStateRoot, prev.endBlockHash)
                ),
                "Proof verification failed"
            );
        }

        prev.endBlock = a.endBlock;
        prev.endBlockHash = a.endBlockHash;
        prev.postStateRoot = a.postStateRoot;
        prev.manifestHash = a.manifestHash;

        blockHashesRoots[a.chainId][a.endBlock] = a.blockHashesRoot;

        if (a.proofSystem > 0) {
            anchorCommitments[a.chainId][a.endBlock] = _anchorStateHash(a);
        }

        emit AnchorSubmitted(a.chainId, a.startBlock, a.endBlock, a);
    }

    function _buildBatchPublicInputs(
        AggregatedAnchor calldata a, bytes32 preStateRoot, bytes32 startBlockHash
    ) internal pure returns (uint256[] memory) {
        uint256[] memory inputs = new uint256[](11);
        inputs[0]  = uint256(a.chainId);
        inputs[1]  = uint256(a.anchorVersion);
        inputs[2]  = uint256(a.proofSystem);
        inputs[3]  = uint256(a.startBlock);
        inputs[4]  = uint256(a.endBlock);
        inputs[5]  = uint256(preStateRoot);
        inputs[6]  = uint256(a.postStateRoot);
        inputs[7]  = uint256(startBlockHash);
        inputs[8]  = uint256(a.endBlockHash);
        inputs[9]  = uint256(a.blockHashesRoot);
        inputs[10] = uint256(a.manifestHash);
        return inputs;
    }

    function _anchorStateHash(AggregatedAnchor calldata a) internal pure returns (bytes32) {
        return keccak256(abi.encode(
            a.chainId, a.genesisHash, a.startBlock, a.endBlock,
            a.endBlockHash, a.previousAnchorHash,
            a.postStateRoot, a.blockHashesRoot, a.manifestHash
        ));
    }

    // ═══════════════════════════════════════════
    //  QUERIES
    // ═══════════════════════════════════════════

    function getLatestAnchor(uint64 chainId) external view returns (
        uint64 endBlock, bytes32 endBlockHash, bytes32 postStateRoot, bytes32 manifestHash
    ) {
        AnchorState storage s = latestAnchor[chainId];
        return (s.endBlock, s.endBlockHash, s.postStateRoot, s.manifestHash);
    }

    function getAppChainConfig(uint64 chainId) external view returns (
        bytes32 genesisHash, uint64 genesisBlock, bytes32 genesisStateRoot,
        uint8 minimumProofSystem, uint8 minimumAnchorVersion, address authority, bool registered
    ) {
        AppChainConfig storage c = appChains[chainId];
        return (c.genesisHash, c.genesisBlock, c.genesisStateRoot,
                c.minimumProofSystem, c.minimumAnchorVersion, address(c.authority), c.registered);
    }

    function getChainAuthority(uint64 chainId) external view returns (IAuthority) {
        return appChains[chainId].authority;
    }

    function verifyStateRoot(uint64 chainId, bytes32 stateRoot) external view returns (bool) {
        return latestAnchor[chainId].postStateRoot == stateRoot;
    }

    /// @notice Verify a block's state transition was committed in an anchor's Merkle tree
    /// @dev Leaf = keccak256(abi.encodePacked(blockNumber, blockHash, preStateRoot, postStateRoot)).
    /// Uses OpenZeppelin MerkleProof with sorted pairs (compatible with Nethereum.Merkle).
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param anchorEndBlock The endBlock of the anchor batch containing this block
    /// @param blockNumber The specific block number to verify
    /// @param blockHash The block's header hash
    /// @param preStateRoot State root before this block's execution
    /// @param postStateRoot State root after this block's execution
    /// @param merkleProof Sibling hashes for the Merkle proof
    /// @return True if the block data was committed in the anchor's Merkle tree
    function verifyBlockInclusion(
        uint64 chainId, uint64 anchorEndBlock,
        uint64 blockNumber, bytes32 blockHash,
        bytes32 preStateRoot, bytes32 postStateRoot,
        bytes32[] calldata merkleProof
    ) external view returns (bool) {
        bytes32 root = blockHashesRoots[chainId][anchorEndBlock];
        require(root != bytes32(0), "No anchor at this block");
        bytes32 leaf = keccak256(abi.encodePacked(blockNumber, blockHash, preStateRoot, postStateRoot));
        return MerkleProof.verify(merkleProof, root, leaf);
    }

    // ═══════════════════════════════════════════
    //  PER-CHAIN AUTHORITY MANAGEMENT
    // ═══════════════════════════════════════════

    /// @notice Upgrade a chain's governance authority
    /// @dev Requires approval from the current authority (canManageChain) or the registry owner.
    /// This is the upgrade mechanism: SimpleAuthority → MultisigAuthority → ValidatorAuthority.
    /// @param chainId The AppChain's EIP-155 chain ID
    /// @param newAuthority The new IAuthority contract to govern this chain
    function setChainAuthority(uint64 chainId, IAuthority newAuthority) external {
        AppChainConfig storage cfg = appChains[chainId];
        require(cfg.registered, "Unknown chain");
        require(address(newAuthority) != address(0), "Invalid authority");
        require(
            cfg.authority.canManageChain(chainId, msg.sender) || msg.sender == owner,
            "Not authorized"
        );
        address old = address(cfg.authority);
        cfg.authority = newAuthority;
        emit ChainAuthorityChanged(chainId, old, address(newAuthority));
    }

    // ═══════════════════════════════════════════
    //  REGISTRY ADMIN (global — owner only)
    // ═══════════════════════════════════════════

    function raiseMinimumProofSystem(uint64 chainId, uint8 newFloor) external onlyOwner {
        AppChainConfig storage cfg = appChains[chainId];
        require(cfg.registered, "Unknown chain");
        require(newFloor > cfg.minimumProofSystem, "Can only raise");
        require(proofSystems[newFloor].exists, "Proof system not registered");
        uint8 old = cfg.minimumProofSystem;
        cfg.minimumProofSystem = newFloor;
        emit MinimumProofSystemRaised(chainId, old, newFloor);
    }

    function raiseMinimumAnchorVersion(uint64 chainId, uint8 newFloor) external onlyOwner {
        AppChainConfig storage cfg = appChains[chainId];
        require(cfg.registered, "Unknown chain");
        require(newFloor > cfg.minimumAnchorVersion, "Can only raise");
        require(schemaExists[newFloor], "Schema not registered");
        uint8 old = cfg.minimumAnchorVersion;
        cfg.minimumAnchorVersion = newFloor;
        emit MinimumAnchorVersionRaised(chainId, old, newFloor);
    }

    function pause() external onlyOwner { _pause(); }
    function unpause() external onlyOwner { _unpause(); }

    function transferOwnership(address newOwner) external onlyOwner {
        require(newOwner != address(0), "Invalid owner");
        address old = owner;
        owner = newOwner;
        emit OwnershipTransferred(old, newOwner);
    }
}
