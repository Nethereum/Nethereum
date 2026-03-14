// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

// Standard Gnosis Safe MultiSend contract for testing
contract MultiSend {
    function multiSend(bytes memory transactions) public payable {
        assembly {
            let length := mload(transactions)
            let i := 0x20
            for { } lt(i, add(length, 0x20)) { } {
                let operation := shr(0xf8, mload(add(transactions, i)))
                let to := shr(0x60, mload(add(transactions, add(i, 0x01))))
                let value := mload(add(transactions, add(i, 0x15)))
                let dataLength := mload(add(transactions, add(i, 0x35)))
                let data := add(transactions, add(i, 0x55))
                let success := 0
                switch operation
                case 0 { success := call(gas(), to, value, data, dataLength, 0, 0) }
                case 1 { success := delegatecall(gas(), to, data, dataLength, 0, 0) }
                if eq(success, 0) { revert(0, 0) }
                i := add(i, add(0x55, dataLength))
            }
        }
    }
}

// Simple counter contract to be called via MultiSend
contract Counter {
    uint256 public count;

    function increment() external {
        count++;
    }

    function incrementBy(uint256 amount) external {
        count += amount;
    }
}
