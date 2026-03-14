// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

contract ERC1271Mock {
    bytes4 internal constant MAGICVALUE = 0x1626ba7e;
    bytes4 internal constant INVALID = 0xffffffff;

    address public owner;

    constructor() {
        owner = msg.sender;
    }

    function isValidSignature(bytes32 hash, bytes memory signature) external view returns (bytes4) {
        // Simple mock: recover signer from signature and check against owner
        // For testing, we accept any signature where the first 20 bytes match the owner
        if (signature.length >= 20) {
            address signer;
            assembly {
                signer := mload(add(signature, 20))
            }
            if (signer == owner) {
                return MAGICVALUE;
            }
        }
        return INVALID;
    }
}
