using System.Collections.Generic;
using System.Numerics;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public sealed class HeaderSyncStateRlpEncoder : IHeaderSyncStateEncoder
    {
        public const ulong CurrentSchemaVersion = 1;

        public static HeaderSyncStateRlpEncoder Instance { get; } = new();

        public byte[] Encode(HeaderSyncState state)
        {
            var subchains = new byte[state.Subchains.Count][];
            for (int i = 0; i < state.Subchains.Count; i++)
                subchains[i] = EncodeSubchain(state.Subchains[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((BigInteger)state.SchemaVersion).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeList(subchains)
            );
        }

        public HeaderSyncState Decode(byte[] data)
        {
            var top = (RLPCollection)RLP.RLP.Decode(data);
            var schemaVersion = top[0].RLPData.ToBigIntegerFromRLPDecoded();
            if ((ulong)schemaVersion != CurrentSchemaVersion) return null;

            var list = (RLPCollection)top[1];
            var subchains = new List<HeaderSubchain>(list.Count);
            for (int i = 0; i < list.Count; i++)
                subchains.Add(DecodeSubchain((RLPCollection)list[i]));

            return new HeaderSyncState
            {
                SchemaVersion = CurrentSchemaVersion,
                Subchains = subchains,
            };
        }

        private static byte[] EncodeSubchain(HeaderSubchain s) =>
            RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((BigInteger)s.Head).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)s.Tail).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)s.Next).ToBytesForRLPEncoding())
            );

        private static HeaderSubchain DecodeSubchain(RLPCollection s) =>
            new HeaderSubchain
            {
                Head = (ulong)s[0].RLPData.ToBigIntegerFromRLPDecoded(),
                Tail = (ulong)s[1].RLPData.ToBigIntegerFromRLPDecoded(),
                Next = (ulong)s[2].RLPData.ToBigIntegerFromRLPDecoded(),
            };
    }
}
