using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class StorageSlotKey : IEquatable<StorageSlotKey>
    {
        public string ContractAddress { get; }
        public BigInteger Slot { get; }

        public StorageSlotKey(string contractAddress, BigInteger slot)
        {
            ContractAddress = (contractAddress ?? "").ToLowerInvariant();
            Slot = slot;
        }

        public bool Equals(StorageSlotKey? other)
        {
            if (other == null) return false;
            return ContractAddress == other.ContractAddress && Slot == other.Slot;
        }

        public override bool Equals(object? obj) => Equals(obj as StorageSlotKey);

        public override int GetHashCode() => HashCode.Combine(ContractAddress, Slot);
    }

    public class UserOpStorageProfile
    {
        public string UserOpHash { get; set; } = "";
        public string SenderAddress { get; set; } = "";
        public HashSet<StorageSlotKey> ReadSlots { get; } = new();
        public HashSet<StorageSlotKey> WriteSlots { get; } = new();
        public HashSet<string> AccessedContracts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string? Factory { get; set; }
        public string? Paymaster { get; set; }
    }

    public class StorageConflict
    {
        public string UserOpHash1 { get; set; } = "";
        public string UserOpHash2 { get; set; } = "";
        public string ContractAddress { get; set; } = "";
        public BigInteger Slot { get; set; }
        public StorageConflictType Type { get; set; }
        public string Message { get; set; } = "";
    }

    public enum StorageConflictType
    {
        WriteWrite,
        ReadWrite,
        EntityConflict,
        SenderConflict
    }

    public class BundleStorageConflictDetector
    {
        public List<StorageConflict> DetectConflicts(IList<UserOpStorageProfile> profiles)
        {
            var conflicts = new List<StorageConflict>();

            for (int i = 0; i < profiles.Count; i++)
            {
                for (int j = i + 1; j < profiles.Count; j++)
                {
                    var profile1 = profiles[i];
                    var profile2 = profiles[j];

                    if (profile1.SenderAddress.Equals(profile2.SenderAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        conflicts.Add(new StorageConflict
                        {
                            UserOpHash1 = profile1.UserOpHash,
                            UserOpHash2 = profile2.UserOpHash,
                            ContractAddress = profile1.SenderAddress,
                            Type = StorageConflictType.SenderConflict,
                            Message = $"Same sender address {profile1.SenderAddress} in bundle"
                        });
                        continue;
                    }

                    var entityConflicts = CheckEntityConflicts(profile1, profile2);
                    conflicts.AddRange(entityConflicts);

                    var slotConflicts = CheckSlotConflicts(profile1, profile2);
                    conflicts.AddRange(slotConflicts);
                }
            }

            return conflicts;
        }

        private List<StorageConflict> CheckEntityConflicts(UserOpStorageProfile p1, UserOpStorageProfile p2)
        {
            var conflicts = new List<StorageConflict>();

            if (!string.IsNullOrEmpty(p1.Factory) && !string.IsNullOrEmpty(p2.Factory) &&
                p1.Factory.Equals(p2.Factory, StringComparison.OrdinalIgnoreCase))
            {
                var sharedWrites = p1.WriteSlots
                    .Where(s => s.ContractAddress.Equals(p1.Factory, StringComparison.OrdinalIgnoreCase))
                    .Intersect(p2.WriteSlots.Where(s => s.ContractAddress.Equals(p2.Factory, StringComparison.OrdinalIgnoreCase)));

                foreach (var slot in sharedWrites)
                {
                    conflicts.Add(new StorageConflict
                    {
                        UserOpHash1 = p1.UserOpHash,
                        UserOpHash2 = p2.UserOpHash,
                        ContractAddress = slot.ContractAddress,
                        Slot = slot.Slot,
                        Type = StorageConflictType.EntityConflict,
                        Message = $"Shared factory {p1.Factory} write conflict at slot {slot.Slot}"
                    });
                }
            }

            if (!string.IsNullOrEmpty(p1.Paymaster) && !string.IsNullOrEmpty(p2.Paymaster) &&
                p1.Paymaster.Equals(p2.Paymaster, StringComparison.OrdinalIgnoreCase))
            {
                var p1PaymasterWrites = p1.WriteSlots
                    .Where(s => s.ContractAddress.Equals(p1.Paymaster, StringComparison.OrdinalIgnoreCase));
                var p2PaymasterWrites = p2.WriteSlots
                    .Where(s => s.ContractAddress.Equals(p2.Paymaster, StringComparison.OrdinalIgnoreCase));
                var sharedWrites = p1PaymasterWrites.Intersect(p2PaymasterWrites);

                foreach (var slot in sharedWrites)
                {
                    conflicts.Add(new StorageConflict
                    {
                        UserOpHash1 = p1.UserOpHash,
                        UserOpHash2 = p2.UserOpHash,
                        ContractAddress = slot.ContractAddress,
                        Slot = slot.Slot,
                        Type = StorageConflictType.EntityConflict,
                        Message = $"Shared paymaster {p1.Paymaster} write conflict at slot {slot.Slot}"
                    });
                }
            }

            return conflicts;
        }

        private List<StorageConflict> CheckSlotConflicts(UserOpStorageProfile p1, UserOpStorageProfile p2)
        {
            var conflicts = new List<StorageConflict>();

            var writeWriteConflicts = p1.WriteSlots.Intersect(p2.WriteSlots);
            foreach (var slot in writeWriteConflicts)
            {
                if (IsAssociatedSlot(slot, p1) && IsAssociatedSlot(slot, p2))
                {
                    continue;
                }

                conflicts.Add(new StorageConflict
                {
                    UserOpHash1 = p1.UserOpHash,
                    UserOpHash2 = p2.UserOpHash,
                    ContractAddress = slot.ContractAddress,
                    Slot = slot.Slot,
                    Type = StorageConflictType.WriteWrite,
                    Message = $"Write-write conflict at {slot.ContractAddress}[{slot.Slot}]"
                });
            }

            var p1ReadsP2Writes = p1.ReadSlots.Intersect(p2.WriteSlots);
            foreach (var slot in p1ReadsP2Writes)
            {
                if (IsAssociatedSlot(slot, p1) && IsAssociatedSlot(slot, p2))
                {
                    continue;
                }

                conflicts.Add(new StorageConflict
                {
                    UserOpHash1 = p1.UserOpHash,
                    UserOpHash2 = p2.UserOpHash,
                    ContractAddress = slot.ContractAddress,
                    Slot = slot.Slot,
                    Type = StorageConflictType.ReadWrite,
                    Message = $"Read-write conflict: op1 reads, op2 writes {slot.ContractAddress}[{slot.Slot}]"
                });
            }

            var p2ReadsP1Writes = p2.ReadSlots.Intersect(p1.WriteSlots);
            foreach (var slot in p2ReadsP1Writes)
            {
                if (IsAssociatedSlot(slot, p1) && IsAssociatedSlot(slot, p2))
                {
                    continue;
                }

                conflicts.Add(new StorageConflict
                {
                    UserOpHash1 = p1.UserOpHash,
                    UserOpHash2 = p2.UserOpHash,
                    ContractAddress = slot.ContractAddress,
                    Slot = slot.Slot,
                    Type = StorageConflictType.ReadWrite,
                    Message = $"Read-write conflict: op2 reads, op1 writes {slot.ContractAddress}[{slot.Slot}]"
                });
            }

            return conflicts;
        }

        private static bool IsAssociatedSlot(StorageSlotKey slot, UserOpStorageProfile profile)
        {
            return slot.ContractAddress.Equals(profile.SenderAddress, StringComparison.OrdinalIgnoreCase);
        }
    }
}
