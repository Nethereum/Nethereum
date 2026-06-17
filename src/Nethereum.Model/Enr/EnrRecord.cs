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
            if (!Pairs.TryGetValue(key, out var v) || v == null || v.Length == 0) return null;
            if (v.Length == 1) return v[0];
            return (ushort)((v[0] << 8) | v[1]);
        }
    }

    public static class EnrRecordEncoder
    {
        /// <summary>
        /// Returns the RLP encoding of the signed content: rlp([seq, k0, v0, ...]).
        /// This is the byte sequence over which the v4 signature is computed (after keccak256).
        /// </summary>
        public static byte[] BuildSignedContent(EnrRecord record)
        {
            var elements = new List<byte[]>();
            elements.Add(RLP.RLP.EncodeElement(LongToRlp((long)record.Sequence)));
            foreach (var kv in record.Pairs)
            {
                elements.Add(RLP.RLP.EncodeElement(Encoding.ASCII.GetBytes(kv.Key)));
                elements.Add(RLP.RLP.EncodeElement(kv.Value));
            }
            return RLP.RLP.EncodeList(elements.ToArray());
        }

        public static byte[] EncodeRecord(EnrRecord record)
        {
            var elements = new List<byte[]>();
            elements.Add(RLP.RLP.EncodeElement(record.Signature));
            elements.Add(RLP.RLP.EncodeElement(LongToRlp((long)record.Sequence)));
            foreach (var kv in record.Pairs)
            {
                elements.Add(RLP.RLP.EncodeElement(Encoding.ASCII.GetBytes(kv.Key)));
                elements.Add(RLP.RLP.EncodeElement(kv.Value));
            }
            var encoded = RLP.RLP.EncodeList(elements.ToArray());
            if (encoded.Length > EnrRecord.MaxEncodedSize)
                throw new InvalidOperationException(
                    $"ENR record exceeds {EnrRecord.MaxEncodedSize}-byte limit (got {encoded.Length})");
            return encoded;
        }

        public static EnrRecord Decode(byte[] data)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(data);
            var record = new EnrRecord
            {
                Signature = outer[0].RLPData,
                Sequence = (ulong)outer[1].RLPData.ToLongFromRLPDecoded()
            };
            for (int i = 2; i + 1 < outer.Count; i += 2)
            {
                var key = Encoding.ASCII.GetString(outer[i].RLPData);
                var value = outer[i + 1].RLPData;
                record.Pairs[key] = value;
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

        private static byte[] LongToRlp(long value)
        {
            if (value == 0) return new byte[0];
            return value.ToBytesForRLPEncoding();
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
