// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

// ModeCode layout (32 bytes):
// [1 byte CallType][1 byte ExecType][4 bytes unused][4 bytes ModeSelector][22 bytes ModePayload]

// Execution mode encoded as bytes32
type ModeCode is bytes32;

// Call type - how the execution is performed
type CallType is bytes1;

// Execution type - how failures are handled
type ExecType is bytes1;

// Mode selector for extended functionality
type ModeSelector is bytes4;

// Mode payload for additional data
type ModePayload is bytes22;

// Call type constants
CallType constant CALLTYPE_SINGLE = CallType.wrap(0x00);
CallType constant CALLTYPE_BATCH = CallType.wrap(0x01);
CallType constant CALLTYPE_STATIC = CallType.wrap(0xFE);
CallType constant CALLTYPE_DELEGATECALL = CallType.wrap(0xFF);

// Execution type constants
ExecType constant EXECTYPE_DEFAULT = ExecType.wrap(0x00);
ExecType constant EXECTYPE_TRY = ExecType.wrap(0x01);

// Mode selector for default execution (no special handling)
ModeSelector constant MODE_DEFAULT = ModeSelector.wrap(0x00000000);

// Mode payload for default execution
ModePayload constant PAYLOAD_DEFAULT = ModePayload.wrap(bytes22(0));

library ModeLib {
    function decode(ModeCode mode)
        internal
        pure
        returns (CallType callType, ExecType execType, ModeSelector selector, ModePayload payload)
    {
        bytes32 raw = ModeCode.unwrap(mode);

        // Extract components using bit operations
        callType = CallType.wrap(bytes1(raw));
        execType = ExecType.wrap(bytes1(raw << 8));
        selector = ModeSelector.wrap(bytes4(raw << 48));
        payload = ModePayload.wrap(bytes22(raw << 80));
    }

    function encode(
        CallType callType,
        ExecType execType,
        ModeSelector selector,
        ModePayload payload
    ) internal pure returns (ModeCode) {
        return ModeCode.wrap(
            bytes32(
                abi.encodePacked(
                    callType,
                    execType,
                    bytes4(0), // unused
                    selector,
                    payload
                )
            )
        );
    }

    function encodeSimpleSingle() internal pure returns (ModeCode) {
        return encode(CALLTYPE_SINGLE, EXECTYPE_DEFAULT, MODE_DEFAULT, PAYLOAD_DEFAULT);
    }

    function encodeSimpleBatch() internal pure returns (ModeCode) {
        return encode(CALLTYPE_BATCH, EXECTYPE_DEFAULT, MODE_DEFAULT, PAYLOAD_DEFAULT);
    }

    function getCallType(ModeCode mode) internal pure returns (CallType) {
        return CallType.wrap(bytes1(ModeCode.unwrap(mode)));
    }

    function getExecType(ModeCode mode) internal pure returns (ExecType) {
        return ExecType.wrap(bytes1(ModeCode.unwrap(mode) << 8));
    }
}
