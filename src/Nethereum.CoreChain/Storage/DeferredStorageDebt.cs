using System;

namespace Nethereum.CoreChain.Storage
{
    public enum StorageCompleteness : byte
    {
        Unknown = 0,
        FullRangeFetched = 1,
        DeferredBigAccount = 2,
        DeferredUnavailable = 3,
        FinalRootReResolved = 4,
        DeepHealInProgress = 5,
        DeepComplete = 6,
        ProofDropped = 7,
        Damaged = 8
    }

    public enum DeferredStorageReason : byte
    {
        StorageResponseEmpty = 1,
        NonEmptyRootReturnedNoSlots = 2,
        StorageRootVerifyMismatch = 3,
        BigAccountInitialEmpty = 4,
        BigAccountInitialProofInvalid = 5,
        BigAccountSubrangeFailed = 6,
        BigAccountException = 7,
        RootRotatedOrPeerCannotServe = 8,
        RepairDamageSeed = 9,
        BigAccountChunked = 10,
        PersistentlyUnservableRoot = 11
    }

    public sealed class DeferredStorageDebt
    {
        public byte[] AccountHash { get; set; }
        public byte[] DiscoveredStorageRoot { get; set; }
        public byte[] FetchStateRoot { get; set; }
        public ulong? FetchPivotBlock { get; set; }
        public DeferredStorageReason Reason { get; set; }
        public StorageCompleteness Status { get; set; }
        public byte[] FinalStateRoot { get; set; }
        public byte[] FinalStorageRoot { get; set; }

        public bool IsOpen => IsOpenStatus(Status);

        public DeferredStorageDebt Clone()
            => new DeferredStorageDebt
            {
                AccountHash = CloneRequired32(AccountHash, nameof(AccountHash)),
                DiscoveredStorageRoot = CloneRequired32(DiscoveredStorageRoot, nameof(DiscoveredStorageRoot)),
                FetchStateRoot = CloneRequired32(FetchStateRoot, nameof(FetchStateRoot)),
                FetchPivotBlock = FetchPivotBlock,
                Reason = Reason,
                Status = Status,
                FinalStateRoot = CloneOptional32(FinalStateRoot, nameof(FinalStateRoot)),
                FinalStorageRoot = CloneOptional32(FinalStorageRoot, nameof(FinalStorageRoot)),
            };

        public static bool IsOpenStatus(StorageCompleteness status)
            => status != StorageCompleteness.FullRangeFetched
               && status != StorageCompleteness.DeepComplete
               && status != StorageCompleteness.ProofDropped;

        public static void Validate(DeferredStorageDebt debt)
        {
            if (debt == null) throw new ArgumentNullException(nameof(debt));
            CloneRequired32(debt.AccountHash, nameof(AccountHash));
            CloneRequired32(debt.DiscoveredStorageRoot, nameof(DiscoveredStorageRoot));
            CloneRequired32(debt.FetchStateRoot, nameof(FetchStateRoot));
            CloneOptional32(debt.FinalStateRoot, nameof(FinalStateRoot));
            CloneOptional32(debt.FinalStorageRoot, nameof(FinalStorageRoot));
        }

        private static byte[] CloneRequired32(byte[] value, string name)
        {
            if (value == null || value.Length != 32)
                throw new ArgumentException($"{name} must be 32 bytes.", name);
            return CloneBytes(value);
        }

        private static byte[] CloneOptional32(byte[] value, string name)
        {
            if (value == null) return null;
            if (value.Length != 32)
                throw new ArgumentException($"{name} must be 32 bytes when present.", name);
            return CloneBytes(value);
        }

        private static byte[] CloneBytes(byte[] value)
        {
            var copy = new byte[value.Length];
            Array.Copy(value, copy, value.Length);
            return copy;
        }
    }
}
