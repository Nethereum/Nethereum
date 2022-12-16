 // SPDX-License-Identifier: MIT
// OpenZeppelin Contracts (last updated v4.8.0) (utils/cryptography/MerkleProof.sol)
pragma solidity ^0.8.0;
import "./MerkleProof.sol";

contract MerkleERC20Drop is MerkleProof {

    event Transfer(address indexed from, address indexed to, uint256 value);
    event Approval(address indexed owner, address indexed spender, uint256 value);
    event ClaimedMerkleDrop(address indexed receiver, uint256 value);
    string public name;
    string public symbol;
    uint8 public immutable decimals;
    uint public  totalSupply;
    bytes32 public root;
    address initialSupplyAddress;
    mapping(address => uint) public  balanceOf;
    mapping(address => bool) public  claimed;
    mapping(address => mapping(address => uint)) public  allowance;

    constructor(
        uint8 _decimals,
        string memory _name,
        string memory _symbol,
        uint256 _initialSupply,
        bytes32 _root
    ) {
        decimals = _decimals;
        name = _name;
        symbol = _symbol;
        initialSupplyAddress = msg.sender;
        balanceOf[msg.sender] = _initialSupply;
        totalSupply = _initialSupply;
        root = _root;
    }

    function _approve(address owner, address spender, uint value) private {
        allowance[owner][spender] = value;
        emit Approval(owner, spender, value);
    }

    function _transfer(address from, address to, uint value) private {
        balanceOf[from] = balanceOf[from] - value;
        balanceOf[to] = balanceOf[to] + value;
        emit Transfer(from, to, value);
    }

    function approve(address spender, uint value) external  returns (bool) {
        _approve(msg.sender, spender, value);
        return true;
    }

    function transfer(address to, uint value) external  returns (bool) {
        _transfer(msg.sender, to, value);
        return true;
    }

    function transferFrom(address from, address to, uint value) external  returns (bool) {
        if (allowance[from][msg.sender] != 0) {
            allowance[from][msg.sender] = allowance[from][msg.sender] - value;
        }
        _transfer(from, to, value);
        return true;
    }

    function claim(uint256 balance, bytes32[] memory merkleProof) public {
        _claim(msg.sender, balance, merkleProof);
    }

   function verifyClaim(address claimAddress, uint256 balance, bytes32[] memory merkleProof) 
          public view returns (bool valid) {
              return _verify(claimAddress, balance, merkleProof);
    }

     function _claim(
        address claimAddress,
        uint256 balance,
        bytes32[] memory merkleProof
    ) private {
        require(claimed[claimAddress] == false, "Address has already been claimed");
        require(_verify(claimAddress, balance, merkleProof), "Incorrect merkle proof");
        claimed[claimAddress] = true;
        _transfer(initialSupplyAddress, claimAddress, balance);
        emit ClaimedMerkleDrop(claimAddress, balance);
  }

    function _verify(
                address _claimer,
                uint256 _balance,
                bytes32[] memory _merkleProof
      ) private view returns (bool valid) {
        bytes32 leaf = keccak256(abi.encodePacked(_claimer, _balance));
        return MerkleProof.verify(_merkleProof, root, leaf);
    }
}