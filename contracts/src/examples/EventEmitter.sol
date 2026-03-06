// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

contract EventEmitter {
    event TestEvent(uint256 indexed value, address indexed sender);
    uint256 public counter;

    function emitEvent() external {
        counter++;
        emit TestEvent(counter, msg.sender);
    }
}
