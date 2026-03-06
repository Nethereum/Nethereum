using System;
using System.Numerics;

namespace Nethereum.AppChain.Sync
{
    public class StateSnapshotInfo
    {
        public BigInteger ChainId { get; set; }
        public BigInteger BlockNumber { get; set; }
        public byte[] BlockHash { get; set; } = Array.Empty<byte>();
        public byte[] StateRoot { get; set; } = Array.Empty<byte>();
        public byte[] SnapshotHash { get; set; } = Array.Empty<byte>();

        public long AccountCount { get; set; }
        public long StorageSlotCount { get; set; }
        public long CodeCount { get; set; }
        public long TotalSizeBytes { get; set; }

        public long CreatedAt { get; set; }
        public string? Uri { get; set; }

        public string SnapshotId => $"state_{BlockNumber}";
    }

    public class StateAccount
    {
        public string Address { get; set; } = string.Empty;
        public BigInteger Nonce { get; set; }
        public BigInteger Balance { get; set; }
        public byte[] CodeHash { get; set; } = Array.Empty<byte>();
        public byte[] StorageRoot { get; set; } = Array.Empty<byte>();
    }

    public class StateStorageSlot
    {
        public string Address { get; set; } = string.Empty;
        public BigInteger Slot { get; set; }
        public byte[] Value { get; set; } = Array.Empty<byte>();
    }

    public class StateCode
    {
        public byte[] CodeHash { get; set; } = Array.Empty<byte>();
        public byte[] Code { get; set; } = Array.Empty<byte>();
    }
}
