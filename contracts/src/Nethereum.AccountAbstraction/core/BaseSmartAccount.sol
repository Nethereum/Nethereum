// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "@openzeppelin/contracts/proxy/utils/UUPSUpgradeable.sol";
import "@openzeppelin/contracts/proxy/utils/Initializable.sol";
import "@openzeppelin/contracts/utils/cryptography/ECDSA.sol";
import "@openzeppelin/contracts/utils/cryptography/MessageHashUtils.sol";
import "../interfaces/IAccount.sol";
import "../interfaces/PackedUserOperation.sol";

abstract contract BaseSmartAccount is IAccount, IAccountExecute, UUPSUpgradeable, Initializable {
    using ECDSA for bytes32;
    using MessageHashUtils for bytes32;

    uint256 internal constant SIG_VALIDATION_FAILED = 1;
    uint256 internal constant SIG_VALIDATION_SUCCESS = 0;

    address public immutable entryPoint;

    address public owner;

    uint256[49] private __gap;

    error OnlyOwner();
    error OnlyEntryPoint();
    error OnlyOwnerOrEntryPoint();
    error ExecutionFailed();
    error ArrayLengthMismatch();

    modifier onlyOwner() {
        if (msg.sender != owner) revert OnlyOwner();
        _;
    }

    modifier onlyEntryPoint() {
        if (msg.sender != entryPoint) revert OnlyEntryPoint();
        _;
    }

    modifier onlyOwnerOrEntryPoint() {
        if (msg.sender != owner && msg.sender != entryPoint) {
            revert OnlyOwnerOrEntryPoint();
        }
        _;
    }

    constructor(address _entryPoint) {
        entryPoint = _entryPoint;
        _disableInitializers();
    }

    function _initializeOwner(address _owner) internal {
        owner = _owner;
    }

    function validateUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash,
        uint256 missingAccountFunds
    ) external virtual onlyEntryPoint returns (uint256 validationData) {
        validationData = _validateSignature(userOp, userOpHash);
        _payPrefund(missingAccountFunds);
    }

    function _validateSignature(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash
    ) internal virtual returns (uint256 validationData) {
        address signer = _recoverSigner(userOpHash, userOp.signature);
        if (signer == owner) {
            return SIG_VALIDATION_SUCCESS;
        }
        return SIG_VALIDATION_FAILED;
    }

    function execute(
        address target,
        uint256 value,
        bytes calldata data
    ) external virtual onlyOwnerOrEntryPoint returns (bytes memory) {
        return _execute(target, value, data);
    }

    function executeBatch(
        address[] calldata targets,
        uint256[] calldata values,
        bytes[] calldata datas
    ) external virtual onlyOwnerOrEntryPoint returns (bytes[] memory results) {
        if (targets.length != values.length || values.length != datas.length) {
            revert ArrayLengthMismatch();
        }

        results = new bytes[](targets.length);
        for (uint256 i = 0; i < targets.length; i++) {
            results[i] = _execute(targets[i], values[i], datas[i]);
        }
    }

    function executeUserOp(
        PackedUserOperation calldata userOp,
        bytes32
    ) external virtual onlyEntryPoint {
        // Execute the callData from the UserOperation
        if (userOp.callData.length > 0) {
            (bool success, bytes memory result) = address(this).call(userOp.callData);
            if (!success) {
                assembly {
                    revert(add(result, 32), mload(result))
                }
            }
        }
    }

    function _execute(
        address target,
        uint256 value,
        bytes calldata data
    ) internal virtual returns (bytes memory) {
        (bool success, bytes memory result) = target.call{value: value}(data);
        if (!success) {
            assembly {
                revert(add(result, 32), mload(result))
            }
        }
        return result;
    }

    function _recoverSigner(
        bytes32 hash,
        bytes memory signature
    ) internal pure returns (address) {
        // EIP-712: userOpHash from EntryPoint is already properly encoded
        // Do NOT wrap with toEthSignedMessageHash() as that breaks EIP-712 standard
        return hash.recover(signature);
    }

    function _packValidationData(
        bool sigFailed,
        uint48 validUntil,
        uint48 validAfter
    ) internal pure returns (uint256) {
        return
            (sigFailed ? SIG_VALIDATION_FAILED : SIG_VALIDATION_SUCCESS) |
            (uint256(validUntil) << 160) |
            (uint256(validAfter) << (160 + 48));
    }

    function _payPrefund(uint256 missingAccountFunds) internal virtual {
        if (missingAccountFunds > 0) {
            (bool success, ) = payable(entryPoint).call{value: missingAccountFunds}("");
            require(success, "Prefund failed");
        }
    }

    function _authorizeUpgrade(
        address newImplementation
    ) internal virtual override onlyOwner {}

    function getDeposit() public view returns (uint256) {
        return IEntryPoint(entryPoint).balanceOf(address(this));
    }

    function addDeposit() public payable {
        IEntryPoint(entryPoint).depositTo{value: msg.value}(address(this));
    }

    function withdrawDepositTo(
        address payable withdrawAddress,
        uint256 amount
    ) public onlyOwner {
        IEntryPoint(entryPoint).withdrawTo(withdrawAddress, amount);
    }

    receive() external payable {}
}

interface IEntryPoint {
    function balanceOf(address account) external view returns (uint256);
    function depositTo(address account) external payable;
    function withdrawTo(address payable withdrawAddress, uint256 withdrawAmount) external;
}
