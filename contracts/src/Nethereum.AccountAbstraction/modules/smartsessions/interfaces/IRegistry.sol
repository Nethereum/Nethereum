// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

type ModuleType is uint256;

interface IRegistry {
    event NewTrustedAttesters(address indexed smartAccount);
    /*´:°•.°+.*•´.*:˚.°*.˚•´.°:°•.°•.*•´.*:˚.°*.˚•´.°:°•.°+.*•´.*:*/
    /*          Check with Registry internal attesters            */
    /*.•°:°.´+˚.*°.˚:*.´•*.+°.•°:´*.´•*.•°.•°:°.´:•˚°.*°.˚:*.´+°.•*/

    function check(address module) external view;

    function checkForAccount(address smartAccount, address module) external view;

    function check(address module, ModuleType moduleType) external view;

    function checkForAccount(address smartAccount, address module, ModuleType moduleType) external view;

    /**
     * Allows Smart Accounts - the end users of the registry - to appoint
     * one or many attesters as trusted.
     * @dev this function reverts, if address(0), or duplicates are provided in attesters[]
     *
     * @param threshold The minimum number of attestations required for a module
     *                  to be considered secure.
     * @param attesters The addresses of the attesters to be trusted.
     */
    function trustAttesters(uint8 threshold, address[] calldata attesters) external;

    /*´:°•.°+.*•´.*:˚.°*.˚•´.°:°•.°•.*•´.*:˚.°*.˚•´.°:°•.°+.*•´.*:*/
    /*              Check with external attester(s)               */
    /*.•°:°.´+˚.*°.˚:*.´•*.+°.•°:´*.´•*.•°.•°:°.´:•˚°.*°.˚:*.´+°.•*/

    function check(address module, address[] calldata attesters, uint256 threshold) external view;

    function check(
        address module,
        ModuleType moduleType,
        address[] calldata attesters,
        uint256 threshold
    )
        external
        view;
}
