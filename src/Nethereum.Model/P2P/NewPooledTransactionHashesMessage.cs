using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// eth/68 NewPooledTransactionHashes (0x08): announces mempool tx availability
    /// without sending the full bodies.
    /// Format (eth/68): [types[], sizes[], hashes[]]
    /// - types: byte array, each byte is the EIP-2718 tx type (0 for legacy)
    /// - sizes: list of unsigned integers, byte length of each tx
    /// - hashes: list of 32-byte hashes
    /// All three arrays must be the same length.
    /// </summary>
    public class NewPooledTransactionHashesMessage
    {
        public byte[] Types { get; set; } = new byte[0];
        public List<long> Sizes { get; set; } = new();
        public List<byte[]> Hashes { get; set; } = new();
    }

    public static class NewPooledTransactionHashesMessageEncoder
    {
        public static byte[] Encode(NewPooledTransactionHashesMessage msg)
        {
            var encodedSizes = new byte[msg.Sizes.Count][];
            for (int i = 0; i < msg.Sizes.Count; i++)
                encodedSizes[i] = RLP.RLP.EncodeElement(msg.Sizes[i].ToBytesForRLPEncoding());

            var encodedHashes = new byte[msg.Hashes.Count][];
            for (int i = 0; i < msg.Hashes.Count; i++)
                encodedHashes[i] = RLP.RLP.EncodeElement(msg.Hashes[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(msg.Types),
                RLP.RLP.EncodeList(encodedSizes),
                RLP.RLP.EncodeList(encodedHashes)
            );
        }

        public static NewPooledTransactionHashesMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var msg = new NewPooledTransactionHashesMessage
            {
                Types = outer[0].RLPData ?? new byte[0]
            };

            var sizes = (RLPCollection)outer[1];
            foreach (var s in sizes)
                msg.Sizes.Add(s.RLPData.ToLongFromRLPDecoded());

            var hashes = (RLPCollection)outer[2];
            foreach (var h in hashes)
                msg.Hashes.Add(h.RLPData);

            return msg;
        }
    }
}
