// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

import { System } from "@latticexyz/world/src/System.sol";
import { UserItem } from "../codegen/tables/UserItem.sol";
import { UserProfile } from "../codegen/tables/UserProfile.sol";

contract InventorySystem is System {
    error ProfileNotActive();
    error InsufficientQuantity();
    error InvalidQuantity();

    event ItemAdded(address indexed user, uint256 indexed itemId, uint256 quantity);
    event ItemRemoved(address indexed user, uint256 indexed itemId, uint256 quantity);
    event ItemTransferred(address indexed from, address indexed to, uint256 indexed itemId, uint256 quantity);

    modifier onlyActiveProfile() {
        if (!UserProfile.getIsActive(_msgSender())) {
            revert ProfileNotActive();
        }
        _;
    }

    function addItem(uint256 itemId, uint256 quantity) public onlyActiveProfile {
        if (quantity == 0) {
            revert InvalidQuantity();
        }

        address user = _msgSender();
        uint256 currentQuantity = UserItem.getQuantity(user, itemId);

        if (currentQuantity == 0) {
            UserItem.set(user, itemId, quantity, block.timestamp);
        } else {
            UserItem.setQuantity(user, itemId, currentQuantity + quantity);
        }

        emit ItemAdded(user, itemId, quantity);
    }

    function removeItem(uint256 itemId, uint256 quantity) public onlyActiveProfile {
        if (quantity == 0) {
            revert InvalidQuantity();
        }

        address user = _msgSender();
        uint256 currentQuantity = UserItem.getQuantity(user, itemId);

        if (currentQuantity < quantity) {
            revert InsufficientQuantity();
        }

        if (currentQuantity == quantity) {
            UserItem.deleteRecord(user, itemId);
        } else {
            UserItem.setQuantity(user, itemId, currentQuantity - quantity);
        }

        emit ItemRemoved(user, itemId, quantity);
    }

    function transferItem(address to, uint256 itemId, uint256 quantity) public onlyActiveProfile {
        if (quantity == 0) {
            revert InvalidQuantity();
        }

        if (!UserProfile.getIsActive(to)) {
            revert ProfileNotActive();
        }

        address from = _msgSender();
        uint256 fromQuantity = UserItem.getQuantity(from, itemId);

        if (fromQuantity < quantity) {
            revert InsufficientQuantity();
        }

        // Remove from sender
        if (fromQuantity == quantity) {
            UserItem.deleteRecord(from, itemId);
        } else {
            UserItem.setQuantity(from, itemId, fromQuantity - quantity);
        }

        // Add to recipient
        uint256 toQuantity = UserItem.getQuantity(to, itemId);
        if (toQuantity == 0) {
            UserItem.set(to, itemId, quantity, block.timestamp);
        } else {
            UserItem.setQuantity(to, itemId, toQuantity + quantity);
        }

        emit ItemTransferred(from, to, itemId, quantity);
    }

    function getItemQuantity(address user, uint256 itemId) public view returns (uint256) {
        return UserItem.getQuantity(user, itemId);
    }
}
