// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

type ModuleType is uint256;

/// SmartSession's DataTypes.sol hardcodes the Rhinestone Module Registry at
/// 0x000000000069E2a187AEFFb852bF3cCdC95151B2 and calls it via a Solidity interface reference for
/// every policy/session-validator it enables when `useRegistry` is true (the ONLY setting the module's
/// public `enableSessions`/`onInstall(ENABLE mode)` entry points use). Solidity emits an implicit
/// `extcodesize` guard ahead of that kind of call and reverts (with no return data) if the target has
/// no code - there is no real registry deployed on a devchain, so every session enable reverts unless
/// something is deployed at that exact address.
///
/// This is that something for the AA example only: a permissive stand-in, deployed normally and then
/// copied onto the registry's fixed address via `DevChainNode.SetCodeAsync` (see
/// `HostBootstrap.StartAsync`) - mirroring Rhinestone's own `MockRegistry` +
/// `vm.etch(REGISTRY_ADDR, ...)` pattern from their SmartSession Foundry tests
/// (contracts/lib/smartsessions and contracts/test/modules/SmartSessionsIntegration.t.sol). It
/// approves every module unconditionally - no attestation checking - which is appropriate ONLY for a
/// local devchain demo, never a real deployment.
contract SmartSessionRegistryStub {
    function check(address) external pure { }
    function checkForAccount(address, address) external pure { }
    function check(address, ModuleType) external pure { }
    function checkForAccount(address, address, ModuleType) external pure { }
    function trustAttesters(uint8, address[] calldata) external pure { }
    function check(address, address[] calldata, uint256) external pure { }
    function check(address, ModuleType, address[] calldata, uint256) external pure { }
}
