// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "@openzeppelin/contracts/access/Ownable.sol";
import "../interfaces/IPaymaster.sol";
import "../interfaces/PackedUserOperation.sol";

interface IEntryPointDeposit {
    function balanceOf(address account) external view returns (uint256);
    function depositTo(address account) external payable;
    function withdrawTo(address payable withdrawAddress, uint256 withdrawAmount) external;
}

abstract contract BasePaymaster is IPaymaster, Ownable {
    address public immutable entryPoint;

    uint256 internal constant SIG_VALIDATION_FAILED = 1;

    error OnlyEntryPoint();
    error InsufficientDeposit();

    modifier onlyEntryPoint() {
        if (msg.sender != entryPoint) revert OnlyEntryPoint();
        _;
    }

    constructor(address _entryPoint, address _owner) Ownable(_owner) {
        entryPoint = _entryPoint;
    }

    function validatePaymasterUserOp(
        PackedUserOperation calldata userOp,
        bytes32 userOpHash,
        uint256 maxCost
    ) external virtual override returns (bytes memory context, uint256 validationData) {
        _requireOnlyEntryPoint();
    }

    function postOp(
        PostOpMode mode,
        bytes calldata context,
        uint256 actualGasCost,
        uint256 actualUserOpFeePerGas
    ) external virtual override {
        _requireOnlyEntryPoint();
    }

    function _requireOnlyEntryPoint() internal view {
        if (msg.sender != entryPoint) revert OnlyEntryPoint();
    }

    function getDeposit() public view returns (uint256) {
        return IEntryPointDeposit(entryPoint).balanceOf(address(this));
    }

    function deposit() external payable virtual {
        IEntryPointDeposit(entryPoint).depositTo{value: msg.value}(address(this));
    }

    function withdrawTo(address payable to, uint256 amount) external virtual onlyOwner {
        IEntryPointDeposit(entryPoint).withdrawTo(to, amount);
    }

    function _packValidationData(
        bool sigFailed,
        uint48 validUntil,
        uint48 validAfter
    ) internal pure returns (uint256) {
        return
            (sigFailed ? 1 : 0) |
            (uint256(validUntil) << 160) |
            (uint256(validAfter) << 208);
    }

    receive() external payable {
        IEntryPointDeposit(entryPoint).depositTo{value: msg.value}(address(this));
    }
}
