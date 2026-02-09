// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

interface IAccountRegistry {
    function isActive(address account) external view returns (bool);
}
