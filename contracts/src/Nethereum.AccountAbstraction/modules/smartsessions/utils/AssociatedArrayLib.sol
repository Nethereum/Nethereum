// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/**
 * ERC-4337 / ERC-7562 Compatible array lib.
 *   This array can be used as mapping value in mappings such as (address account => Bytes32Array array)
 *   Array size should not exceed 128.
 */
library AssociatedArrayLib {
    using AssociatedArrayLib for *;

    error AssociatedArray_OutOfBounds(uint256 index);

    struct Array {
        uint256 _spacer;
    }

    function _slot(Array storage s, address account) private pure returns (bytes32 __slot) {
        assembly {
            mstore(0x00, account)
            mstore(0x20, s.slot)
            __slot := keccak256(0x00, 0x40)
        }
    }

    function _length(Array storage s, address account) private view returns (uint256 __length) {
        bytes32 slot = _slot(s, account);
        assembly {
            __length := sload(slot)
        }
    }

    function _get(Array storage s, address account, uint256 index) private view returns (bytes32 value) {
        return _get(_slot(s, account), index);
    }

    function _get(bytes32 slot, uint256 index) private view returns (bytes32 value) {
        assembly {
            //if (index >= _length(s, account)) revert AssociatedArray_OutOfBounds(index);
            if iszero(lt(index, sload(slot))) {
                mstore(0, 0x8277484f) // `AssociatedArray_OutOfBounds(uint256)`
                mstore(0x20, index)
                revert(0x1c, 0x24)
            }
            value := sload(add(slot, add(index, 1)))
        }
    }

    function _getAll(Array storage s, address account) private view returns (bytes32[] memory values) {
        bytes32 slot = _slot(s, account);
        uint256 __length;
        assembly {
            __length := sload(slot)
        }
        values = new bytes32[](__length);
        for (uint256 i; i < __length; i++) {
            values[i] = _get(slot, i);
        }
    }

    // inefficient. complexity = O(n)
    // use with caution
    // in case of large arrays, consider using EnumerableSet4337 instead
    function _contains(Array storage s, address account, bytes32 value) private view returns (bool) {
        bytes32 slot = _slot(s, account);
        uint256 __length;
        assembly {
            __length := sload(slot)
        }
        for (uint256 i; i < __length; i++) {
            if (_get(slot, i) == value) {
                return true;
            }
        }
        return false;
    }

    function _set(Array storage s, address account, uint256 index, bytes32 value) private {
        _set(_slot(s, account), index, value);
    }

    function _set(bytes32 slot, uint256 index, bytes32 value) private {
        assembly {
            //if (index >= _length(s, account)) revert AssociatedArray_OutOfBounds(index);
            if iszero(lt(index, sload(slot))) {
                mstore(0, 0x8277484f) // `AssociatedArray_OutOfBounds(uint256)`
                mstore(0x20, index)
                revert(0x1c, 0x24)
            }
            sstore(add(slot, add(index, 1)), value)
        }
    }

    function _push(Array storage s, address account, bytes32 value) private {
        bytes32 slot = _slot(s, account);
        assembly {
            // load length (stored @ slot) => this would be the index of a new element
            let index := sload(slot)
            if gt(index, 127) {
                mstore(0, 0x8277484f) // `AssociatedArray_OutOfBounds(uint256)`
                mstore(0x20, index)
                revert(0x1c, 0x24)
            }
            sstore(add(slot, add(index, 1)), value) // store at (slot+index+1) => 0th element is stored at slot+1
            sstore(slot, add(index, 1)) // increment length by 1
        }
    }

    function _pop(Array storage s, address account) private {
        bytes32 slot = _slot(s, account);
        uint256 __length;
        assembly {
            __length := sload(slot)
        }
        if (__length == 0) return;
        _set(slot, __length - 1, 0);
        assembly {
            sstore(slot, sub(__length, 1))
        }
    }

    function _remove(Array storage s, address account, uint256 index) private {
        bytes32 slot = _slot(s, account);
        uint256 __length;
        assembly {
            __length := sload(slot)
            if iszero(lt(index, __length)) {
                mstore(0, 0x8277484f) // `AssociatedArray_OutOfBounds(uint256)`
                mstore(0x20, index)
                revert(0x1c, 0x24)
            }
        }
        _set(slot, index, _get(s, account, __length - 1));

        assembly {
            // clear the last slot
            // this is the 'unchecked' version of _set(slot, __length - 1, 0)
            // as we use length-1 as index, so the check is excessive.
            // also removes extra -1 and +1 operations
            sstore(add(slot, __length), 0)
            // store new length
            sstore(slot, sub(__length, 1))
        }
    }

    struct Bytes32Array {
        Array _inner;
    }

    function length(Bytes32Array storage s, address account) internal view returns (uint256) {
        return _length(s._inner, account);
    }

    function get(Bytes32Array storage s, address account, uint256 index) internal view returns (bytes32) {
        return _get(s._inner, account, index);
    }

    function getAll(Bytes32Array storage s, address account) internal view returns (bytes32[] memory) {
        return _getAll(s._inner, account);
    }

    function contains(Bytes32Array storage s, address account, bytes32 value) internal view returns (bool) {
        return _contains(s._inner, account, value);
    }

    function add(Bytes32Array storage s, address account, bytes32 value) internal {
        if (!_contains(s._inner, account, value)) {
            _push(s._inner, account, value);
        }
    }

    function set(Bytes32Array storage s, address account, uint256 index, bytes32 value) internal {
        _set(s._inner, account, index, value);
    }

    function push(Bytes32Array storage s, address account, bytes32 value) internal {
        _push(s._inner, account, value);
    }

    function pop(Bytes32Array storage s, address account) internal {
        _pop(s._inner, account);
    }

    function remove(Bytes32Array storage s, address account, uint256 index) internal {
        _remove(s._inner, account, index);
    }

    struct AddressArray {
        Array _inner;
    }

    function length(AddressArray storage s, address account) internal view returns (uint256) {
        return _length(s._inner, account);
    }

    function get(AddressArray storage s, address account, uint256 index) internal view returns (address) {
        return address(uint160(uint256(_get(s._inner, account, index))));
    }

    function getAll(AddressArray storage s, address account) internal view returns (address[] memory) {
        bytes32[] memory bytes32Array = _getAll(s._inner, account);
        address[] memory addressArray;

        /// @solidity memory-safe-assembly
        assembly {
            addressArray := bytes32Array
        }
        return addressArray;
    }

    function contains(AddressArray storage s, address account, address value) internal view returns (bool) {
        return _contains(s._inner, account, bytes32(uint256(uint160(value))));
    }

    function add(AddressArray storage s, address account, address value) internal {
        if (!_contains(s._inner, account, bytes32(uint256(uint160(value))))) {
            _push(s._inner, account, bytes32(uint256(uint160(value))));
        }
    }

    function set(AddressArray storage s, address account, uint256 index, address value) internal {
        _set(s._inner, account, index, bytes32(uint256(uint160(value))));
    }

    function push(AddressArray storage s, address account, address value) internal {
        _push(s._inner, account, bytes32(uint256(uint160(value))));
    }

    function pop(AddressArray storage s, address account) internal {
        _pop(s._inner, account);
    }

    function remove(AddressArray storage s, address account, uint256 index) internal {
        _remove(s._inner, account, index);
    }

    struct UintArray {
        Array _inner;
    }

    function length(UintArray storage s, address account) internal view returns (uint256) {
        return _length(s._inner, account);
    }

    function get(UintArray storage s, address account, uint256 index) internal view returns (uint256) {
        return uint256(_get(s._inner, account, index));
    }

    function getAll(UintArray storage s, address account) internal view returns (uint256[] memory) {
        bytes32[] memory bytes32Array = _getAll(s._inner, account);
        uint256[] memory uintArray;

        /// @solidity memory-safe-assembly
        assembly {
            uintArray := bytes32Array
        }
        return uintArray;
    }

    function contains(UintArray storage s, address account, uint256 value) internal view returns (bool) {
        return _contains(s._inner, account, bytes32(value));
    }

    function add(UintArray storage s, address account, uint256 value) internal {
        if (!_contains(s._inner, account, bytes32(value))) {
            _push(s._inner, account, bytes32(value));
        }
    }

    function set(UintArray storage s, address account, uint256 index, uint256 value) internal {
        _set(s._inner, account, index, bytes32(value));
    }

    function push(UintArray storage s, address account, uint256 value) internal {
        _push(s._inner, account, bytes32(value));
    }

    function pop(UintArray storage s, address account) internal {
        _pop(s._inner, account);
    }

    function remove(UintArray storage s, address account, uint256 index) internal {
        _remove(s._inner, account, index);
    }
}
