using System.Collections.Generic;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
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
            var reader = new RlpReader(data);
            var outer = reader.ReadListHeader();
            var msg = new NewBlockHashesMessage();
            while (!reader.IsAtEnd(outer))
            {
                var entry = reader.ReadListHeader();
                msg.Entries.Add(new NewBlockHashesMessage.BlockHashEntry
                {
                    Hash = reader.ReadFixed(PeerRlp.HashLength),
                    Number = reader.ReadUInt64()
                });
                reader.EnsureEnd(entry);
            }
            reader.EnsureEnd(outer);
            reader.EnsureEnd();
            return msg;
        }
    }
}
