// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

// Documentation only — bytecode is hand-crafted in GenerateTestWitness.cs
// Calls ecrecover(hash, v, r, s) via precompile at 0x01 and stores the recovered address.
//
// EVM bytecode layout:
//   1. PUSH32 <hash> MSTORE at 0x00
//   2. PUSH1 <v> MSTORE at 0x20
//   3. PUSH32 <r> MSTORE at 0x40
//   4. PUSH32 <s> MSTORE at 0x60
//   5. STATICCALL(gas, 0x01, 0x00, 0x80, 0x80, 0x20)
//   6. MLOAD 0x80, SSTORE to slot 0
//   7. RETURN 32 bytes from 0x80

contract EcRecoverTest {
    address public recovered;

    function verify(bytes32 hash, uint8 v, bytes32 r, bytes32 s) external {
        recovered = ecrecover(hash, v, r, s);
    }
}
