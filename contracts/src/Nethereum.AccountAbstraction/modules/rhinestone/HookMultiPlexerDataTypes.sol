// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

// Hook types
enum HookType {
    GLOBAL,
    DELEGATECALL,
    VALUE,
    SIG,
    TARGET_SIG
}

// Hook initialization data for sig hooks
struct SigHookInit {
    bytes4 sig;
    address[] subHooks;
}

struct HookAndContext {
    address hook;
    bytes context;
}

struct SignatureHooks {
    bytes4[] allSigs;
    mapping(bytes4 => address[]) sigHooks;
}

struct Config {
    bool initialized;
    mapping(HookType hookType => address[]) hooks;
    mapping(HookType hookType => SignatureHooks) sigHooks;
}
