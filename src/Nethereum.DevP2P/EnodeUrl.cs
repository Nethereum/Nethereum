using System;
using System.Globalization;
using System.Net;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.DevP2P
{
    public class EnodeUrl
    {
        public byte[] PublicKey { get; set; }
        public string Host { get; set; }

        public int Port { get; set; }

        public int DiscoveryPort { get; set; }

        public string PeerId => PublicKey.ToHex();

        public string Format() => Format(PublicKey, Host, Port);

        public override string ToString() => Format();

        public static string Format(byte[] publicKey, IPAddress ip, int port) =>
            Format(publicKey, ip?.ToString(), port);

        public static string Format(byte[] publicKey, string host, int port) =>
            $"enode://{publicKey.ToHex()}@{host}:{port}";

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "EnodeUrl.Parse — enode:// URL parser")]
        public static EnodeUrl Parse(string enode)
        {
            if (string.IsNullOrWhiteSpace(enode))
                throw new ArgumentException("Enode URL cannot be empty", nameof(enode));

            if (!enode.StartsWith("enode://", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Invalid enode URL: must start with enode://", nameof(enode));

            var rest = enode.Substring("enode://".Length);
            var atIndex = rest.IndexOf('@');
            if (atIndex < 0)
                throw new ArgumentException("Invalid enode URL: missing @", nameof(enode));

            var pubKeyHex = rest.Substring(0, atIndex);
            if (pubKeyHex.Length != 128)
                throw new ArgumentException("Invalid enode URL: public key must be 128 hex chars (64 bytes)", nameof(enode));

            var hostPort = rest.Substring(atIndex + 1);
            var colonIndex = hostPort.LastIndexOf(':');
            if (colonIndex < 0)
                throw new ArgumentException("Invalid enode URL: missing port", nameof(enode));

            var host = hostPort.Substring(0, colonIndex);
            var portTail = hostPort.Substring(colonIndex + 1);
            var queryIndex = portTail.IndexOf('?');
            var portStr = queryIndex >= 0 ? portTail.Substring(0, queryIndex) : portTail;

            if (!TryParseUint16(portStr, out var port))
                throw new ArgumentException("Invalid enode URL: invalid port", nameof(enode));

            var discoveryPort = port;
            if (queryIndex >= 0)
            {
                var query = portTail.Substring(queryIndex + 1);
                foreach (var param in query.Split('&'))
                {
                    var kv = param.Split('=', 2);

                    if (kv.Length < 1 || !string.Equals(kv[0], "discport", StringComparison.Ordinal))
                        continue;

                    var discPortStr = kv.Length > 1 ? kv[1] : string.Empty;

                    if (string.IsNullOrEmpty(discPortStr))
                        break;

                    if (!TryParseUint16(discPortStr, out var parsedDiscoveryPort))
                        throw new ArgumentException("Invalid enode URL: invalid discport in query", nameof(enode));
                    discoveryPort = parsedDiscoveryPort;
                    break;
                }
            }

            return new EnodeUrl
            {
                PublicKey = pubKeyHex.HexToByteArray(),
                Host = host,
                Port = port,
                DiscoveryPort = discoveryPort
            };
        }

        public static bool TryParse(string enode, out EnodeUrl parsed)
        {
            try
            {
                parsed = Parse(enode);
                return true;
            }
            catch (ArgumentException)
            {
                parsed = null;
                return false;
            }
        }

        private static bool TryParseUint16(string s, out int value)
        {
            value = 0;
            if (!uint.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                return false;
            if (parsed > ushort.MaxValue)
                return false;
            value = (int)parsed;
            return true;
        }
    }
}
