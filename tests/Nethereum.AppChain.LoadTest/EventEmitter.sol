// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

contract EventEmitter {
    mapping(uint256 => uint256) public data;

    event DataSet(uint256 indexed key, uint256 value);
    event Ping(uint256 indexed id, uint256 timestamp);

    function setStorage(uint256 key, uint256 value) external {
        data[key] = value;
        emit DataSet(key, value);
    }

    function emitEvents(uint256 count) external {
        for (uint256 i = 0; i < count; i++) {
            emit Ping(i, block.timestamp);
        }
    }

    function emitSingle() external {
        emit Ping(block.number, block.timestamp);
    }
}
