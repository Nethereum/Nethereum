// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

contract StorageTest {
    // slot 0: mapping(address => uint256)
    mapping(address => uint256) public balances;

    function setBalance(address account, uint256 amount) external {
        balances[account] = amount;
    }
}
