// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

error InsufficientBalance(address account, uint256 required, uint256 available);
error Unauthorized(address caller);

contract MultiCallTest {
    address public owner;
    mapping(address => uint256) public balances;
    address public childContract;

    event Deposit(address indexed from, uint256 amount);
    event Withdrawal(address indexed to, uint256 amount);
    event ChildCreated(address indexed child);
    event InternalCallResult(address indexed target, bool success, bytes data);

    constructor() {
        owner = msg.sender;
    }

    function deposit() external payable {
        balances[msg.sender] += msg.value;
        emit Deposit(msg.sender, msg.value);
    }

    function withdraw(uint256 amount) external {
        if (balances[msg.sender] < amount)
            revert InsufficientBalance(msg.sender, amount, balances[msg.sender]);
        balances[msg.sender] -= amount;
        payable(msg.sender).transfer(amount);
        emit Withdrawal(msg.sender, amount);
    }

    function forwardDeposit(address target) external payable {
        (bool success, bytes memory data) = target.call{value: msg.value}(
            abi.encodeWithSignature("deposit()")
        );
        emit InternalCallResult(target, success, data);
        require(success, "Forward failed");
    }

    function createChild() external {
        if (msg.sender != owner) revert Unauthorized(msg.sender);
        ChildHelper child = new ChildHelper(address(this));
        childContract = address(child);
        emit ChildCreated(address(child));
    }

    function pingChild() external returns (uint256) {
        require(childContract != address(0), "No child");
        (bool success, bytes memory data) = childContract.call(
            abi.encodeWithSignature("ping()")
        );
        require(success, "Ping failed");
        return abi.decode(data, (uint256));
    }

    function getChildCounter() external view returns (uint256) {
        require(childContract != address(0), "No child");
        (bool success, bytes memory data) = childContract.staticcall(
            abi.encodeWithSignature("counter()")
        );
        require(success, "Static call failed");
        return abi.decode(data, (uint256));
    }

    function pong() external view returns (address) {
        return owner;
    }

    function getBalance() external view returns (uint256) {
        return address(this).balance;
    }

    receive() external payable {
        balances[msg.sender] += msg.value;
        emit Deposit(msg.sender, msg.value);
    }
}

contract ChildHelper {
    address public parent;
    uint256 public counter;

    event Pinged(address indexed from, uint256 count);

    constructor(address _parent) {
        parent = _parent;
    }

    function ping() external returns (uint256) {
        counter++;
        (bool success,) = parent.staticcall(
            abi.encodeWithSignature("pong()")
        );
        require(success, "Pong failed");
        emit Pinged(msg.sender, counter);
        return counter;
    }
}
