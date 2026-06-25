using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// eth/68 NewBlockHashes (0x01): announces availability of blocks by hash.
    /// Format: [[hash_0, number_0], [hash_1, number_1], ...]
    /// Recipient is expected to fetch the bodies via GetBlockHeaders/GetBlockBodies
    /// if it wants the full block.
    /// </summary>
    public class NewBlockHashesMessage
    {
        public List<BlockHashEntry> Entries { get; set; } = new();

        public class BlockHashEntry
        {
            public byte[] Hash { get; set; } = new byte[32];
            public ulong Number { get; set; }
        }
    }

    public static class NewBlockHashesMessageEncoder
    {
        public static byte[] Encode(NewBlockHashesMessage msg)
        {
            var encodedEntries = new byte[msg.Entries.Count][];
            for (int i = 0; i < msg.Entries.Count; i++)
            {
                encodedEntries[i] = RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(msg.Entries[i].Hash),
                    RLP.RLP.EncodeElement(((long)msg.Entries[i].Number).ToBytesForRLPEncoding())
                );
            }
            return RLP.RLP.EncodeList(encodedEntries);
        }

        public static NewBlockHashesMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var msg = new NewBlockHashesMessage();
            foreach (RLPCollection entry in outer)
            {
                msg.Entries.Add(new NewBlockHashesMessage.BlockHashEntry
                {
                    Hash = entry[0].RLPData,
                    Number = (ulong)entry[1].RLPData.ToLongFromRLPDecoded()
                });
            }
            return msg;
        }
    }
}
