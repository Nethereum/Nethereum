using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// End-to-end DevP2P stack validation against a real public mainnet bootnode.
    /// Dials TCP, runs the RLPx ECIES handshake, exchanges p2p Hello, negotiates the
    /// shared <c>eth/68</c> capability, exchanges Status (with a freshly computed
    /// EIP-2124 mainnet ForkID), then asks the peer for the genesis block header
    /// and asserts the returned header keccaks to the canonical
    /// <see cref="MainnetGenesisConstants.BlockHashHex"/>.
    /// <para>
    /// Tagged <c>Category=LiveNetwork</c> so CI can opt out via
    /// <c>--filter "Category!=LiveNetwork"</c>. The test gracefully tries each
    /// well-known Ethereum Foundation bootnode in turn; only fails if NONE of them
    /// responds within the timeout.
    /// </para>
    /// </summary>
    [Trait("Category", "LiveNetwork")]
    public class MainnetPeerHandshakeTests
    {
        /// <summary>
        /// Ethereum Foundation Go bootnodes — same list as go-ethereum's
        /// <c>params.MainnetBootnodes</c>. Tried sequentially until one accepts our
        /// dial. Update if upstream rotates them.
        /// </summary>
        private static readonly string[] MainnetBootnodes = new[]
        {
            "enode://d860a01f9722d78051619d1e2351aba3f43f943f6f00718d1b9baa4101932a1f5011f16bb2b1bb35db20d6fe28fa0bf09636d26a87d31de9ec6203eeedb1f666@18.138.108.67:30303",
            "enode://22a8232c3abc76a16ae9d6c3b164f98775fe226f0917b0ca871128a74a8e9630b458460865bab457221f1d448dd9791d24c4e5d88786180ac185df813a68d4de@3.209.45.79:30303",
            "enode://2b252ab6a1d0f971d9722cb839a42cb81db019ba44c08754628ab4a823487071b5695317c8ccd085219c3a03af063495b2f1da8d18218da2d6a82981b45e6ffc@65.108.70.101:30303",
            "enode://4aeb4ab6c14b23e2c4cfdce879c04b0748a20d8e9b59e25ded2a08143e265c6c25936e74cbc8e641e3312ca288673d91f2f93f8e277de3cfa444ecdaaf982052@157.90.35.166:30303"
        };

        /// <summary>Frozen post-Merge total difficulty (5.875 × 10²² wei-equivalent).</summary>
        private static readonly BigInteger TerminalTotalDifficulty =
            BigInteger.Parse("58750000000000000000000");

        private static readonly TimeSpan PerBootnodeTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(90);

        private readonly ITestOutputHelper _output;
        public MainnetPeerHandshakeTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public async Task DialMainnetBootnode_FetchGenesisHeader_HashMatchesCanonical()
        {
            using var overallCts = new CancellationTokenSource(TotalTimeout);

            Exception lastError = null;
            foreach (var enodeUrl in MainnetBootnodes)
            {
                overallCts.Token.ThrowIfCancellationRequested();
                try
                {
                    await RunSingleBootnodeAsync(enodeUrl, overallCts.Token);
                    return; // first successful peer is enough
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  {ParseEnodeHost(enodeUrl)} failed: {ex.GetType().Name}: {ex.Message}");
                    lastError = ex;
                }
            }

            throw new Xunit.Sdk.XunitException(
                $"All {MainnetBootnodes.Length} bootnodes refused or timed out. Last error: {lastError?.Message}");
        }

        private async Task RunSingleBootnodeAsync(string enodeUrl, CancellationToken ct)
        {
            var (nodeId, host, port) = ParseEnode(enodeUrl);
            _output.WriteLine($"Dialing {host}:{port} ({enodeUrl.Substring("enode://".Length, 12)}…)");

            var localKey = EthECKey.GenerateKey();
            var cfg = new DevP2PConfig
            {
                ClientId = "Nethereum/0.0.0-test",
                ConnectTimeoutMs = (int)PerBootnodeTimeout.TotalMilliseconds,
                HandshakeTimeoutMs = (int)PerBootnodeTimeout.TotalMilliseconds,
                RequestTimeoutMs = (int)PerBootnodeTimeout.TotalMilliseconds,
                ReadTimeoutMs = (int)PerBootnodeTimeout.TotalMilliseconds,
                PingIntervalMs = 30_000
            };

            using var perPeerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perPeerCts.CancelAfter(PerBootnodeTimeout);

            using var conn = new RlpxConnection(localKey, cfg);
            await conn.ConnectAsync(host, port, nodeId, perPeerCts.Token);

            var ethCap = conn.SharedCapabilities.Find(c => c.Name == "eth")
                ?? throw new InvalidOperationException("Peer didn't negotiate any eth/* capability");
            _output.WriteLine($"  RLPx + eth/{ethCap.Version} negotiated; remote ClientId='{conn.RemoteHello.ClientId}'");

            var ethOffset = conn.GetCapabilityOffset("eth");
            var genesisHash = MainnetGenesisConstants.BlockHashHex.HexToByteArray();

            // Status exchange happens in two phases:
            //   1. Receive THEIR Status first so we know the live forkHash + latest
            //      block. Many production peers send Status before they expect ours.
            //   2. Echo their live-network values back so we look like a sibling
            //      peer near the chain tip — peers refuse to serve historical data
            //      to nodes claiming LatestBlock=0 (they assume we'll never catch up).
            var peerStatus = await ReceivePeerStatusAsync(conn, ethCap.Version, ethOffset, genesisHash, perPeerCts.Token);

            await SendStatusAsync(conn, ethCap.Version, ethOffset,
                forkHash: peerStatus.ForkHash,
                latestBlock: peerStatus.LatestBlock,
                latestBlockHash: peerStatus.LatestBlockHash ?? genesisHash,
                genesisHash: genesisHash,
                perPeerCts.Token);

            await FetchAndValidateGenesisHeaderAsync(conn, ethOffset, genesisHash, perPeerCts.Token);

            try { await conn.DisconnectAsync(DisconnectReason.ClientQuitting); }
            catch (Exception) { /* peer already closing */ }
        }

        /// <summary>Snapshot of the peer's network-state claim, taken from their Status reply.</summary>
        private struct PeerStatusInfo
        {
            public uint ForkHash;
            public ulong LatestBlock;
            public byte[] LatestBlockHash;
        }

        private async Task<PeerStatusInfo> ReceivePeerStatusAsync(
            RlpxConnection conn, int ethVersion, int ethOffset, byte[] expectedGenesis, CancellationToken ct)
        {
            var (msgId, payload) = await conn.ReceiveMessageAsync(ct);
            Assert.Equal(ethOffset + EthMessageIds.Status, msgId);

            ulong networkId;
            byte[] peerGenesis;
            var info = new PeerStatusInfo();
            if (ethVersion >= 69)
            {
                var s = Eth69StatusMessageEncoder.Decode(payload);
                networkId = s.NetworkId;
                peerGenesis = s.GenesisHash;
                info.ForkHash = s.ForkHash;
                info.LatestBlock = s.LatestBlock;
                info.LatestBlockHash = s.LatestBlockHash;
                _output.WriteLine($"  peer Status: networkId={s.NetworkId} latest={s.LatestBlock} forkHash=0x{s.ForkHash:x8}");
            }
            else
            {
                var s = Eth68StatusMessageEncoder.Decode(payload);
                networkId = s.NetworkId;
                peerGenesis = s.GenesisHash;
                info.ForkHash = s.ForkHash;
                info.LatestBlock = 0;
                info.LatestBlockHash = s.BestHash;
                _output.WriteLine($"  peer Status: networkId={s.NetworkId} td={s.TotalDifficulty} forkHash=0x{s.ForkHash:x8}");
            }

            Assert.Equal((ulong)MainnetGenesisConstants.ChainId, networkId);
            Assert.Equal(expectedGenesis.ToHex(), peerGenesis.ToHex());
            return info;
        }

        private static async Task SendStatusAsync(
            RlpxConnection conn, int ethVersion, int ethOffset,
            uint forkHash, ulong latestBlock, byte[] latestBlockHash, byte[] genesisHash, CancellationToken ct)
        {
            byte[] payload;
            if (ethVersion >= 69)
            {
                var status = new Eth69StatusMessage
                {
                    ProtocolVersion = ethVersion,
                    NetworkId = (ulong)MainnetGenesisConstants.ChainId,
                    GenesisHash = genesisHash,
                    ForkHash = forkHash,
                    ForkNext = 0,
                    EarliestBlock = 0,
                    LatestBlock = latestBlock,
                    LatestBlockHash = latestBlockHash
                };
                payload = Eth69StatusMessageEncoder.Encode(status);
            }
            else
            {
                // eth/68 still carries TotalDifficulty (frozen at TTD post-Merge).
                var status = new Eth68StatusMessage
                {
                    ProtocolVersion = ethVersion,
                    NetworkId = (ulong)MainnetGenesisConstants.ChainId,
                    TotalDifficulty = TerminalTotalDifficulty,
                    BestHash = latestBlockHash,
                    GenesisHash = genesisHash,
                    ForkHash = forkHash,
                    ForkNext = 0
                };
                payload = Eth68StatusMessageEncoder.Encode(status);
            }
            await conn.SendMessageAsync(ethOffset + EthMessageIds.Status, payload, ct);
        }

        private async Task FetchAndValidateGenesisHeaderAsync(
            RlpxConnection conn, int ethOffset, byte[] expectedGenesisHash, CancellationToken ct)
        {
            var req = new GetBlockHeadersMessage
            {
                RequestId = 1,
                StartBlock = 0,
                Limit = 1,
                Skip = 0,
                Reverse = false
            };
            await conn.SendMessageAsync(
                ethOffset + EthMessageIds.GetBlockHeaders,
                GetBlockHeadersMessageEncoder.Encode(req), ct);

            // Some peers send other traffic (NewPooledTransactionHashes, BlockRangeUpdate, etc.)
            // before getting to our response; loop until we see the BlockHeaders reply we expect.
            for (int i = 0; i < 10; i++)
            {
                var (msgId, payload) = await conn.ReceiveMessageAsync(ct);
                if (msgId == ethOffset + EthMessageIds.BlockHeaders)
                {
                    var headers = BlockHeadersMessageEncoder.Decode(payload);
                    Assert.Equal((ulong)1, headers.RequestId);
                    Assert.Single(headers.Headers);

                    var headerRlp = BlockHeaderEncoder.Current.Encode(headers.Headers[0]);
                    var hash = new Sha3Keccack().CalculateHash(headerRlp);
                    _output.WriteLine($"  received genesis header, keccak = 0x{hash.ToHex()}");

                    Assert.Equal(expectedGenesisHash.ToHex(), hash.ToHex());
                    return;
                }
                // Discard unrelated push traffic and keep waiting for our response.
            }
            throw new InvalidOperationException("Peer did not send BlockHeaders in response to GetBlockHeaders within 10 message attempts");
        }

        private static string ParseEnodeHost(string enode)
        {
            var at = enode.IndexOf('@');
            return at < 0 ? enode : enode.Substring(at + 1);
        }

        private static (byte[] NodeId, string Host, int Port) ParseEnode(string enode)
        {
            const string prefix = "enode://";
            if (!enode.StartsWith(prefix)) throw new ArgumentException("Not an enode URL", nameof(enode));
            var rest = enode.Substring(prefix.Length);
            var atIdx = rest.IndexOf('@');
            var nodeIdHex = rest.Substring(0, atIdx);
            var hostPort = rest.Substring(atIdx + 1);
            var colonIdx = hostPort.IndexOf(':');
            var host = hostPort.Substring(0, colonIdx);
            var port = int.Parse(hostPort.Substring(colonIdx + 1));
            return (nodeIdHex.HexToByteArray(), host, port);
        }
    }
}
