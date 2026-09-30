// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

// Documentation only — bytecode is hand-crafted in GenerateTestWitness.cs
// Calls BN128 ecAdd(G1, G1) via precompile 0x06 and ecMul(G1, 2) via precompile 0x07.
// Verifies both produce the same result (2*G1) and stores match flag in slot 0.
// G1 = (1, 2) is the BN254 curve generator.

contract Bn128AddMulTest {
    uint256 public matchResult;

    function test() external {
        // ecAdd(G1, G1) via precompile 0x06 → 2*G1
        // ecMul(G1, 2) via precompile 0x07 → 2*G1
        // Compare results, store 1 if match
    }
}
