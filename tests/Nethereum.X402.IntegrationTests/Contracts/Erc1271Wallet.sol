// SPDX-License-Identifier: MIT
pragma solidity ^0.8.28;

/// @notice Minimal ERC-1271 smart-contract wallet for x402 tests. It validates a signature by
/// recovering an EOA owner (EIP-712 digests are pre-hashed by the caller) and exposes `execute`
/// so the owner can drive on-chain calls from the wallet (e.g. approving Permit2). This is the
/// simplest thing that lets Permit2's ERC-1271 branch and the facilitator's ERC-1271 fallback run.
contract Erc1271Wallet {
    bytes4 internal constant MAGICVALUE = 0x1626ba7e;
    address public owner;

    constructor(address _owner) {
        owner = _owner;
    }

    function isValidSignature(bytes32 hash, bytes calldata signature) external view returns (bytes4) {
        if (signature.length != 65) {
            return 0xffffffff;
        }
        bytes32 r;
        bytes32 s;
        uint8 v;
        assembly {
            r := calldataload(signature.offset)
            s := calldataload(add(signature.offset, 32))
            v := byte(0, calldataload(add(signature.offset, 64)))
        }
        address recovered = ecrecover(hash, v, r, s);
        if (recovered != address(0) && recovered == owner) {
            return MAGICVALUE;
        }
        return 0xffffffff;
    }

    function execute(address target, bytes calldata data) external returns (bytes memory) {
        require(msg.sender == owner, "not owner");
        (bool ok, bytes memory ret) = target.call(data);
        require(ok, "call failed");
        return ret;
    }

    receive() external payable {}
}
