// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

interface IProofVerifier {
    function verify(bytes32 root, bytes calldata proof) external view returns (bool);
}

contract AppChainHub {

    // ═══════════════════════════════════════════
    //  STRUCTS
    // ═══════════════════════════════════════════

    struct AppChainInfo {
        address owner;
        address sequencer;
        uint64 latestBlock;
        uint64 lastProcessedMessageId;
        uint64 nextMessageId;
        bool registered;
    }

    struct Anchor {
        bytes32 stateRoot;
        bytes32 txRoot;
        bytes32 receiptRoot;
        uint256 timestamp;
        bytes extraData; // future: ZK proofs, blob commitments, etc.
    }

    struct Message {
        uint64 sourceChainId;
        address sender;
        uint64 targetChainId;
        address target;
        bytes data;
        uint256 timestamp;
    }

    struct MessageRootCheckpoint {
        uint64 processedUpToMessageId;
        bytes32 merkleRoot;
    }

    // ═══════════════════════════════════════════
    //  STATE
    // ═══════════════════════════════════════════

    address public hubOwner;

    uint256 public registrationFee;
    uint256 public messageFee;
    uint256 public hubFeeBps; // basis points (e.g. 1000 = 10%)

    uint256 public hubBalance;
    mapping(uint64 => uint256) public ownerBalances;

    mapping(uint64 => AppChainInfo) public appChains;
    mapping(uint64 => mapping(uint64 => Anchor)) public anchors; // chainId => blockNumber => anchor
    mapping(uint64 => mapping(uint64 => Message)) public messages; // chainId => messageId => message
    mapping(uint64 => mapping(address => bool)) public authorizedSenders; // chainId => sender => authorized
    mapping(uint64 => address) public verifiers; // chainId => IProofVerifier (address(0) = default root matching)
    mapping(uint64 => MessageRootCheckpoint) public messageRootCheckpoints; // chainId => (lastProcessedMessageId, merkleRoot)

    uint256 public constant MAX_MESSAGE_SIZE = 10240; // 10 KB
    uint64 public constant MAX_CHAIN_ID = type(uint64).max;

    // ═══════════════════════════════════════════
    //  EVENTS
    // ═══════════════════════════════════════════

    event AppChainRegistered(
        uint64 indexed chainId,
        address indexed owner,
        address sequencer
    );

    event AppChainMetadataUpdated(
        uint64 indexed chainId,
        string name,
        string description,
        string url
    );

    event SequencerChanged(
        uint64 indexed chainId,
        address indexed oldSequencer,
        address indexed newSequencer
    );

    event AuthorizedSenderChanged(
        uint64 indexed chainId,
        address indexed sender,
        bool authorized
    );

    event Anchored(
        uint64 indexed chainId,
        uint64 indexed blockNumber,
        bytes32 stateRoot,
        bytes32 txRoot,
        bytes32 receiptRoot,
        uint64 processedUpToMessageId
    );

    event MessageSent(
        uint64 indexed targetChainId,
        uint64 indexed messageId,
        uint64 sourceChainId,
        address sender,
        address target,
        bytes data
    );

    event MessagesAcknowledged(
        uint64 indexed chainId,
        uint64 indexed processedUpToMessageId,
        bytes32 messagesRoot
    );

    event VerifierChanged(uint64 indexed chainId, address indexed oldVerifier, address indexed newVerifier);
    event RegistrationFeeChanged(uint256 oldFee, uint256 newFee);
    event MessageFeeChanged(uint256 oldFee, uint256 newFee);
    event HubFeeBpsChanged(uint256 oldBps, uint256 newBps);
    event HubOwnerChanged(address indexed oldOwner, address indexed newOwner);

    // ═══════════════════════════════════════════
    //  MODIFIERS
    // ═══════════════════════════════════════════

    modifier onlyHubOwner() {
        require(msg.sender == hubOwner, "Only hub owner");
        _;
    }

    modifier onlyAppChainOwner(uint64 chainId) {
        require(appChains[chainId].owner == msg.sender, "Only AppChain owner");
        _;
    }

    modifier onlySequencer(uint64 chainId) {
        require(appChains[chainId].sequencer == msg.sender, "Only sequencer");
        _;
    }

    // ═══════════════════════════════════════════
    //  CONSTRUCTOR
    // ═══════════════════════════════════════════

    constructor(uint256 _registrationFee, uint256 _messageFee, uint256 _hubFeeBps) {
        require(_hubFeeBps <= 10000, "Hub fee cannot exceed 100%");
        hubOwner = msg.sender;
        registrationFee = _registrationFee;
        messageFee = _messageFee;
        hubFeeBps = _hubFeeBps;
    }

    // ═══════════════════════════════════════════
    //  REGISTRATION
    // ═══════════════════════════════════════════

    function registerAppChain(
        uint64 chainId,
        address sequencer,
        bytes calldata sequencerSignature
    ) external payable {
        require(msg.value >= registrationFee, "Insufficient fee");
        require(!appChains[chainId].registered, "Already registered");
        require(sequencer != address(0), "Invalid sequencer");

        bytes32 hash = keccak256(abi.encodePacked(chainId, msg.sender));
        bytes32 ethHash = _toEthSignedMessageHash(hash);
        require(_recover(ethHash, sequencerSignature) == sequencer, "Invalid sequencer signature");

        appChains[chainId] = AppChainInfo({
            owner: msg.sender,
            sequencer: sequencer,
            latestBlock: 0,
            lastProcessedMessageId: 0,
            nextMessageId: 1,
            registered: true
        });

        hubBalance += msg.value;

        emit AppChainRegistered(chainId, msg.sender, sequencer);
    }

    function updateMetadata(
        uint64 chainId,
        string calldata name,
        string calldata description,
        string calldata url
    ) external onlyAppChainOwner(chainId) {
        emit AppChainMetadataUpdated(chainId, name, description, url);
    }

    function setSequencer(
        uint64 chainId,
        address newSequencer
    ) external onlyAppChainOwner(chainId) {
        require(newSequencer != address(0), "Invalid sequencer");
        address oldSequencer = appChains[chainId].sequencer;
        appChains[chainId].sequencer = newSequencer;
        emit SequencerChanged(chainId, oldSequencer, newSequencer);
    }

    function transferAppChainOwnership(
        uint64 chainId,
        address newOwner
    ) external onlyAppChainOwner(chainId) {
        require(newOwner != address(0), "Invalid owner");
        appChains[chainId].owner = newOwner;
    }

    // ═══════════════════════════════════════════
    //  AUTHORIZED SENDERS
    // ═══════════════════════════════════════════

    function setAuthorizedSender(
        uint64 chainId,
        address sender,
        bool authorized
    ) external onlyAppChainOwner(chainId) {
        authorizedSenders[chainId][sender] = authorized;
        emit AuthorizedSenderChanged(chainId, sender, authorized);
    }

    // ═══════════════════════════════════════════
    //  PROOF VERIFICATION
    // ═══════════════════════════════════════════

    function setVerifier(
        uint64 chainId,
        address verifier
    ) external onlyAppChainOwner(chainId) {
        address oldVerifier = verifiers[chainId];
        verifiers[chainId] = verifier;
        emit VerifierChanged(chainId, oldVerifier, verifier);
    }

    // ═══════════════════════════════════════════
    //  MESSAGING
    // ═══════════════════════════════════════════

    function sendMessage(
        uint64 sourceChainId,
        uint64 targetChainId,
        address target,
        bytes calldata data
    ) external payable {
        require(appChains[targetChainId].registered, "Target AppChain not registered");
        require(authorizedSenders[targetChainId][msg.sender], "Not authorized");
        require(msg.value >= messageFee, "Insufficient fee");
        require(data.length <= MAX_MESSAGE_SIZE, "Message too large");
        require(target != address(0), "Invalid target");

        uint256 fee = messageFee;
        uint256 hubCut = fee * hubFeeBps / 10000;
        ownerBalances[targetChainId] += fee - hubCut;
        hubBalance += hubCut;

        // Refund excess
        if (msg.value > fee) {
            payable(msg.sender).transfer(msg.value - fee);
        }

        uint64 messageId = appChains[targetChainId].nextMessageId;

        messages[targetChainId][messageId] = Message({
            sourceChainId: sourceChainId,
            sender: msg.sender,
            targetChainId: targetChainId,
            target: target,
            data: data,
            timestamp: block.timestamp
        });

        appChains[targetChainId].nextMessageId = messageId + 1;

        emit MessageSent(targetChainId, messageId, sourceChainId, msg.sender, target, data);
    }

    function getMessage(
        uint64 chainId,
        uint64 messageId
    ) external view returns (
        uint64 sourceChainId,
        address sender,
        address target,
        bytes memory data,
        uint256 timestamp
    ) {
        Message storage m = messages[chainId][messageId];
        return (m.sourceChainId, m.sender, m.target, m.data, m.timestamp);
    }

    function getMessageRange(
        uint64 chainId,
        uint64 fromId,
        uint64 toId
    ) external view returns (Message[] memory) {
        require(toId >= fromId, "Invalid range");
        require(toId - fromId <= 100, "Range too large");

        Message[] memory result = new Message[](toId - fromId);
        for (uint64 i = fromId; i < toId; i++) {
            result[i - fromId] = messages[chainId][i];
        }
        return result;
    }

    // ═══════════════════════════════════════════
    //  ANCHORING
    // ═══════════════════════════════════════════

    function anchor(
        uint64 chainId,
        uint64 blockNumber,
        bytes32 stateRoot,
        bytes32 txRoot,
        bytes32 receiptRoot,
        uint64 processedUpToMessageId,
        bytes calldata extraData
    ) external onlySequencer(chainId) {
        AppChainInfo storage info = appChains[chainId];
        require(blockNumber > info.latestBlock, "Block must be newer");
        require(
            processedUpToMessageId >= info.lastProcessedMessageId,
            "Cannot un-process messages"
        );
        require(
            processedUpToMessageId < info.nextMessageId,
            "Cannot process future messages"
        );

        anchors[chainId][blockNumber] = Anchor({
            stateRoot: stateRoot,
            txRoot: txRoot,
            receiptRoot: receiptRoot,
            timestamp: block.timestamp,
            extraData: extraData
        });

        info.latestBlock = blockNumber;
        info.lastProcessedMessageId = processedUpToMessageId;

        emit Anchored(chainId, blockNumber, stateRoot, txRoot, receiptRoot, processedUpToMessageId);
    }

    function getAnchor(
        uint64 chainId,
        uint64 blockNumber
    ) external view returns (
        bytes32 stateRoot,
        bytes32 txRoot,
        bytes32 receiptRoot,
        uint256 timestamp,
        bytes memory extraData
    ) {
        Anchor storage a = anchors[chainId][blockNumber];
        return (a.stateRoot, a.txRoot, a.receiptRoot, a.timestamp, a.extraData);
    }

    function verifyAnchor(
        uint64 chainId,
        uint64 blockNumber,
        bytes32 stateRoot,
        bytes32 txRoot,
        bytes32 receiptRoot
    ) external view returns (bool) {
        Anchor storage a = anchors[chainId][blockNumber];
        if (a.timestamp == 0) return false;
        return a.stateRoot == stateRoot &&
               a.txRoot == txRoot &&
               a.receiptRoot == receiptRoot;
    }

    function verifyAnchorProof(
        uint64 chainId,
        uint64 blockNumber,
        bytes calldata proof
    ) external view returns (bool) {
        Anchor storage a = anchors[chainId][blockNumber];
        if (a.timestamp == 0) return false;

        address verifier = verifiers[chainId];
        if (verifier == address(0)) {
            // Default: no proof verifier set, root matching only
            return true;
        }

        return IProofVerifier(verifier).verify(a.stateRoot, proof);
    }

    // ═══════════════════════════════════════════
    //  MESSAGE ACKNOWLEDGMENT
    // ═══════════════════════════════════════════

    function acknowledgeMessages(
        uint64 chainId,
        uint64 processedUpToMessageId,
        bytes32 messagesRoot
    ) external onlySequencer(chainId) {
        AppChainInfo storage info = appChains[chainId];
        require(info.registered, "Not registered");
        require(
            processedUpToMessageId >= info.lastProcessedMessageId,
            "Cannot un-process messages"
        );
        require(
            processedUpToMessageId < info.nextMessageId,
            "Cannot process future messages"
        );

        info.lastProcessedMessageId = processedUpToMessageId;
        messageRootCheckpoints[chainId] = MessageRootCheckpoint({
            processedUpToMessageId: processedUpToMessageId,
            merkleRoot: messagesRoot
        });

        emit MessagesAcknowledged(chainId, processedUpToMessageId, messagesRoot);
    }

    function getMessageRootCheckpoint(
        uint64 chainId
    ) external view returns (uint64 processedUpToMessageId, bytes32 merkleRoot) {
        MessageRootCheckpoint storage cp = messageRootCheckpoints[chainId];
        return (cp.processedUpToMessageId, cp.merkleRoot);
    }

    function verifyMessageInclusion(
        uint64 chainId,
        bytes32[] calldata proof,
        uint64 sourceChainId,
        uint64 messageId,
        bytes32 txHash,
        bool success,
        bytes32 dataHash
    ) external view returns (bool) {
        bytes32 root = messageRootCheckpoints[chainId].merkleRoot;
        require(root != bytes32(0), "No messages acknowledged yet");

        bytes32 leaf = keccak256(abi.encodePacked(
            sourceChainId, messageId, txHash, success, dataHash
        ));

        return _verifyMerkleProof(proof, root, leaf);
    }

    // ═══════════════════════════════════════════
    //  FEES
    // ═══════════════════════════════════════════

    function withdrawFees(uint64 chainId) external onlyAppChainOwner(chainId) {
        uint256 amount = ownerBalances[chainId];
        require(amount > 0, "No fees to withdraw");
        ownerBalances[chainId] = 0;
        payable(msg.sender).transfer(amount);
    }

    function withdrawHubFees() external onlyHubOwner {
        uint256 amount = hubBalance;
        require(amount > 0, "No fees to withdraw");
        hubBalance = 0;
        payable(msg.sender).transfer(amount);
    }

    // ═══════════════════════════════════════════
    //  HUB ADMIN
    // ═══════════════════════════════════════════

    function setRegistrationFee(uint256 newFee) external onlyHubOwner {
        emit RegistrationFeeChanged(registrationFee, newFee);
        registrationFee = newFee;
    }

    function setMessageFee(uint256 newFee) external onlyHubOwner {
        emit MessageFeeChanged(messageFee, newFee);
        messageFee = newFee;
    }

    function setHubFeeBps(uint256 newBps) external onlyHubOwner {
        require(newBps <= 10000, "Hub fee cannot exceed 100%");
        emit HubFeeBpsChanged(hubFeeBps, newBps);
        hubFeeBps = newBps;
    }

    function transferHubOwnership(address newOwner) external onlyHubOwner {
        require(newOwner != address(0), "Invalid owner");
        emit HubOwnerChanged(hubOwner, newOwner);
        hubOwner = newOwner;
    }

    // ═══════════════════════════════════════════
    //  VIEW HELPERS
    // ═══════════════════════════════════════════

    function getAppChainInfo(uint64 chainId) external view returns (
        address owner,
        address sequencer,
        uint64 latestBlock,
        uint64 lastProcessedMessageId,
        uint64 nextMessageId,
        bool registered
    ) {
        AppChainInfo storage info = appChains[chainId];
        return (
            info.owner,
            info.sequencer,
            info.latestBlock,
            info.lastProcessedMessageId,
            info.nextMessageId,
            info.registered
        );
    }

    function pendingMessageCount(uint64 chainId) external view returns (uint64) {
        AppChainInfo storage info = appChains[chainId];
        if (!info.registered) return 0;
        return info.nextMessageId - info.lastProcessedMessageId - 1;
    }

    // ═══════════════════════════════════════════
    //  INTERNAL: MERKLE PROOF (OZ-compatible sorted pairing)
    // ═══════════════════════════════════════════

    function _verifyMerkleProof(
        bytes32[] calldata proof,
        bytes32 root,
        bytes32 leaf
    ) internal pure returns (bool) {
        bytes32 computedHash = leaf;
        for (uint256 i = 0; i < proof.length; i++) {
            computedHash = _commutativeKeccak256(computedHash, proof[i]);
        }
        return computedHash == root;
    }

    function _commutativeKeccak256(bytes32 a, bytes32 b) internal pure returns (bytes32) {
        return a < b ? _efficientKeccak256(a, b) : _efficientKeccak256(b, a);
    }

    function _efficientKeccak256(bytes32 a, bytes32 b) internal pure returns (bytes32 value) {
        assembly ("memory-safe") {
            mstore(0x00, a)
            mstore(0x20, b)
            value := keccak256(0x00, 0x40)
        }
    }

    // ═══════════════════════════════════════════
    //  INTERNAL: SIGNATURE RECOVERY
    // ═══════════════════════════════════════════

    function _toEthSignedMessageHash(bytes32 hash) internal pure returns (bytes32) {
        return keccak256(abi.encodePacked("\x19Ethereum Signed Message:\n32", hash));
    }

    function _recover(bytes32 hash, bytes calldata sig) internal pure returns (address) {
        require(sig.length == 65, "Invalid signature length");
        bytes32 r;
        bytes32 s;
        uint8 v;
        assembly {
            r := calldataload(sig.offset)
            s := calldataload(add(sig.offset, 32))
            v := byte(0, calldataload(add(sig.offset, 64)))
        }
        if (v < 27) v += 27;
        require(v == 27 || v == 28, "Invalid signature v");
        address recovered = ecrecover(hash, v, r, s);
        require(recovered != address(0), "Invalid signature");
        return recovered;
    }
}
