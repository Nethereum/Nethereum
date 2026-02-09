// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "./BasePaymaster.sol";
import "../interfaces/IPaymaster.sol";

contract DepositPaymaster is BasePaymaster, IDepositPaymaster, ReentrancyGuard {
    mapping(address => uint256) private _deposits;

    uint256 public minDeposit;

    event MinDepositChanged(uint256 oldMin, uint256 newMin);

    error InsufficientUserDeposit();
    error WithdrawFailed();

    constructor(
        address _entryPoint,
        address _owner
    ) BasePaymaster(_entryPoint, _owner) {
        minDeposit = 0;
    }

    function deposits(address account) external view override returns (uint256) {
        return _deposits[account];
    }

    function deposit() external payable override(BasePaymaster, IDepositPaymaster) nonReentrant {
        _deposits[msg.sender] += msg.value;
        emit Deposited(msg.sender, msg.value);
    }

    function depositFor(address account) external payable override nonReentrant {
        _deposits[account] += msg.value;
        emit Deposited(account, msg.value);
    }

    function withdraw(uint256 amount) external override nonReentrant {
        _withdrawInternal(msg.sender, payable(msg.sender), amount);
    }

    function withdrawTo(address payable to, uint256 amount) external override(BasePaymaster, IDepositPaymaster) nonReentrant {
        _withdrawInternal(msg.sender, to, amount);
    }

    function _withdrawInternal(
        address from,
        address payable to,
        uint256 amount
    ) internal {
        require(_deposits[from] >= amount, "Insufficient deposit");
        _deposits[from] -= amount;

        (bool success, ) = to.call{value: amount}("");
        if (!success) revert WithdrawFailed();

        emit Withdrawn(from, to, amount);
    }

    function setMinDeposit(uint256 min) external onlyOwner {
        uint256 oldMin = minDeposit;
        minDeposit = min;
        emit MinDepositChanged(oldMin, min);
    }

    function validatePaymasterUserOp(
        PackedUserOperation calldata userOp,
        bytes32,
        uint256 maxCost
    )
        external
        override(BasePaymaster, IPaymaster)
        onlyEntryPoint
        returns (bytes memory context, uint256 validationData)
    {
        address sender = userOp.sender;

        if (_deposits[sender] < maxCost) {
            return ("", SIG_VALIDATION_FAILED);
        }

        _deposits[sender] -= maxCost;

        context = abi.encode(sender, maxCost);
        validationData = 0;
    }

    function postOp(
        PostOpMode mode,
        bytes calldata context,
        uint256 actualGasCost,
        uint256
    ) external override(BasePaymaster, IPaymaster) onlyEntryPoint {
        (address sender, uint256 maxCost) = abi.decode(context, (address, uint256));

        if (mode == PostOpMode.OpReverted) {
            _deposits[sender] += maxCost;
            return;
        }

        uint256 refund = maxCost - actualGasCost;
        if (refund > 0) {
            _deposits[sender] += refund;
        }
    }

    function getDepositInfo(
        address account
    ) external view returns (uint256 balance, bool canPayFor) {
        balance = _deposits[account];
        canPayFor = balance >= minDeposit;
    }
}
