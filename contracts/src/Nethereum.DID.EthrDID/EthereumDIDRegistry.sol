// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

contract EthereumDIDRegistry {

    mapping(address => address) public owners;
    mapping(address => mapping(bytes32 => mapping(address => uint))) public delegates;
    mapping(address => uint) public changed;
    mapping(address => uint) public nonce;

    modifier onlyOwner(address identity, address actor) {
        require(actor == identityOwner(identity), "bad_actor");
        _;
    }

    event DIDOwnerChanged(
        address indexed identity,
        address owner,
        uint previousChange
    );

    event DIDDelegateChanged(
        address indexed identity,
        bytes32 delegateType,
        address delegate_,
        uint validTo,
        uint previousChange
    );

    event DIDAttributeChanged(
        address indexed identity,
        bytes32 name,
        bytes value,
        uint validTo,
        uint previousChange
    );

    function identityOwner(address identity) public view returns(address) {
        address owner = owners[identity];
        if (owner != address(0x00)) {
            return owner;
        }
        return identity;
    }

    function checkSignature(address identity, uint8 sigV, bytes32 sigR, bytes32 sigS, bytes32 hash) internal returns(address) {
        address signer = ecrecover(hash, sigV, sigR, sigS);
        require(signer == identityOwner(identity), "bad_signature");
        nonce[signer]++;
        return signer;
    }

    function validDelegate(address identity, bytes32 delegateType, address delegate_) public view returns(bool) {
        return delegates[identity][keccak256(abi.encode(delegateType))][delegate_] > block.timestamp;
    }

    function changeOwner(address identity, address newOwner) internal onlyOwner(identity, msg.sender) {
        owners[identity] = newOwner;
        emit DIDOwnerChanged(identity, newOwner, changed[identity]);
        changed[identity] = block.number;
    }

    function changeOwner(address identity, address actor, address newOwner) public onlyOwner(identity, actor) {
        owners[identity] = newOwner;
        emit DIDOwnerChanged(identity, newOwner, changed[identity]);
        changed[identity] = block.number;
    }

    function changeOwnerSigned(address identity, uint8 sigV, bytes32 sigR, bytes32 sigS, address newOwner) public {
        bytes32 hash = keccak256(abi.encodePacked(bytes1(0x19), bytes1(0x00), address(this), nonce[identityOwner(identity)], identity, "changeOwner", newOwner));
        changeOwner(identity, checkSignature(identity, sigV, sigR, sigS, hash), newOwner);
    }

    function addDelegate(address identity, address actor, bytes32 delegateType, address delegate_, uint validity) public onlyOwner(identity, actor) {
        delegates[identity][keccak256(abi.encode(delegateType))][delegate_] = block.timestamp + validity;
        emit DIDDelegateChanged(identity, delegateType, delegate_, block.timestamp + validity, changed[identity]);
        changed[identity] = block.number;
    }

    function addDelegate(address identity, bytes32 delegateType, address delegate_, uint validity) public {
        addDelegate(identity, msg.sender, delegateType, delegate_, validity);
    }

    function addDelegateSigned(address identity, uint8 sigV, bytes32 sigR, bytes32 sigS, bytes32 delegateType, address delegate_, uint validity) public {
        bytes32 hash = keccak256(abi.encodePacked(bytes1(0x19), bytes1(0x00), address(this), nonce[identityOwner(identity)], identity, "addDelegate", delegateType, delegate_, validity));
        addDelegate(identity, checkSignature(identity, sigV, sigR, sigS, hash), delegateType, delegate_, validity);
    }

    function revokeDelegate(address identity, address actor, bytes32 delegateType, address delegate_) public onlyOwner(identity, actor) {
        delegates[identity][keccak256(abi.encode(delegateType))][delegate_] = block.timestamp;
        emit DIDDelegateChanged(identity, delegateType, delegate_, block.timestamp, changed[identity]);
        changed[identity] = block.number;
    }

    function revokeDelegate(address identity, bytes32 delegateType, address delegate_) public {
        revokeDelegate(identity, msg.sender, delegateType, delegate_);
    }

    function revokeDelegateSigned(address identity, uint8 sigV, bytes32 sigR, bytes32 sigS, bytes32 delegateType, address delegate_) public {
        bytes32 hash = keccak256(abi.encodePacked(bytes1(0x19), bytes1(0x00), address(this), nonce[identityOwner(identity)], identity, "revokeDelegate", delegateType, delegate_));
        revokeDelegate(identity, checkSignature(identity, sigV, sigR, sigS, hash), delegateType, delegate_);
    }

    function setAttribute(address identity, address actor, bytes32 name, bytes memory value, uint validity) public onlyOwner(identity, actor) {
        emit DIDAttributeChanged(identity, name, value, block.timestamp + validity, changed[identity]);
        changed[identity] = block.number;
    }

    function setAttribute(address identity, bytes32 name, bytes memory value, uint validity) public {
        setAttribute(identity, msg.sender, name, value, validity);
    }

    function setAttributeSigned(address identity, uint8 sigV, bytes32 sigR, bytes32 sigS, bytes32 name, bytes memory value, uint validity) public {
        bytes32 hash = keccak256(abi.encodePacked(bytes1(0x19), bytes1(0x00), address(this), nonce[identityOwner(identity)], identity, "setAttribute", name, value, validity));
        setAttribute(identity, checkSignature(identity, sigV, sigR, sigS, hash), name, value, validity);
    }

    function revokeAttribute(address identity, address actor, bytes32 name, bytes memory value) public onlyOwner(identity, actor) {
        emit DIDAttributeChanged(identity, name, value, 0, changed[identity]);
        changed[identity] = block.number;
    }

    function revokeAttribute(address identity, bytes32 name, bytes memory value) public {
        revokeAttribute(identity, msg.sender, name, value);
    }

    function revokeAttributeSigned(address identity, uint8 sigV, bytes32 sigR, bytes32 sigS, bytes32 name, bytes memory value) public {
        bytes32 hash = keccak256(abi.encodePacked(bytes1(0x19), bytes1(0x00), address(this), nonce[identityOwner(identity)], identity, "revokeAttribute", name, value));
        revokeAttribute(identity, checkSignature(identity, sigV, sigR, sigS, hash), name, value);
    }
}
