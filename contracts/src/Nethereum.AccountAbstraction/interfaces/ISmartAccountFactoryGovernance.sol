// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

interface ISmartAccountFactoryGovernance {
    // ========== EVENTS ==========

    event AccountCreated(
        address indexed account,
        address indexed owner,
        bytes32 indexed salt,
        bytes32[] moduleIds
    );
    event ModuleRegistered(bytes32 indexed moduleId, address indexed moduleAddress);
    event ModuleUnregistered(bytes32 indexed moduleId);
    event AdminsUpdated(address[] newAdmins, uint256 newThreshold, uint256 nonce);

    // ========== ACCOUNT FUNCTIONS ==========

    function createAccount(
        address owner,
        bytes32 salt,
        bytes32[] calldata moduleIds
    ) external returns (address account);

    function createAccountIfNeeded(
        address owner,
        bytes32 salt,
        bytes32[] calldata moduleIds
    ) external returns (address account);

    function getAddress(
        address owner,
        bytes32 salt,
        bytes32[] calldata moduleIds
    ) external view returns (address);

    function isDeployed(
        address owner,
        bytes32 salt,
        bytes32[] calldata moduleIds
    ) external view returns (bool);

    // ========== MODULE FUNCTIONS (multi-sig required) ==========

    function registerModule(
        bytes32 moduleId,
        address moduleAddress,
        uint256 deadline,
        bytes[] calldata signatures
    ) external;

    function unregisterModule(
        bytes32 moduleId,
        uint256 deadline,
        bytes[] calldata signatures
    ) external;

    function getModuleAddress(bytes32 moduleId) external view returns (address);
    function getRegisteredModules() external view returns (bytes32[] memory);
    function getRegisteredModuleCount() external view returns (uint256);

    // ========== ADMIN FUNCTIONS ==========

    function updateAdmins(
        address[] calldata newAdmins,
        uint256 newThreshold,
        uint256 deadline,
        bytes[] calldata signatures
    ) external;

    function getAdmins() external view returns (address[] memory);
    function getAdminCount() external view returns (uint256);
    function isAdmin(address account) external view returns (bool);
    function threshold() external view returns (uint256);
    function nonce() external view returns (uint256);

    // ========== SIGNATURE VALIDATION ==========

    function validateSignatures(
        bytes32 digest,
        bytes[] calldata signatures
    ) external view returns (bool);

    function getDomainSeparator() external view returns (bytes32);

    // ========== CONSTANTS ==========

    function MAX_ADMINS() external pure returns (uint256);
    function implementation() external view returns (address);
    function entryPoint() external view returns (address);
    function accountRegistry() external view returns (address);
}
