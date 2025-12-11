using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    public class SyncCommittee
    {
        public IList<byte[]> PubKeys { get; set; } = new List<byte[]>(SszBasicTypes.SyncCommitteeSize);
        public byte[] AggregatePubKey { get; set; } = new byte[SszBasicTypes.PubKeyLength];

        public byte[] Encode()
        {
            using var writer = new SszWriter();
            if (PubKeys.Count != SszBasicTypes.SyncCommitteeSize)
            {
                throw new InvalidOperationException($"Sync committee must contain {SszBasicTypes.SyncCommitteeSize} pubkeys.");
            }

            foreach (var key in PubKeys)
            {
                writer.WriteFixedBytes(key, SszBasicTypes.PubKeyLength);
            }

            writer.WriteFixedBytes(AggregatePubKey, SszBasicTypes.PubKeyLength);
            return writer.ToArray();
        }

        public static SyncCommittee Decode(ReadOnlySpan<byte> data)
        {
            var reader = new SszReader(data);
            var keys = new List<byte[]>(SszBasicTypes.SyncCommitteeSize);
            for (var i = 0; i < SszBasicTypes.SyncCommitteeSize; i++)
            {
                keys.Add(reader.ReadFixedBytes(SszBasicTypes.PubKeyLength));
            }

            return new SyncCommittee
            {
                PubKeys = keys,
                AggregatePubKey = reader.ReadFixedBytes(SszBasicTypes.PubKeyLength)
            };
        }

        public byte[] HashTreeRoot()
        {
            if (PubKeys.Count != SszBasicTypes.SyncCommitteeSize)
            {
                throw new InvalidOperationException($"Sync committee must contain {SszBasicTypes.SyncCommitteeSize} pubkeys.");
            }

            var pubkeysRoot = SszBasicTypes.HashTreeRootByteVector(PubKeys, SszBasicTypes.PubKeyLength);
            var aggregateRoot = SszBasicTypes.HashTreeRootFixedBytes(AggregatePubKey, SszBasicTypes.PubKeyLength);
            return SszMerkleizer.Merkleize(new[] { pubkeysRoot, aggregateRoot });
        }
    }
}
