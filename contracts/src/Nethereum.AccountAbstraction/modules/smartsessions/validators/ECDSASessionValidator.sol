// SPDX-License-Identifier: MIT
pragma solidity ^0.8.25;

import { ISessionValidator } from "../interfaces/ISessionValidator.sol";
import { ERC7579_MODULE_TYPE_STATELESS_VALIDATOR } from "../DataTypes.sol";
import { ECDSA } from "@openzeppelin/contracts/utils/cryptography/ECDSA.sol";
import { MessageHashUtils } from "@openzeppelin/contracts/utils/cryptography/MessageHashUtils.sol";

/// @title ECDSASessionValidator
/// @notice SmartSession `ISessionValidator` backed by a plain secp256k1 session key.
/// @dev `data` is the session key address, packed as the raw 20 bytes SmartSessionConfig writes into
/// `Session.sessionValidatorInitData` (mirrors ECDSAValidator's own `onInstall` encoding, not
/// `abi.encode(address)`). Tries a raw-hash recovery first (matches ECDSAValidator/EntryPoint's
/// userOpHash convention) and falls back to the EIP-191 personal-message hash, so the same validator
/// also backs ERC-1271-style session signing.
contract ECDSASessionValidator is ISessionValidator {
    using ECDSA for bytes32;
    using MessageHashUtils for bytes32;

    error InvalidSessionKeyData();

    function validateSignatureWithData(
        bytes32 hash,
        bytes calldata sig,
        bytes calldata data
    )
        external
        pure
        override
        returns (bool validSig)
    {
        if (data.length != 20) revert InvalidSessionKeyData();
        address sessionKey = address(bytes20(data[0:20]));

        if (hash.recover(sig) == sessionKey) return true;
        return hash.toEthSignedMessageHash().recover(sig) == sessionKey;
    }

    function onInstall(bytes calldata) external pure override { }

    function onUninstall(bytes calldata) external pure override { }

    function isModuleType(uint256 moduleTypeId) external pure override returns (bool) {
        return moduleTypeId == ERC7579_MODULE_TYPE_STATELESS_VALIDATOR;
    }

    function isInitialized(address) external pure override returns (bool) {
        return true;
    }
}
