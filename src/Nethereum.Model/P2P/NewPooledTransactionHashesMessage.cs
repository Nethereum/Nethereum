using System;
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new NewPooledTransactionHashesMessage
            {
                Types = reader.ReadBytes()
            };

            var sizes = reader.ReadListHeader();
            if (reader.CountItems(sizes) != msg.Types.Length
                || outer.End - sizes.End < (long)msg.Types.Length * (PeerRlp.HashLength + 1))
                throw new InvalidOperationException("NewPooledTransactionHashes: invalid len of fields");
            msg.Sizes.Capacity = msg.Types.Length;
            while (!reader.IsAtEnd(sizes))
                msg.Sizes.Add(PeerRlp.ReadUInt32(reader));
            reader.EnsureEnd(sizes);

            msg.Hashes.AddRange(PeerRlp.ReadHashes(reader));
            reader.EnsureEnd(outer);
            reader.EnsureEnd();

            if (msg.Hashes.Count != msg.Types.Length || msg.Hashes.Count != msg.Sizes.Count)
                throw new InvalidOperationException("NewPooledTransactionHashes: invalid len of fields");
            return msg;
        }
    }
}
