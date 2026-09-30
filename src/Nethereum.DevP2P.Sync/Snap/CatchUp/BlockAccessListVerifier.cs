using System;
using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public sealed class BlockAccessListVerifier
    {
        private static readonly Sha3Keccack Keccak = Sha3Keccack.Current;

        public IReadOnlyList<VerifiedBlockAccessList> Verify(
            IReadOnlyList<byte[]> expectedBalHashes, IReadOnlyList<byte[]> rawEntries)
        {
            if (expectedBalHashes == null) throw new ArgumentNullException(nameof(expectedBalHashes));
            if (rawEntries == null) throw new ArgumentNullException(nameof(rawEntries));
            if (rawEntries.Count > expectedBalHashes.Count)
                throw new ArgumentException("Peer returned more block access lists than were requested.");

            var results = new List<VerifiedBlockAccessList>(rawEntries.Count);
            for (var i = 0; i < rawEntries.Count; i++)
                results.Add(VerifyOne(expectedBalHashes[i], rawEntries[i]));
            return results;
        }

        private static VerifiedBlockAccessList VerifyOne(byte[] expectedHash, byte[] rawEntry)
        {
            if (rawEntry == null || rawEntry.Length == 0)
                return VerifiedBlockAccessList.Unavailable;
            if (!ByteUtil.AreEqual(Keccak.CalculateHash(rawEntry), expectedHash))
                return VerifiedBlockAccessList.HashMismatch;
            return VerifiedBlockAccessList.Verified(BlockAccessListRLPEncoder.Current.Decode(rawEntry));
        }
    }
}
