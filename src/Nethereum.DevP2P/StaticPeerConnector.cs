using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;

namespace Nethereum.DevP2P
{
    public class EnodeUrl
    {
        public byte[] PublicKey { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }

        public string PeerId => PublicKey.ToHex();

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
            var portStr = hostPort.Substring(colonIndex + 1).Split('?')[0];
            var port = int.Parse(portStr, CultureInfo.InvariantCulture);

            return new EnodeUrl
            {
                PublicKey = pubKeyHex.HexToByteArray(),
                Host = host,
                Port = port
            };
        }
    }

    public class StaticPeerConnector
    {
        private readonly EthECKey _localKey;
        private readonly DevP2PConfig _config;

        public StaticPeerConnector(EthECKey localKey = null, DevP2PConfig config = null)
        {
            _localKey = localKey ?? EthECKey.GenerateKey();
            _config = config ?? new DevP2PConfig();
        }

        public async Task<RlpxConnection> ConnectAsync(string enode, CancellationToken ct = default)
        {
            var parsed = EnodeUrl.Parse(enode);
            var conn = new RlpxConnection(_localKey, _config);
            await conn.ConnectAsync(parsed.Host, parsed.Port, parsed.PublicKey, ct);
            return conn;
        }
    }
}
