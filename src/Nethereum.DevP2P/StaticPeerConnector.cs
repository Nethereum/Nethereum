using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Documentation;
using Nethereum.Signer;

namespace Nethereum.DevP2P
{
    public class StaticPeerConnector
    {
        private readonly EthECKey _localKey;
        private readonly DevP2PConfig _config;

        public StaticPeerConnector(EthECKey localKey = null, DevP2PConfig config = null)
        {
            _localKey = localKey ?? EthECKey.GenerateKey();
            _config = config ?? new DevP2PConfig();
        }

        [NethereumDocExample(DocSection.DevP2P, "devp2p", "StaticPeerConnector.ConnectAsync — dial an enode in one call")]
        public async Task<RlpxConnection> ConnectAsync(string enode, CancellationToken ct = default)
        {
            var parsed = EnodeUrl.Parse(enode);
            var conn = new RlpxConnection(_localKey, _config);
            await conn.ConnectAsync(parsed.Host, parsed.Port, parsed.PublicKey, ct);
            return conn;
        }
    }
}
