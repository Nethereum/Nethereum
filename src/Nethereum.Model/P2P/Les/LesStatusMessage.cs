using System.Collections.Generic;
using System.Numerics;
using Nethereum.RLP;

namespace Nethereum.Model.P2P.Les
{
    /// <summary>
    /// les/4 Status (0x00): key/value list (not a flat tuple like eth Status).
    /// Required keys: protocolVersion, networkId, headTd, headHash, headNum, genesisHash.
    /// les/4 added: forkID, recentTxLookup.
    /// Optional (server only): serveHeaders, serveChainSince, serveStateSince,
    /// txRelay, flowControl/BL, flowControl/MRR, flowControl/MRC.
    /// </summary>
    public class LesStatusMessage
    {
        public Dictionary<string, byte[]> Entries { get; set; } = new();

        public int? ProtocolVersion
        {
            get => Entries.TryGetValue("protocolVersion", out var v) ? (int)v.ToLongFromRLPDecoded() : (int?)null;
            set => Entries["protocolVersion"] = value.HasValue && value.Value > 0
                ? ((long)value.Value).ToBytesForRLPEncoding()
                : new byte[0];
        }

        public ulong? NetworkId
        {
            get => Entries.TryGetValue("networkId", out var v) ? (ulong)v.ToLongFromRLPDecoded() : (ulong?)null;
            set => Entries["networkId"] = value.HasValue && value.Value > 0
                ? ((long)value.Value).ToBytesForRLPEncoding()
                : new byte[0];
        }

        public byte[] HeadHash
        {
            get => Entries.TryGetValue("headHash", out var v) ? v : null;
            set => Entries["headHash"] = value;
        }

        public ulong? HeadNumber
        {
            get => Entries.TryGetValue("headNum", out var v) ? (ulong)v.ToLongFromRLPDecoded() : (ulong?)null;
            set => Entries["headNum"] = value.HasValue && value.Value > 0
                ? ((long)value.Value).ToBytesForRLPEncoding()
                : new byte[0];
        }

        public byte[] GenesisHash
        {
            get => Entries.TryGetValue("genesisHash", out var v) ? v : null;
            set => Entries["genesisHash"] = value;
        }
    }

    public static class LesStatusMessageEncoder
    {
        public static byte[] Encode(LesStatusMessage msg)
        {
            var encoded = new List<byte[]>();
            foreach (var kv in msg.Entries)
            {
                encoded.Add(RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(System.Text.Encoding.ASCII.GetBytes(kv.Key)),
                    RLP.RLP.EncodeElement(kv.Value ?? new byte[0])
                ));
            }
            return RLP.RLP.EncodeList(encoded.ToArray());
        }

        public static LesStatusMessage Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var msg = new LesStatusMessage();
            foreach (RLPCollection entry in outer)
            {
                var key = System.Text.Encoding.ASCII.GetString(entry[0].RLPData);
                var value = entry[1].RLPData ?? new byte[0];
                msg.Entries[key] = value;
            }
            return msg;
        }
    }
}
