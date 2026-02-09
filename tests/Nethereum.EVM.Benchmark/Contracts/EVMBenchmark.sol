// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

contract EVMBenchmark {
    uint256 public counter;
    mapping(uint256 => uint256) public data;
    bytes32 public lastHash;

    event BenchmarkEvent(uint256 indexed id, uint256 value);

    // Test arithmetic operations (ADD, MUL, SUB, DIV, MOD)
    function benchArithmetic(uint256 iterations) external returns (uint256) {
        uint256 result = 1;
        for (uint256 i = 1; i <= iterations; i++) {
            result = (result * 7 + i * 3) % 1000000007;
            result = result + (i * 2);
            result = result - (i / 2);
        }
        counter = result;
        return result;
    }

    // Test storage writes (SSTORE)
    function benchStorageWrite(uint256 iterations) external {
        for (uint256 i = 0; i < iterations; i++) {
            data[i] = i * 2 + 1;
        }
        counter = iterations;
    }

    // Test storage reads (SLOAD)
    function benchStorageRead(uint256 iterations) external returns (uint256) {
        uint256 sum = 0;
        for (uint256 i = 0; i < iterations; i++) {
            sum += data[i];
        }
        counter = sum;
        return sum;
    }

    // Test mixed storage (SLOAD + SSTORE)
    function benchStorageMixed(uint256 iterations) external returns (uint256) {
        uint256 sum = 0;
        for (uint256 i = 0; i < iterations; i++) {
            data[i] = data[i] + 1;
            sum += data[i];
        }
        counter = sum;
        return sum;
    }

    // Test memory operations (MLOAD, MSTORE)
    function benchMemory(uint256 size) external pure returns (bytes32) {
        bytes memory buffer = new bytes(size);
        for (uint256 i = 0; i < size; i++) {
            buffer[i] = bytes1(uint8(i & 0xFF));
        }
        return keccak256(buffer);
    }

    // Test keccak256 hashing (SHA3)
    function benchKeccak(uint256 iterations) external returns (bytes32) {
        bytes32 hash = bytes32(uint256(1));
        for (uint256 i = 0; i < iterations; i++) {
            hash = keccak256(abi.encodePacked(hash, i));
        }
        lastHash = hash;
        return hash;
    }

    // Test events (LOG operations)
    function benchEvents(uint256 count) external {
        for (uint256 i = 0; i < count; i++) {
            emit BenchmarkEvent(i, i * 2);
        }
        counter = count;
    }

    // Test EXP operation
    function benchExp(uint256 iterations) external returns (uint256) {
        uint256 result = 0;
        for (uint256 i = 1; i <= iterations; i++) {
            result += (2 ** (i % 32));
        }
        counter = result;
        return result;
    }

    // Test loops with minimal operations (measure loop overhead)
    function benchLoop(uint256 iterations) external returns (uint256) {
        uint256 i = 0;
        while (i < iterations) {
            i++;
        }
        counter = i;
        return i;
    }

    // Test CALL to self (internal calls)
    function benchInternalCalls(uint256 iterations) external returns (uint256) {
        uint256 result = 0;
        for (uint256 i = 0; i < iterations; i++) {
            result = this.addOne(result);
        }
        counter = result;
        return result;
    }

    function addOne(uint256 value) external pure returns (uint256) {
        return value + 1;
    }

    // Fibonacci - mixed stack operations
    function benchFibonacci(uint256 n) external returns (uint256) {
        if (n <= 1) {
            counter = n;
            return n;
        }
        uint256 a = 0;
        uint256 b = 1;
        for (uint256 i = 2; i <= n; i++) {
            uint256 c = a + b;
            a = b;
            b = c;
        }
        counter = b;
        return b;
    }

    // Complex: simulates realistic contract workload
    function benchComplex(uint256 iterations) external returns (uint256) {
        uint256 result = 0;
        for (uint256 i = 0; i < iterations; i++) {
            // Storage write
            data[i] = i * 3 + 1;
            // Arithmetic
            result = (result + data[i]) % 1000000007;
            // Hash
            if (i % 10 == 0) {
                lastHash = keccak256(abi.encodePacked(result, i));
            }
            // Event every 20 iterations
            if (i % 20 == 0) {
                emit BenchmarkEvent(i, result);
            }
        }
        counter = result;
        return result;
    }
}
