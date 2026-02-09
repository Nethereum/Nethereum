// SPDX-License-Identifier: MIT

pragma solidity ^0.8.20;

import "./AssociatedArrayLib.sol";

/**
 * Fork of OZ's EnumerableSet that makes all storage access ERC-4337 compliant via associated storage
 * @author zeroknots.eth (rhinestone)
 */
library EnumerableSet {
    using AssociatedArrayLib for AssociatedArrayLib.Bytes32Array;
    // To implement this library for multiple types with as little code
    // repetition as possible, we write it in terms of a generic Set type with
    // bytes32 values.
    // The Set implementation uses private functions, and user-facing
    // implementations (such as AddressSet) are just wrappers around the
    // underlying Set.
    // This means that we can only create new EnumerableSets for types that fit
    // in bytes32.

    struct Set {
        // Storage of set values
        AssociatedArrayLib.Bytes32Array _values;
        // Position is the index of the value in the `values` array plus 1.
        // Position 0 is used to mean a value is not in the set.
        mapping(bytes32 value => mapping(address account => uint256)) _positions;
    }

    /**
     * @dev Add a value to a set. O(1).
     *
     * Returns true if the value was added to the set, that is if it was not
     * already present.
     */
    function _add(Set storage set, address account, bytes32 value) private returns (bool) {
        if (!_contains(set, account, value)) {
            set._values.push(account, value);
            // The value is stored at length-1, but we add 1 to all indexes
            // and use 0 as a sentinel value
            set._positions[value][account] = set._values.length(account);
            return true;
        } else {
            return false;
        }
    }

    /**
     * @dev Removes a value from a set. O(1).
     *
     * Returns true if the value was removed from the set, that is if it was
     * present.
     */
    function _remove(Set storage set, address account, bytes32 value) private returns (bool) {
        // We cache the value's position to prevent multiple reads from the same storage slot
        uint256 position = set._positions[value][account];

        if (position != 0) {
            // Equivalent to contains(set, value)
            // To delete an element from the _values array in O(1), we swap the element to delete with the last one in
            // the array, and then remove the last element (sometimes called as 'swap and pop').
            // This modifies the order of the array, as noted in {at}.

            uint256 valueIndex = position - 1;
            uint256 lastIndex = set._values.length(account) - 1;

            if (valueIndex != lastIndex) {
                bytes32 lastValue = set._values.get(account, lastIndex);

                // Move the lastValue to the index where the value to delete is
                set._values.set(account, valueIndex, lastValue);
                // Update the tracked position of the lastValue (that was just moved)
                set._positions[lastValue][account] = position;
            }

            // Delete the slot where the moved value was stored
            set._values.pop(account);

            // Delete the tracked position for the deleted slot
            delete set._positions[value][account];

            return true;
        } else {
            return false;
        }
    }

    function _removeAll(Set storage set, address account) internal {
        // get length of the array
        uint256 len = _length(set, account);
        for (uint256 i = 1; i <= len; i++) {
            // get last value
            bytes32 value = _at(set, account, len - i);
            _remove(set, account, value);
        }
    }

    /**
     * @dev Returns true if the value is in the set. O(1).
     */
    function _contains(Set storage set, address account, bytes32 value) private view returns (bool) {
        return set._positions[value][account] != 0;
    }

    /**
     * @dev Returns the number of values on the set. O(1).
     */
    function _length(Set storage set, address account) private view returns (uint256) {
        return set._values.length(account);
    }

    /**
     * @dev Returns the value stored at position `index` in the set. O(1).
     *
     * Note that there are no guarantees on the ordering of values inside the
     * array, and it may change when more values are added or removed.
     *
     * Requirements:
     *
     * - `index` must be strictly less than {length}.
     */
    function _at(Set storage set, address account, uint256 index) private view returns (bytes32) {
        return set._values.get(account, index);
    }

    /**
     * @dev Return the entire set in an array
     *
     * WARNING: This operation will copy the entire storage to memory, which can be quite expensive. This is designed
     * to mostly be used by view accessors that are queried without any gas fees. Developers should keep in mind that
     * this function has an unbounded cost, and using it as part of a state-changing function may render the function
     * uncallable if the set grows to a point where copying to memory consumes too much gas to fit in a block.
     */
    function _values(Set storage set, address account) private view returns (bytes32[] memory) {
        return set._values.getAll(account);
    }

    // Bytes32Set

    struct Bytes32Set {
        Set _inner;
    }

    /**
     * @dev Add a value to a set. O(1).
     *
     * Returns true if the value was added to the set, that is if it was not
     * already present.
     */
    function add(Bytes32Set storage set, address account, bytes32 value) internal returns (bool) {
        return _add(set._inner, account, value);
    }

    /**
     * @dev Removes a value from a set. O(1).
     *
     * Returns true if the value was removed from the set, that is if it was
     * present.
     */
    function remove(Bytes32Set storage set, address account, bytes32 value) internal returns (bool) {
        return _remove(set._inner, account, value);
    }

    function removeAll(Bytes32Set storage set, address account) internal {
        return _removeAll(set._inner, account);
    }

    /**
     * @dev Returns true if the value is in the set. O(1).
     */
    function contains(Bytes32Set storage set, address account, bytes32 value) internal view returns (bool) {
        return _contains(set._inner, account, value);
    }

    /**
     * @dev Returns the number of values in the set. O(1).
     */
    function length(Bytes32Set storage set, address account) internal view returns (uint256) {
        return _length(set._inner, account);
    }

    /**
     * @dev Returns the value stored at position `index` in the set. O(1).
     *
     * Note that there are no guarantees on the ordering of values inside the
     * array, and it may change when more values are added or removed.
     *
     * Requirements:
     *
     * - `index` must be strictly less than {length}.
     */
    function at(Bytes32Set storage set, address account, uint256 index) internal view returns (bytes32) {
        return _at(set._inner, account, index);
    }

    /**
     * @dev Return the entire set in an array
     *
     * WARNING: This operation will copy the entire storage to memory, which can be quite expensive. This is designed
     * to mostly be used by view accessors that are queried without any gas fees. Developers should keep in mind that
     * this function has an unbounded cost, and using it as part of a state-changing function may render the function
     * uncallable if the set grows to a point where copying to memory consumes too much gas to fit in a block.
     */
    function values(Bytes32Set storage set, address account) internal view returns (bytes32[] memory) {
        bytes32[] memory store = _values(set._inner, account);
        bytes32[] memory result;

        /// @solidity memory-safe-assembly
        assembly {
            result := store
        }

        return result;
    }

    // AddressSet

    struct AddressSet {
        Set _inner;
    }

    /**
     * @dev Add a value to a set. O(1).
     *
     * Returns true if the value was added to the set, that is if it was not
     * already present.
     */
    function add(AddressSet storage set, address account, address value) internal returns (bool) {
        return _add(set._inner, account, bytes32(uint256(uint160(value))));
    }

    /**
     * @dev Removes a value from a set. O(1).
     *
     * Returns true if the value was removed from the set, that is if it was
     * present.
     */
    function remove(AddressSet storage set, address account, address value) internal returns (bool) {
        return _remove(set._inner, account, bytes32(uint256(uint160(value))));
    }

    function removeAll(AddressSet storage set, address account) internal {
        return _removeAll(set._inner, account);
    }

    /**
     * @dev Returns true if the value is in the set. O(1).
     */
    function contains(AddressSet storage set, address account, address value) internal view returns (bool) {
        return _contains(set._inner, account, bytes32(uint256(uint160(value))));
    }

    /**
     * @dev Returns the number of values in the set. O(1).
     */
    function length(AddressSet storage set, address account) internal view returns (uint256) {
        return _length(set._inner, account);
    }

    /**
     * @dev Returns the value stored at position `index` in the set. O(1).
     *
     * Note that there are no guarantees on the ordering of values inside the
     * array, and it may change when more values are added or removed.
     *
     * Requirements:
     *
     * - `index` must be strictly less than {length}.
     */
    function at(AddressSet storage set, address account, uint256 index) internal view returns (address) {
        return address(uint160(uint256(_at(set._inner, account, index))));
    }

    /**
     * @dev Return the entire set in an array
     *
     * WARNING: This operation will copy the entire storage to memory, which can be quite expensive. This is designed
     * to mostly be used by view accessors that are queried without any gas fees. Developers should keep in mind that
     * this function has an unbounded cost, and using it as part of a state-changing function may render the function
     * uncallable if the set grows to a point where copying to memory consumes too much gas to fit in a block.
     */
    function values(AddressSet storage set, address account) internal view returns (address[] memory) {
        bytes32[] memory store = _values(set._inner, account);
        address[] memory result;

        /// @solidity memory-safe-assembly
        assembly {
            result := store
        }

        return result;
    }

    // UintSet

    struct UintSet {
        Set _inner;
    }

    /**
     * @dev Add a value to a set. O(1).
     *
     * Returns true if the value was added to the set, that is if it was not
     * already present.
     */
    function add(UintSet storage set, address account, uint256 value) internal returns (bool) {
        return _add(set._inner, account, bytes32(value));
    }

    /**
     * @dev Removes a value from a set. O(1).
     *
     * Returns true if the value was removed from the set, that is if it was
     * present.
     */
    function remove(UintSet storage set, address account, uint256 value) internal returns (bool) {
        return _remove(set._inner, account, bytes32(value));
    }

    function removeAll(UintSet storage set, address account) internal {
        return _removeAll(set._inner, account);
    }

    /**
     * @dev Returns true if the value is in the set. O(1).
     */
    function contains(UintSet storage set, address account, uint256 value) internal view returns (bool) {
        return _contains(set._inner, account, bytes32(value));
    }

    /**
     * @dev Returns the number of values in the set. O(1).
     */
    function length(UintSet storage set, address account) internal view returns (uint256) {
        return _length(set._inner, account);
    }

    /**
     * @dev Returns the value stored at position `index` in the set. O(1).
     *
     * Note that there are no guarantees on the ordering of values inside the
     * array, and it may change when more values are added or removed.
     *
     * Requirements:
     *
     * - `index` must be strictly less than {length}.
     */
    function at(UintSet storage set, address account, uint256 index) internal view returns (uint256) {
        return uint256(_at(set._inner, account, index));
    }

    /**
     * @dev Return the entire set in an array
     *
     * WARNING: This operation will copy the entire storage to memory, which can be quite expensive. This is designed
     * to mostly be used by view accessors that are queried without any gas fees. Developers should keep in mind that
     * this function has an unbounded cost, and using it as part of a state-changing function may render the function
     * uncallable if the set grows to a point where copying to memory consumes too much gas to fit in a block.
     */
    function values(UintSet storage set, address account) internal view returns (uint256[] memory) {
        bytes32[] memory store = _values(set._inner, account);
        uint256[] memory result;

        /// @solidity memory-safe-assembly
        assembly {
            result := store
        }

        return result;
    }
}
