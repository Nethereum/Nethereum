// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

contract AppChainPolicy {
    uint256 public immutable appChainId;

    // === MEMBERSHIP ROOTS (Gas Efficient - no lists stored) ===
    bytes32 public writersRoot;
    bytes32 public adminsRoot;
    bytes32 public blacklistRoot;
    uint256 public epoch;

    // === POLICY CONFIG ===
    struct PolicyConfig {
        uint256 version;
        uint256 maxCalldataBytes;
        uint256 maxLogBytes;
        uint256 blockGasLimit;
        address sequencer;
    }

    PolicyConfig public currentPolicy;

    // === EVENTS ===
    event MemberInvited(
        address indexed inviter,
        address indexed invitee,
        bytes32 newRoot,
        uint256 epoch
    );

    event MemberBanned(
        address indexed bannedBy,
        address indexed banned,
        bytes32 newBlacklistRoot
    );

    event AdminAdded(
        address indexed addedBy,
        address indexed admin,
        bytes32 newAdminsRoot
    );

    event TreeRebuilt(
        uint256 indexed newEpoch,
        bytes32 newWritersRoot,
        bytes32 newAdminsRoot
    );

    event PolicyChanged(uint256 indexed version, PolicyConfig config);

    constructor(
        uint256 _appChainId,
        address _sequencer,
        bytes32 _initialWritersRoot,
        bytes32 _initialAdminsRoot
    ) {
        appChainId = _appChainId;
        writersRoot = _initialWritersRoot;
        adminsRoot = _initialAdminsRoot;

        currentPolicy = PolicyConfig({
            version: 1,
            maxCalldataBytes: 128000,
            maxLogBytes: 1000000,
            blockGasLimit: 30000000,
            sequencer: _sequencer
        });
    }

    // === MEMBERSHIP FUNCTIONS ===

    function invite(
        address invitee,
        bytes32 newWritersRoot,
        bytes32[] calldata proofCallerIsWriter
    ) external {
        require(_verify(msg.sender, writersRoot, proofCallerIsWriter), "Not a writer");
        require(!_isBlacklisted(invitee), "Invitee is blacklisted");

        writersRoot = newWritersRoot;
        emit MemberInvited(msg.sender, invitee, newWritersRoot, epoch);
    }

    function ban(
        address toBan,
        bytes32 newBlacklistRoot,
        bytes32[] calldata proofCallerIsAdmin
    ) external {
        require(_verify(msg.sender, adminsRoot, proofCallerIsAdmin), "Not an admin");

        blacklistRoot = newBlacklistRoot;
        emit MemberBanned(msg.sender, toBan, newBlacklistRoot);
    }

    function rebuildTrees(
        bytes32 newWritersRoot,
        bytes32 newAdminsRoot,
        bytes32[] calldata proofCallerIsAdmin
    ) external {
        require(_verify(msg.sender, adminsRoot, proofCallerIsAdmin), "Not an admin");

        epoch++;
        writersRoot = newWritersRoot;
        adminsRoot = newAdminsRoot;
        blacklistRoot = bytes32(0);

        emit TreeRebuilt(epoch, newWritersRoot, newAdminsRoot);
    }

    function updatePolicy(
        uint256 maxCalldataBytes,
        uint256 maxLogBytes,
        uint256 blockGasLimit,
        address sequencer,
        bytes32[] calldata proofCallerIsAdmin
    ) external {
        require(_verify(msg.sender, adminsRoot, proofCallerIsAdmin), "Not an admin");

        currentPolicy.version++;
        currentPolicy.maxCalldataBytes = maxCalldataBytes;
        currentPolicy.maxLogBytes = maxLogBytes;
        currentPolicy.blockGasLimit = blockGasLimit;
        currentPolicy.sequencer = sequencer;

        emit PolicyChanged(currentPolicy.version, currentPolicy);
    }

    // === VERIFICATION LOGIC ===
    function isValidWriter(
        address addr,
        bytes32[] calldata writerProof,
        bytes32[] calldata blacklistProof
    ) external view returns (bool) {
        bool isWriter = _verify(addr, writersRoot, writerProof);
        bool isBanned = blacklistRoot != bytes32(0) &&
            blacklistProof.length > 0 &&
            _verify(addr, blacklistRoot, blacklistProof);
        return isWriter && !isBanned;
    }

    function _verify(
        address addr,
        bytes32 root,
        bytes32[] calldata proof
    ) internal pure returns (bool) {
        if (root == bytes32(0)) return true;

        bytes32 leaf = keccak256(abi.encodePacked(addr));
        bytes32 computedHash = leaf;

        for (uint256 i = 0; i < proof.length; i++) {
            bytes32 proofElement = proof[i];
            if (computedHash <= proofElement) {
                computedHash = keccak256(abi.encodePacked(computedHash, proofElement));
            } else {
                computedHash = keccak256(abi.encodePacked(proofElement, computedHash));
            }
        }

        return computedHash == root;
    }

    function _isBlacklisted(address addr) internal view returns (bool) {
        // Simplified check - in production would verify against blacklistRoot
        return false;
    }
}
