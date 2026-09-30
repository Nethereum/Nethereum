// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "@openzeppelin/contracts/utils/cryptography/ECDSA.sol";
import "@openzeppelin/contracts/utils/cryptography/MessageHashUtils.sol";
import "./BasePaymaster.sol";
import "../interfaces/IPaymaster.sol";

contract VerifyingPaymaster is BasePaymaster, IVerifyingPaymaster {
    using ECDSA for bytes32;
    using MessageHashUtils for bytes32;

    address private _verifyingSigner;

    mapping(address => uint256) public senderNonce;

    event SignerChanged(address indexed oldSigner, address indexed newSigner);

    error InvalidSignature();
    error ExpiredSignature();

    constructor(
        address _entryPoint,
        address _owner,
        address _signer
    ) BasePaymaster(_entryPoint, _owner) {
        _verifyingSigner = _signer;
    }

    function verifyingSigner() external view override returns (address) {
        return _verifyingSigner;
    }

    function setVerifyingSigner(address signer) external override onlyOwner {
        address oldSigner = _verifyingSigner;
        _verifyingSigner = signer;
        emit SignerChanged(oldSigner, signer);
    }

    function validatePaymasterUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash,
        uint256 maxCost
    )
        external
        override(BasePaymaster, IPaymaster)
        onlyEntryPoint
        returns (bytes memory context, uint256 validationData)
    {
        (uint48 validUntil, uint48 validAfter, bytes memory signature) = _parsePaymasterData(
            userOp.paymasterAndData
        );

        if (block.timestamp > validUntil && validUntil != 0) {
            return ("", _packValidationData(true, validUntil, validAfter));
        }

        bytes32 hash = _getHash(userOp, validUntil, validAfter);
        address signer = hash.toEthSignedMessageHash().recover(signature);

        if (signer != _verifyingSigner) {
            return ("", _packValidationData(true, validUntil, validAfter));
        }

        context = abi.encode(userOp.sender, maxCost);
        validationData = _packValidationData(false, validUntil, validAfter);
    }

    function postOp(
        PostOpMode,
        bytes calldata context,
        uint256 actualGasCost,
        uint256
    ) external override(BasePaymaster, IPaymaster) onlyEntryPoint {
        (address sender, ) = abi.decode(context, (address, uint256));
        emit GasSponsored(sender, actualGasCost);
    }

    // ERC-4337 v0.7+ paymasterAndData layout:
    //   [0:20]   paymaster address
    //   [20:36]  paymasterVerificationGasLimit
    //   [36:52]  paymasterPostOpGasLimit
    //   [52:]    paymasterData -> validUntil(6) | validAfter(6) | signature
    // The paymaster-specific data begins at PAYMASTER_DATA_OFFSET (52), after the
    // address and the two packed gas-limit fields the EntryPoint reads.
    uint256 private constant PAYMASTER_DATA_OFFSET = 52;

    function _parsePaymasterData(
        bytes calldata paymasterAndData
    ) internal pure returns (uint48 validUntil, uint48 validAfter, bytes memory signature) {
        require(paymasterAndData.length >= PAYMASTER_DATA_OFFSET + 6 + 6, "Invalid paymaster data");

        validUntil = uint48(bytes6(paymasterAndData[PAYMASTER_DATA_OFFSET:PAYMASTER_DATA_OFFSET + 6]));
        validAfter = uint48(bytes6(paymasterAndData[PAYMASTER_DATA_OFFSET + 6:PAYMASTER_DATA_OFFSET + 12]));
        signature = paymasterAndData[PAYMASTER_DATA_OFFSET + 12:];
    }

    function _getHash(
        PackedUserOperation calldata userOp,
        uint48 validUntil,
        uint48 validAfter
    ) internal view returns (bytes32) {
        return
            keccak256(
                abi.encode(
                    userOp.sender,
                    userOp.nonce,
                    keccak256(userOp.initCode),
                    keccak256(userOp.callData),
                    userOp.accountGasLimits,
                    userOp.preVerificationGas,
                    userOp.gasFees,
                    block.chainid,
                    address(this),
                    validUntil,
                    validAfter
                )
            );
    }

    function getHash(
        PackedUserOperation calldata userOp,
        uint48 validUntil,
        uint48 validAfter
    ) external view returns (bytes32) {
        return _getHash(userOp, validUntil, validAfter);
    }
}
