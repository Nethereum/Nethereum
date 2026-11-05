using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Nethereum.RLP;

namespace Nethereum.Model.Enr
{
    /// <summary>
    /// Ethereum Node Record (EIP-778). Signed key/value list identifying a node.
    /// Canonical form: rlp([signature, seq, k0, v0, k1, v1, ...])
    /// Signature scheme "v4": secp256k1 over keccak256(rlp([seq, k0, v0, ...]))
    /// returns r||s (no recovery byte).
    /// Maximum encoded size: 300 bytes.
    ///
    /// This class is the pure data model + RLP encoding. Signing and verification
    /// helpers live in Nethereum.Signer.Enr (needs crypto).
    /// </summary>
    public class EnrRecord
    {
        public const int MaxEncodedSize = 300;

        public byte[] Signature { get; set; } = new byte[0];
        public ulong Sequence { get; set; }
        public SortedDictionary<string, byte[]> Pairs { get; } = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _listValues = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-enr-list-value", "EnrRecord.SetListValue")]
        public void SetListValue(string key, byte[] encodedList)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!IsSingleEncodedList(encodedList))
                throw new ArgumentException("ENR list value must be exactly one RLP-encoded list", nameof(encodedList));
            Pairs[key] = encodedList;
            _listValues[key] = encodedList;
        }

        [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "model-enr-list-value", "EnrRecord.IsListValue")]
        public bool IsListValue(string key) =>
            key != null
            && _listValues.TryGetValue(key, out var list)
            && Pairs.TryGetValue(key, out var current)
            && ReferenceEquals(list, current);

        private static bool IsSingleEncodedList(byte[] encoded)
        {
            if (encoded == null || encoded.Length == 0 || encoded[0] < 0xc0) return false;
            try
            {
                return RLP.RLP.GetFirstElementLength(encoded) == encoded.Length
                    && RLP.RLP.Decode(encoded) is RLPCollection;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public string Id => Pairs.TryGetValue("id", out var v) ? Encoding.ASCII.GetString(v) : null;
        public byte[] Secp256k1 => Pairs.TryGetValue("secp256k1", out var v) ? v : null;
        public IPAddress IP4 => Pairs.TryGetValue("ip", out var v) && v.Length == 4 ? new IPAddress(v) : null;
        public IPAddress IP6 => Pairs.TryGetValue("ip6", out var v) && v.Length == 16 ? new IPAddress(v) : null;
        public ushort? TcpPort => ReadPort("tcp");
        public ushort? UdpPort => ReadPort("udp");
        public ushort? TcpPort6 => ReadPort("tcp6");
        public ushort? UdpPort6 => ReadPort("udp6");

        private ushort? ReadPort(string key)
        {
            if (!Pairs.TryGetValue(key, out var v) || v == null || v.Length == 0 || v.Length > 2 || v[0] == 0) return null;
            if (v.Length == 1) return v[0];
            return (ushort)((v[0] << 8) | v[1]);
        }
    }

    public static class EnrRecordEncoder
    {
        public static byte[] BuildSignedContent(EnrRecord record)
        {
            var elements = new List<byte[]>();
            elements.Add(RLP.RLP.EncodeElement(SequenceToRlp(record.Sequence)));
            AddPairs(record, elements);
            return RLP.RLP.EncodeList(elements.ToArray());
        }

        public static byte[] EncodeRecord(EnrRecord record)
        {
            var elements = new List<byte[]>();
            elements.Add(RLP.RLP.EncodeElement(record.Signature));
            elements.Add(RLP.RLP.EncodeElement(SequenceToRlp(record.Sequence)));
            AddPairs(record, elements);
            var encoded = RLP.RLP.EncodeList(elements.ToArray());
            if (encoded.Length > EnrRecord.MaxEncodedSize)
                throw new InvalidOperationException(
                    $"ENR record exceeds {EnrRecord.MaxEncodedSize}-byte limit (got {encoded.Length})");
            return encoded;
        }

        private static void AddPairs(EnrRecord record, List<byte[]> elements)
        {
            foreach (var kv in record.Pairs)
            {
                elements.Add(RLP.RLP.EncodeElement(Encoding.ASCII.GetBytes(kv.Key)));
                elements.Add(record.IsListValue(kv.Key) ? kv.Value : RLP.RLP.EncodeElement(kv.Value));
            }
        }

        public static EnrRecord Decode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length > EnrRecord.MaxEncodedSize)
                throw new FormatException(
                    $"ENR record exceeds {EnrRecord.MaxEncodedSize}-byte limit (got {data.Length})");
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            if (outer.Count < 2 || outer.Count % 2 != 0)
                throw new FormatException("ENR record must hold a signature, a sequence and key/value pairs");
            var record = new EnrRecord
            {
                Signature = outer[0].RLPData,
                Sequence = SequenceFromRlp(outer[1].RLPData)
            };
            string previousKey = null;
            for (int i = 2; i < outer.Count; i += 2)
            {
                var key = Encoding.ASCII.GetString(outer[i].RLPData);
                if (previousKey != null && string.CompareOrdinal(previousKey, key) >= 0)
                    throw new FormatException("ENR record keys must be unique and sorted");
                previousKey = key;
                if (outer[i + 1] is RLPCollection)
                    record.SetListValue(key, outer[i + 1].RLPData);
                else
                    record.Pairs[key] = outer[i + 1].RLPData;
            }
            return record;
        }

        /// <summary>
        /// Text encoding per EIP-778: "enr:" prefix + URL-safe base64 of the RLP record (no padding).
        /// </summary>
        public static string ToUrl(EnrRecord record)
        {
            var encoded = EncodeRecord(record);
            return "enr:" + UrlSafeBase64.Encode(encoded);
        }

        public static EnrRecord ParseUrl(string url)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));
            const string prefix = "enr:";
            if (!url.StartsWith(prefix, StringComparison.Ordinal))
                throw new FormatException("ENR URL must start with 'enr:'");
            var b64 = url.Substring(prefix.Length);
            var bytes = UrlSafeBase64.Decode(b64);
            return Decode(bytes);
        }

        private static byte[] SequenceToRlp(ulong value)
        {
            var length = 0;
            for (var v = value; v != 0; v >>= 8) length++;
            var bytes = new byte[length];
            for (var i = length - 1; i >= 0; i--)
            {
                bytes[i] = (byte)value;
                value >>= 8;
            }
            return bytes;
        }

        private static ulong SequenceFromRlp(byte[] data)
        {
            if (data.Length > 8)
                throw new FormatException("ENR sequence exceeds 64 bits");
            if (data.Length > 0 && data[0] == 0)
                throw new FormatException("ENR sequence must not have leading zeros");
            ulong value = 0;
            foreach (var b in data) value = (value << 8) | b;
            return value;
        }

        private static class UrlSafeBase64
        {
            public static string Encode(byte[] data)
            {
                var b64 = Convert.ToBase64String(data);
                return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
            }

            public static byte[] Decode(string s)
            {
                var standard = s.Replace('-', '+').Replace('_', '/');
                var padded = standard + new string('=', (4 - standard.Length % 4) % 4);
                return Convert.FromBase64String(padded);
            }
        }
    }
}
