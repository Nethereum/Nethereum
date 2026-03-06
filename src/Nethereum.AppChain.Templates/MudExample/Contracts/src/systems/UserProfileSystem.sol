// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

import { System } from "@latticexyz/world/src/System.sol";
import { UserProfile } from "../codegen/tables/UserProfile.sol";

contract UserProfileSystem is System {
    error ProfileAlreadyExists();
    error ProfileNotFound();
    error NotProfileOwner();

    function createProfile(string memory username, string memory bio) public {
        address user = _msgSender();

        if (UserProfile.getCreatedAt(user) != 0) {
            revert ProfileAlreadyExists();
        }

        UserProfile.set(
            user,
            username,
            bio,
            block.timestamp,
            block.timestamp,
            true
        );
    }

    function updateProfile(string memory username, string memory bio) public {
        address user = _msgSender();

        if (UserProfile.getCreatedAt(user) == 0) {
            revert ProfileNotFound();
        }

        UserProfile.setUsername(user, username);
        UserProfile.setBio(user, bio);
        UserProfile.setUpdatedAt(user, block.timestamp);
    }

    function deactivateProfile() public {
        address user = _msgSender();

        if (UserProfile.getCreatedAt(user) == 0) {
            revert ProfileNotFound();
        }

        UserProfile.setIsActive(user, false);
        UserProfile.setUpdatedAt(user, block.timestamp);
    }

    function reactivateProfile() public {
        address user = _msgSender();

        if (UserProfile.getCreatedAt(user) == 0) {
            revert ProfileNotFound();
        }

        UserProfile.setIsActive(user, true);
        UserProfile.setUpdatedAt(user, block.timestamp);
    }
}
