// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

// Linked list implementation for storing module addresses
// Uses sentinel value (0x1) as list head/tail marker

address constant SENTINEL = address(0x1);
address constant ZERO_ADDRESS = address(0x0);

library SentinelListLib {
    struct SentinelList {
        mapping(address => address) entries;
    }

    error LinkedList_AlreadyInitialized();
    error LinkedList_InvalidEntry(address entry);
    error LinkedList_EntryAlreadyInList(address entry);
    error LinkedList_InvalidPrevEntry(address prevEntry);

    function init(SentinelList storage self) internal {
        if (self.entries[SENTINEL] != ZERO_ADDRESS) {
            revert LinkedList_AlreadyInitialized();
        }
        self.entries[SENTINEL] = SENTINEL;
    }

    function contains(SentinelList storage self, address entry) internal view returns (bool) {
        return entry != SENTINEL && entry != ZERO_ADDRESS && self.entries[entry] != ZERO_ADDRESS;
    }

    function getEntriesPaginated(
        SentinelList storage self,
        address start,
        uint256 pageSize
    ) internal view returns (address[] memory array, address next) {
        if (start != SENTINEL && !contains(self, start)) {
            revert LinkedList_InvalidEntry(start);
        }

        array = new address[](pageSize);
        uint256 count = 0;
        next = self.entries[start];

        while (next != ZERO_ADDRESS && next != SENTINEL && count < pageSize) {
            array[count] = next;
            next = self.entries[next];
            count++;
        }

        assembly {
            mstore(array, count)
        }
    }

    function push(SentinelList storage self, address newEntry) internal {
        if (newEntry == ZERO_ADDRESS || newEntry == SENTINEL) {
            revert LinkedList_InvalidEntry(newEntry);
        }
        if (self.entries[newEntry] != ZERO_ADDRESS) {
            revert LinkedList_EntryAlreadyInList(newEntry);
        }

        self.entries[newEntry] = self.entries[SENTINEL];
        self.entries[SENTINEL] = newEntry;
    }

    function pop(SentinelList storage self, address prevEntry, address entry) internal {
        if (entry == ZERO_ADDRESS || entry == SENTINEL) {
            revert LinkedList_InvalidEntry(entry);
        }
        if (self.entries[prevEntry] != entry) {
            revert LinkedList_InvalidPrevEntry(prevEntry);
        }

        self.entries[prevEntry] = self.entries[entry];
        self.entries[entry] = ZERO_ADDRESS;
    }

    function getAll(SentinelList storage self) internal view returns (address[] memory) {
        uint256 count = 0;
        address current = self.entries[SENTINEL];

        // Count entries
        while (current != ZERO_ADDRESS && current != SENTINEL) {
            count++;
            current = self.entries[current];
        }

        // Build array
        address[] memory array = new address[](count);
        current = self.entries[SENTINEL];
        for (uint256 i = 0; i < count; i++) {
            array[i] = current;
            current = self.entries[current];
        }

        return array;
    }
}
