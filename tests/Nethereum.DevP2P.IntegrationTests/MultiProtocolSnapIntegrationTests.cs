using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Validates that eth + snap multiplex correctly over a single RLPx
    /// connection: client dials, both peers exchange eth Status, client then
    /// sends a snap/1 GetAccountRange and the server replies with a valid
    /// AccountRange. Proves MultiProtocolRlpxSession dispatches by capability
    /// offset correctly and that the snap wire bytes are what the spec expects.
    /// This is the prerequisite for hitting the real go-ethereum
    /// `devp2p rlpx snap-test` conformance tool.
    /// </summary>
    public class MultiProtocolSnapIntegrationTests
    {
        private readonly ITestOutputHelper _output;
        public MultiProtocolSnapIntegrationTests(ITestOutputHelper output) { _output = output; }

        private class InMemoryBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<byte[], byte[]> _codes = new(new ByteArrayComparer());
            public void Put(byte[] h, byte[] c) { _codes[h] = c; }
            public byte[] Get(byte[] h) => _codes.TryGetValue(h, out var v) ? v : null;
        }

        [Fact]
        public async Task SnapGetAccountRange_OverMultiplexedRlpx_ReturnsCorrectRange()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig
            {
                ClientId = "Nethereum/snap-mux-test",
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 8000
            };

            // Build a tiny state trie so we have something to snap.
            var keccak = new Sha3Keccack();
            var stateStorage = new InMemoryTrieStorage();
            var stateTrie = new PatriciaTrie();
            var sortedHashes = new List<byte[]>();
            for (int i = 0; i < 16; i++)
            {
                var addrHash = keccak.CalculateHash(new[] { (byte)i, (byte)0xEE });
                var body = new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)(uint)(i + 1),
                    Balance = (EvmUInt256)((ulong)(i + 1) * 7UL),
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH
                });
                stateTrie.Put(addrHash, body, stateStorage);
                sortedHashes.Add(addrHash);
            }
            stateTrie.SaveDirtyNodesToStorage(stateStorage);
            sortedHashes.Sort((a, b) => ByteArrayComparer.Current.Compare(a, b));
            var rootHash = stateTrie.Root.GetHash();

            byte[] fakeGenesis = new byte[32];
            for (int i = 0; i < 32; i++) fakeGenesis[i] = (byte)i;

            using var sessionCts = new CancellationTokenSource();
            var serverReadyTcs = new TaskCompletionSource<bool>();

            var listener = new RlpxListener(serverKey, config);
            listener.PeerAccepted += async (_, conn) =>
            {
                try
                {
                    var ethOffset = conn.GetCapabilityOffset("eth");
                    var status = new Eth68StatusMessage
                    {
                        ProtocolVersion = 68,
                        NetworkId = 12345,
                        TotalDifficulty = BigInteger.One,
                        BestHash = fakeGenesis,
                        GenesisHash = fakeGenesis,
                        ForkHash = ForkId.ComputeHash(fakeGenesis, Array.Empty<ulong>()),
                        ForkNext = 0
                    };
                    await conn.SendMessageAsync(ethOffset + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(status));
                    await conn.ReceiveMessageAsync();

                    var snapHandler = new PatriciaSnapRequestHandler(stateStorage, new InMemoryBytecodeStore());
                    var ethServer = new Eth68ServerSession(conn, new NullEthHandler(), status);
                    ethServer.GetType().GetProperty(nameof(Eth68ServerSession.EthOffset))!
                        .SetValue(ethServer, ethOffset);
                    var snap = new Snap1Handler(conn, snapHandler);
                    var mux = new MultiProtocolRlpxSession(conn, ethServer, snap);

                    serverReadyTcs.TrySetResult(true);
                    await mux.RunAsync(ct: sessionCts.Token);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"server side: {ex.GetType().Name}: {ex.Message}");
                    serverReadyTcs.TrySetException(ex);
                }
            };

            try
            {
                listener.Start(port: 0, bindAddress: IPAddress.Loopback);
                _output.WriteLine($"Server at 127.0.0.1:{listener.Port}");

                var clientConn = new RlpxConnection(clientKey, config);
                await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());

                Assert.Contains(clientConn.SharedCapabilities, c => c.Name == "snap" && c.Version == 1);
                Assert.Contains(clientConn.SharedCapabilities, c => c.Name == "eth");

                var clientEthOffset = clientConn.GetCapabilityOffset("eth");
                var clientSnapOffset = clientConn.GetCapabilityOffset("snap");
                _output.WriteLine($"eth offset {clientEthOffset:x2}, snap offset {clientSnapOffset:x2}");

                var clientStatus = new Eth68StatusMessage
                {
                    ProtocolVersion = 68,
                    NetworkId = 12345,
                    TotalDifficulty = BigInteger.One,
                    BestHash = fakeGenesis,
                    GenesisHash = fakeGenesis,
                    ForkHash = ForkId.ComputeHash(fakeGenesis, Array.Empty<ulong>()),
                    ForkNext = 0
                };
                await clientConn.SendMessageAsync(clientEthOffset + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(clientStatus));
                await clientConn.ReceiveMessageAsync();

                await serverReadyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

                // Send GetAccountRange over snap and read AccountRange back.
                var req = new GetAccountRangeMessage
                {
                    RequestId = 100,
                    RootHash = rootHash,
                    StartingHash = new byte[32],
                    LimitHash = FilledHash(0xff),
                    ResponseBytes = 1_000_000UL
                };
                await clientConn.SendMessageAsync(
                    clientSnapOffset + SnapMessageIds.GetAccountRange,
                    GetAccountRangeMessageEncoder.Encode(req));

                var (respMsgId, respPayload) = await clientConn.ReceiveMessageAsync();
                Assert.Equal(clientSnapOffset + SnapMessageIds.AccountRange, respMsgId);

                var resp = AccountRangeMessageEncoder.Decode(respPayload);
                Assert.Equal(100UL, resp.RequestId);
                Assert.Equal(sortedHashes.Count, resp.Accounts.Count);
                for (int i = 0; i < sortedHashes.Count; i++)
                    Assert.Equal(sortedHashes[i].ToHex(), resp.Accounts[i].Hash.ToHex());
                Assert.NotEmpty(resp.Proof);
                _output.WriteLine($"Got {resp.Accounts.Count} accounts + {resp.Proof.Count} proof nodes over multiplexed RLPx");

                await clientConn.DisconnectAsync();
            }
            finally
            {
                sessionCts.Cancel();
                await listener.StopAsync();
            }
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }

        private class NullEthHandler : IEth68RequestHandler
        {
            public Task<IList<Nethereum.Model.BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage r, CancellationToken ct = default)
                => Task.FromResult<IList<Nethereum.Model.BlockHeader>>(new List<Nethereum.Model.BlockHeader>());
            public Task<IList<BlockBody>> GetBodiesAsync(byte[][] _, CancellationToken ct = default)
                => Task.FromResult<IList<BlockBody>>(new List<BlockBody>());
            public Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] _, CancellationToken ct = default)
                => Task.FromResult(new List<List<Receipt>>());
            public Task<IList<Nethereum.Model.ISignedTransaction>> GetPooledTransactionsAsync(byte[][] _, CancellationToken ct = default)
                => Task.FromResult<IList<Nethereum.Model.ISignedTransaction>>(new List<Nethereum.Model.ISignedTransaction>());
        }
    }
}
