using System;
using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    public enum BlockAccessListStructureViolation
    {
        None = 0,
        AccountAddressNotCanonical,
        AccountsOutOfOrder,
        DuplicateAccount,
        ChangedSlotsOutOfOrder,
        DuplicateChangedSlot,
        SlotCarriesNoChange,
        ReadSlotsOutOfOrder,
        DuplicateReadSlot,
        SlotBothChangedAndRead,
        ChangeIndexesOutOfOrder,
        DuplicateChangeIndex,
        ChangeIndexAboveBlockBound
    }

    public readonly struct BlockAccessListStructureCheck
    {
        public static readonly BlockAccessListStructureCheck WellFormed =
            new BlockAccessListStructureCheck(BlockAccessListStructureViolation.None, null);

        public BlockAccessListStructureCheck(BlockAccessListStructureViolation violation, string description)
        {
            Violation = violation;
            Description = description;
        }

        public BlockAccessListStructureViolation Violation { get; }
        public string Description { get; }
        public bool IsWellFormed => Violation == BlockAccessListStructureViolation.None;

        public override string ToString() => IsWellFormed ? "well-formed" : Violation + ": " + Description;
    }

    /// <summary>
    /// EIP-7928 § Ordering, Uniqueness and Determinism: the canonical form a block
    /// access list must already be in when it arrives, checked WITHOUT rebuilding it.
    ///
    /// <para>The list a block builds is canonical by construction, so this rule exists
    /// for a list we did NOT build — the one attached to an Engine API payload, which
    /// EIP-7928 § Engine API requires be validated in its own right: "Returns
    /// <c>INVALID</c> if access list is malformed or doesn't match". A hash comparison
    /// alone answers "doesn't match" and is silent on "malformed".</para>
    ///
    /// <para>Accounts are checked STRICTLY — ascending AND unique — because the EIP
    /// states the two requirements separately, "Accounts: Lexicographic by address"
    /// and "Each address MUST appear exactly once in <c>BlockAccessList</c>", and one
    /// strict comparison is exactly their conjunction. A non-strict comparison accepts
    /// two identical adjacent addresses, which is the whole of the difference.</para>
    /// </summary>
    public static class BlockAccessListStructureRule
    {
        /// <summary>
        /// The first violation of the canonical form, or
        /// <see cref="BlockAccessListStructureCheck.WellFormed"/>. A null list is
        /// well-formed: a block that attaches no access list is validated by
        /// recomputation instead, and absence is not malformation — a caller that
        /// REQUIRES a list, as the Engine API does from Amsterdam, must say so itself.
        ///
        /// <para><paramref name="blockTransactionCount"/> bounds the block access
        /// index, per EIP-7928: "Spurious entries MAY be detected by validating BAL
        /// indices, which MUST never be higher than <c>len(transactions) + 1</c>".</para>
        /// </summary>
        public static BlockAccessListStructureCheck FindViolation(
            List<AccountChanges> blockAccessList, int blockTransactionCount)
        {
            if (blockTransactionCount < 0)
                throw new ArgumentOutOfRangeException(nameof(blockTransactionCount),
                    "A block cannot hold a negative number of transactions; " +
                    "cast unchecked, it would silently bound every access index to zero.");

            if (blockAccessList == null) return BlockAccessListStructureCheck.WellFormed;

            var addresses = AccountAddressesAreCanonical(blockAccessList);
            if (!addresses.IsWellFormed) return addresses;

            var order = AccountsAscendingAndUnique(blockAccessList);
            if (!order.IsWellFormed) return order;

            var highestIndex = (ulong)blockTransactionCount + 1;
            for (var i = 0; i < blockAccessList.Count; i++)
            {
                var account = AccountViolation(blockAccessList[i], highestIndex);
                if (!account.IsWellFormed) return account;
            }
            return BlockAccessListStructureCheck.WellFormed;
        }

        private static BlockAccessListStructureCheck AccountAddressesAreCanonical(
            List<AccountChanges> blockAccessList)
        {
            for (var i = 0; i < blockAccessList.Count; i++)
            {
                var address = blockAccessList[i].Address;
                if (!IsCanonicalAddress(address))
                    return Fail(BlockAccessListStructureViolation.AccountAddressNotCanonical,
                        "account " + i + " has a non-canonical address " + Quote(address) +
                        "; expected a lowercase 0x-prefixed 20-byte address");
            }
            return BlockAccessListStructureCheck.WellFormed;
        }

        private const int CanonicalAddressLength = 42;

        private static bool IsCanonicalAddress(string address)
        {
            if (address == null || address.Length != CanonicalAddressLength) return false;
            if (address[0] != '0' || address[1] != 'x') return false;

            for (var i = 2; i < CanonicalAddressLength; i++)
            {
                var c = address[i];
                var isDigit = c >= '0' && c <= '9';
                var isLowercaseHex = c >= 'a' && c <= 'f';
                if (!isDigit && !isLowercaseHex) return false;
            }
            return true;
        }

        /// <summary>EIP-7928: "Accounts: Lexicographic by address" and "Each address
        /// MUST appear exactly once in <c>BlockAccessList</c>".</summary>
        private static BlockAccessListStructureCheck AccountsAscendingAndUnique(
            List<AccountChanges> blockAccessList)
        {
            for (var i = 1; i < blockAccessList.Count; i++)
            {
                var previous = blockAccessList[i - 1].Address;
                var current = blockAccessList[i].Address;

                var order = string.CompareOrdinal(previous, current);
                if (order > 0)
                    return Fail(BlockAccessListStructureViolation.AccountsOutOfOrder,
                        "accounts are not in lexicographic order: " + Quote(previous) +
                        " precedes " + Quote(current));
                if (order == 0)
                    return Fail(BlockAccessListStructureViolation.DuplicateAccount,
                        "account " + Quote(current) + " appears more than once");
            }
            return BlockAccessListStructureCheck.WellFormed;
        }

        private static BlockAccessListStructureCheck AccountViolation(
            AccountChanges account, ulong highestIndex)
        {
            var slotOrder = ChangedSlotsAscendingAndUnique(account);
            if (!slotOrder.IsWellFormed) return slotOrder;

            var slotsCarryChanges = EverySlotCarriesAChange(account);
            if (!slotsCarryChanges.IsWellFormed) return slotsCarryChanges;

            var slotIndexes = SlotChangeIndexesAreWellFormed(account, highestIndex);
            if (!slotIndexes.IsWellFormed) return slotIndexes;

            var readOrder = ReadSlotsAscendingAndUnique(account);
            if (!readOrder.IsWellFormed) return readOrder;

            var disjoint = ReadsAndChangesAreDisjoint(account);
            if (!disjoint.IsWellFormed) return disjoint;

            return ChangeSeriesIndexesAreWellFormed(account, highestIndex);
        }

        /// <summary>EIP-7928: "storage_changes: Slots lexicographic by storage key" and
        /// "Each storage key MUST appear at most once in <c>storage_changes</c> per
        /// account".</summary>
        private static BlockAccessListStructureCheck ChangedSlotsAscendingAndUnique(AccountChanges account)
        {
            var slots = account.StorageChanges;
            for (var i = 1; i < slots.Count; i++)
            {
                var order = slots[i - 1].Slot.CompareTo(slots[i].Slot);
                if (order > 0)
                    return Fail(BlockAccessListStructureViolation.ChangedSlotsOutOfOrder,
                        Where(account) + "storage_changes slots are not in lexicographic order: " +
                        Hex(slots[i - 1].Slot) + " precedes " + Hex(slots[i].Slot));
                if (order == 0)
                    return Fail(BlockAccessListStructureViolation.DuplicateChangedSlot,
                        Where(account) + "storage_changes lists slot " + Hex(slots[i].Slot) +
                        " more than once");
            }
            return BlockAccessListStructureCheck.WellFormed;
        }

        /// <summary>EIP-7928: "Each <c>SlotChanges</c> entry MUST contain at least one
        /// <c>StorageChange</c>."</summary>
        private static BlockAccessListStructureCheck EverySlotCarriesAChange(AccountChanges account)
        {
            var slots = account.StorageChanges;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].Changes == null || slots[i].Changes.Count == 0)
                    return Fail(BlockAccessListStructureViolation.SlotCarriesNoChange,
                        Where(account) + "slot " + Hex(slots[i].Slot) + " carries no storage change");
            }
            return BlockAccessListStructureCheck.WellFormed;
        }

        /// <summary>EIP-7928: "within each slot, changes by block access index".</summary>
        private static BlockAccessListStructureCheck SlotChangeIndexesAreWellFormed(
            AccountChanges account, ulong highestIndex)
        {
            var slots = account.StorageChanges;
            for (var i = 0; i < slots.Count; i++)
            {
                var withinSlot = ChangeIndexesAreWellFormed(
                    slots[i].Changes, c => c.BlockAccessIndex,
                    "storage_changes for slot " + Hex(slots[i].Slot), account, highestIndex);
                if (!withinSlot.IsWellFormed) return withinSlot;
            }
            return BlockAccessListStructureCheck.WellFormed;
        }

        /// <summary>EIP-7928: "storage_reads: Lexicographic by storage key" and "Each
        /// storage key MUST appear at most once in <c>storage_reads</c> per
        /// account".</summary>
        private static BlockAccessListStructureCheck ReadSlotsAscendingAndUnique(AccountChanges account)
        {
            var reads = account.StorageReads;
            for (var i = 1; i < reads.Count; i++)
            {
                var order = reads[i - 1].CompareTo(reads[i]);
                if (order > 0)
                    return Fail(BlockAccessListStructureViolation.ReadSlotsOutOfOrder,
                        Where(account) + "storage_reads are not in lexicographic order: " +
                        Hex(reads[i - 1]) + " precedes " + Hex(reads[i]));
                if (order == 0)
                    return Fail(BlockAccessListStructureViolation.DuplicateReadSlot,
                        Where(account) + "storage_reads lists slot " + Hex(reads[i]) + " more than once");
            }
            return BlockAccessListStructureCheck.WellFormed;
        }

        /// <summary>EIP-7928: "A storage key MUST NOT appear in both
        /// <c>storage_changes</c> and <c>storage_reads</c>" — a slot that changed is
        /// reported as a change and never also as a read.</summary>
        private static BlockAccessListStructureCheck ReadsAndChangesAreDisjoint(AccountChanges account)
        {
            var reads = account.StorageReads;
            var changes = account.StorageChanges;
            if (reads.Count == 0 || changes.Count == 0) return BlockAccessListStructureCheck.WellFormed;

            var changed = new HashSet<EvmUInt256>();
            for (var i = 0; i < changes.Count; i++)
                changed.Add(changes[i].Slot);

            for (var i = 0; i < reads.Count; i++)
                if (changed.Contains(reads[i]))
                    return Fail(BlockAccessListStructureViolation.SlotBothChangedAndRead,
                        Where(account) + "slot " + Hex(reads[i]) +
                        " appears in both storage_changes and storage_reads");
            return BlockAccessListStructureCheck.WellFormed;
        }

        /// <summary>EIP-7928: "balance_changes, nonce_changes, code_changes: By block
        /// access index (ascending)".</summary>
        private static BlockAccessListStructureCheck ChangeSeriesIndexesAreWellFormed(
            AccountChanges account, ulong highestIndex)
        {
            var balances = ChangeIndexesAreWellFormed(
                account.BalanceChanges, c => c.BlockAccessIndex, "balance_changes",
                account, highestIndex);
            if (!balances.IsWellFormed) return balances;

            var nonces = ChangeIndexesAreWellFormed(
                account.NonceChanges, c => c.BlockAccessIndex, "nonce_changes",
                account, highestIndex);
            if (!nonces.IsWellFormed) return nonces;

            return ChangeIndexesAreWellFormed(
                account.CodeChanges, c => c.BlockAccessIndex, "code_changes",
                account, highestIndex);
        }

        private static BlockAccessListStructureCheck ChangeIndexesAreWellFormed<T>(
            List<T> changes, Func<T, ulong> blockAccessIndex, string listName,
            AccountChanges account, ulong highestIndex)
        {
            var withinBound = IndexesWithinTheBlockBound(
                changes, blockAccessIndex, listName, account, highestIndex);
            if (!withinBound.IsWellFormed) return withinBound;

            return IndexesAscendingAndUnique(changes, blockAccessIndex, listName, account);
        }

        /// <summary>EIP-7928: "Spurious entries MAY be detected by validating BAL
        /// indices, which MUST never be higher than <c>len(transactions) + 1</c>" —
        /// the bound is inclusive, because <c>len(transactions) + 1</c> is the index
        /// post-execution work (withdrawals, requests) is recorded under.</summary>
        private static BlockAccessListStructureCheck IndexesWithinTheBlockBound<T>(
            List<T> changes, Func<T, ulong> blockAccessIndex, string listName,
            AccountChanges account, ulong highestIndex)
        {
            for (var i = 0; i < changes.Count; i++)
            {
                var current = blockAccessIndex(changes[i]);
                if (current > highestIndex)
                    return Fail(BlockAccessListStructureViolation.ChangeIndexAboveBlockBound,
                        Where(account) + listName + " block access index " + current +
                        " is above the block bound of " + highestIndex);
            }
            return BlockAccessListStructureCheck.WellFormed;
        }

        /// <summary>EIP-7928: "Each <c>block_access_index</c> MUST appear at most once
        /// per change list" — ascending and unique is one strict comparison.</summary>
        private static BlockAccessListStructureCheck IndexesAscendingAndUnique<T>(
            List<T> changes, Func<T, ulong> blockAccessIndex, string listName,
            AccountChanges account)
        {
            for (var i = 1; i < changes.Count; i++)
            {
                var previous = blockAccessIndex(changes[i - 1]);
                var current = blockAccessIndex(changes[i]);

                if (previous > current)
                    return Fail(BlockAccessListStructureViolation.ChangeIndexesOutOfOrder,
                        Where(account) + listName +
                        " block access indexes are not ascending: " + previous + " precedes " + current);
                if (previous == current)
                    return Fail(BlockAccessListStructureViolation.DuplicateChangeIndex,
                        Where(account) + listName +
                        " lists block access index " + current + " more than once");
            }
            return BlockAccessListStructureCheck.WellFormed;
        }

        private static BlockAccessListStructureCheck Fail(
            BlockAccessListStructureViolation violation, string description) =>
            new BlockAccessListStructureCheck(violation, description);

        private static string Where(AccountChanges account) => "account " + Quote(account.Address) + ": ";

        private static string Quote(string value) => "'" + value + "'";

        private static string Hex(EvmUInt256 slot) => slot.ToHexString();
    }
}
