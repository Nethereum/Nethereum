// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

// Documentation only — bytecode is hand-crafted in GenerateTestWitness.cs
// Calls BN128 pairing check via precompile 0x08 with the identity pairing:
//   e(G1, G2) * e(-G1, G2) == 1
//
// Input: 384 bytes = 2 pairs of (G1 point 64B + G2 point 128B)
// Pair 1: G1=(1,2), G2=standard generator
// Pair 2: -G1=(1, p-2), G2=standard generator
// Expected result: 1 (valid pairing)

contract Bn128PairingTest {
    uint256 public pairingResult;

    function check() external {
        // Identity pairing — exercises full Miller loop + final exponentiation
    }
}
