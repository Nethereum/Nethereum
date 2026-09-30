// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/// A minimal booking registry for the Account Abstraction example: each numbered slot can be
/// booked once. Demonstrates the two revert flavours a dApp surfaces through AA:
///  - a typed custom error carrying data (SlotAlreadyBooked), and
///  - a require with a string reason (only the owner may release a slot).
contract BookingRegistry {
    address public owner;
    mapping(uint256 => address) public guestOf;

    /// Thrown when booking a slot that another guest already holds.
    error SlotAlreadyBooked(uint256 slot, address guest);

    event SlotBooked(uint256 indexed slot, address indexed guest);
    event SlotReleased(uint256 indexed slot);

    constructor() {
        owner = msg.sender;
    }

    function book(uint256 slot) external {
        address guest = guestOf[slot];
        if (guest != address(0)) revert SlotAlreadyBooked(slot, guest);
        guestOf[slot] = msg.sender;
        emit SlotBooked(slot, msg.sender);
    }

    function release(uint256 slot) external {
        require(msg.sender == owner, "only owner can release");
        guestOf[slot] = address(0);
        emit SlotReleased(slot);
    }
}
