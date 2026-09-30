using System;
using System.Numerics;
using Nethereum.RLP;

namespace Nethereum.CoreChain.Storage.History
{
    public sealed class BlockMeta
    {
        public byte[] BlockHash { get; set; }
        public int TxCount { get; set; }
        public byte[] Uncles { get; set; }
        public byte[] Withdrawals { get; set; }
        public byte[] Bloom { get; set; }
    }

    public static class BlockMetaCodec
    {
        public static byte[] Encode(BlockMeta m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(m.BlockHash ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(((BigInteger)m.TxCount).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(m.Uncles ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(m.Withdrawals ?? Array.Empty<byte>()),
                RLP.RLP.EncodeElement(m.Bloom ?? Array.Empty<byte>()));
        }

        public static BlockMeta Decode(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            var list = (RLPCollection)RLP.RLP.Decode(data);
            return new BlockMeta
            {
                BlockHash = Nz(list[0].RLPData),
                TxCount = (int)(list[1].RLPData == null ? 0 : list[1].RLPData.ToLongFromRLPDecoded()),
                Uncles = Nz(list[2].RLPData),
                Withdrawals = Nz(list[3].RLPData),
                Bloom = Nz(list[4].RLPData),
            };
        }

        private static byte[] Nz(byte[] b) => b ?? Array.Empty<byte>();
    }
}
