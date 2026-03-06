// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

import { System } from "@latticexyz/world/src/System.sol";
import { AppConfig } from "../codegen/tables/AppConfig.sol";
import { Permission } from "../codegen/tables/Permission.sol";

contract AdminSystem is System {
    bytes32 public constant ADMIN_ROLE = keccak256("ADMIN");
    bytes32 public constant MODERATOR_ROLE = keccak256("MODERATOR");
    bytes32 public constant GLOBAL_RESOURCE = bytes32(0);

    error NotAdmin();
    error AppPaused();
    error AlreadyInitialized();

    modifier onlyAdmin() {
        if (!_isAdmin(_msgSender())) {
            revert NotAdmin();
        }
        _;
    }

    modifier whenNotPaused() {
        if (AppConfig.getPaused()) {
            revert AppPaused();
        }
        _;
    }

    function initialize(string memory appName, string memory version, uint256 maxUsers) public {
        if (bytes(AppConfig.getAppName()).length > 0) {
            revert AlreadyInitialized();
        }

        address admin = _msgSender();

        AppConfig.set(appName, version, admin, false, maxUsers);

        Permission.set(GLOBAL_RESOURCE, ADMIN_ROLE, admin, true, admin, block.timestamp);
    }

    function pause() public onlyAdmin {
        AppConfig.setPaused(true);
    }

    function unpause() public onlyAdmin {
        AppConfig.setPaused(false);
    }

    function setMaxUsers(uint256 maxUsers) public onlyAdmin {
        AppConfig.setMaxUsers(maxUsers);
    }

    function updateVersion(string memory version) public onlyAdmin {
        AppConfig.setVersion(version);
    }

    function grantRole(bytes32 resource, bytes32 role, address account) public onlyAdmin {
        Permission.set(resource, role, account, true, _msgSender(), block.timestamp);
    }

    function revokeRole(bytes32 resource, bytes32 role, address account) public onlyAdmin {
        Permission.setGranted(resource, role, account, false);
    }

    function hasRole(bytes32 resource, bytes32 role, address account) public view returns (bool) {
        return Permission.getGranted(resource, role, account);
    }

    function transferAdmin(address newAdmin) public onlyAdmin {
        address currentAdmin = _msgSender();

        Permission.setGranted(GLOBAL_RESOURCE, ADMIN_ROLE, currentAdmin, false);

        Permission.set(GLOBAL_RESOURCE, ADMIN_ROLE, newAdmin, true, currentAdmin, block.timestamp);

        AppConfig.setAdmin(newAdmin);
    }

    function _isAdmin(address account) internal view returns (bool) {
        return Permission.getGranted(GLOBAL_RESOURCE, ADMIN_ROLE, account);
    }
}
