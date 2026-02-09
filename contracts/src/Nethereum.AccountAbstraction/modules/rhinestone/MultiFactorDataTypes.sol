// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import { FlatBytesLib } from "flatbytes/BytesLib.sol";

// Validator ID
type ValidatorId is bytes12;

// Validator Data
// This struct is used when configuring a subValidator and when validating signatures
struct Validator {
    bytes32 packedValidatorAndId; // abi.encodePacked(bytes12(id), address(validator))
    bytes data; // either subValidator config data or signature
}

// The data to be sent to stateless validator
struct SubValidatorConfig {
    bytes data;
}

// MFA Configuration
struct MFAConfig {
    uint8 threshold; // number of validators required to validate a signature
    uint8 validationLength; // number of validators required to validate a signature
    uint128 iteration; // iteration number
}

// Iterative SubValidator Record
// a ValidatorId is used so that validators can be used multiple times
// ValidatorId => account => SubValidatorConfig
struct IterativeSubvalidatorRecord {
    mapping(ValidatorId id => mapping(address account => FlatBytesLib.Bytes config)) subValidators;
}
