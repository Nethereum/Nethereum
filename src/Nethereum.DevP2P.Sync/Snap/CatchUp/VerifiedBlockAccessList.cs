using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public enum BlockAccessListStatus
    {
        Verified,
        Unavailable,
        HashMismatch
    }

    public sealed class VerifiedBlockAccessList
    {
        public BlockAccessListStatus Status { get; }

        public IReadOnlyList<AccountChanges> AccessList { get; }

        private VerifiedBlockAccessList(BlockAccessListStatus status, IReadOnlyList<AccountChanges> accessList)
        {
            Status = status;
            AccessList = accessList;
        }

        public static VerifiedBlockAccessList Verified(IReadOnlyList<AccountChanges> accessList)
            => new VerifiedBlockAccessList(BlockAccessListStatus.Verified, accessList);

        public static VerifiedBlockAccessList Unavailable { get; } =
            new VerifiedBlockAccessList(BlockAccessListStatus.Unavailable, null);

        public static VerifiedBlockAccessList HashMismatch { get; } =
            new VerifiedBlockAccessList(BlockAccessListStatus.HashMismatch, null);
    }
}
