// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

contract AppChainAnchor {
    uint256 public immutable appChainId;
    address public sequencer;

    struct Anchor {
        bytes32 stateRoot;
        bytes32 txRoot;
        bytes32 receiptRoot;
        uint256 timestamp;
    }

    mapping(uint256 => Anchor) public anchors;
    uint256 public latestBlock;

    event Anchored(
        uint256 indexed blockNumber,
        bytes32 stateRoot,
        bytes32 txRoot,
        bytes32 receiptRoot
    );

    event SequencerChanged(address indexed oldSequencer, address indexed newSequencer);

    modifier onlySequencer() {
        require(msg.sender == sequencer, "Only sequencer");
        _;
    }

    constructor(uint256 _appChainId, address _sequencer) {
        appChainId = _appChainId;
        sequencer = _sequencer;
    }

    function anchor(
        uint256 blockNumber,
        bytes32 stateRoot,
        bytes32 txRoot,
        bytes32 receiptRoot
    ) external onlySequencer {
        require(blockNumber > latestBlock, "Block must be newer");

        anchors[blockNumber] = Anchor({
            stateRoot: stateRoot,
            txRoot: txRoot,
            receiptRoot: receiptRoot,
            timestamp: block.timestamp
        });

        latestBlock = blockNumber;

        emit Anchored(blockNumber, stateRoot, txRoot, receiptRoot);
    }

    function getAnchor(uint256 blockNumber) external view returns (
        bytes32 stateRoot,
        bytes32 txRoot,
        bytes32 receiptRoot,
        uint256 timestamp
    ) {
        Anchor storage a = anchors[blockNumber];
        return (a.stateRoot, a.txRoot, a.receiptRoot, a.timestamp);
    }

    function verifyAnchor(
        uint256 blockNumber,
        bytes32 stateRoot,
        bytes32 txRoot,
        bytes32 receiptRoot
    ) external view returns (bool) {
        Anchor storage a = anchors[blockNumber];
        if (a.timestamp == 0) return false;
        return a.stateRoot == stateRoot &&
               a.txRoot == txRoot &&
               a.receiptRoot == receiptRoot;
    }

    function setSequencer(address newSequencer) external onlySequencer {
        address oldSequencer = sequencer;
        sequencer = newSequencer;
        emit SequencerChanged(oldSequencer, newSequencer);
    }
}
