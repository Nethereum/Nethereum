// SPDX-License-Identifier: AGPL-3.0-only
pragma solidity ^0.8.25;

import { Validator, ValidatorId } from "./MultiFactorDataTypes.sol";

/**
 * @title MultiFactorLib
 * @dev Library for encoding and decoding data for MultiFactor
 * @author zeroknots.eth | rhinestone.wtf
 */
library MultiFactorLib {
    /**
     * Decodes a bytes array into a list of validators
     *
     * @param data Encoded data
     *
     * @return validators List of validators
     */
    function decode(bytes calldata data) internal pure returns (Validator[] calldata validators) {
        // (Validator[]) = abi.decode(data,(Validator[])
        assembly ("memory-safe") {
            let offset := data.offset
            let baseOffset := offset
            let dataPointer := add(baseOffset, calldataload(offset))

            validators.offset := add(dataPointer, 32)
            validators.length := calldataload(dataPointer)
            offset := add(offset, 32)

            dataPointer := add(baseOffset, calldataload(offset))
        }
    }

    /**
     * Packs a validator and an id into a bytes32
     *
     * @param subValidator SubValidator address
     * @param id Validator ID
     *
     * @return _packed Packed data
     */
    function pack(address subValidator, ValidatorId id) internal pure returns (bytes32 _packed) {
        // pack the validator and id
        _packed = bytes32(abi.encodePacked(ValidatorId.unwrap(id), subValidator));
    }

    /**
     * Unpacks a bytes32 into a validator and an id
     *
     * @param packed Packed data
     *
     * @return subValidator SubValidator address
     * @return id Validator ID
     */
    function unpack(bytes32 packed) internal pure returns (address subValidator, ValidatorId id) {
        // packed = abi.encodePacked(id, subValidator)
        assembly {
            // unpack the validator
            subValidator := packed
            // unpack the id
            id := shl(0, packed)
        }
    }
}
