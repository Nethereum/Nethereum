using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Live mainnet peer session: dial → RLPx + p2p Hello → eth capability →
    /// echoed Status (mirror peer's forkHash/latestBlock so they accept us as a
    /// sibling near the tip) → bulk Header + Body request/response.
    /// <para>
    /// Implements the small fraction of eth/68/69 needed to drive a from-genesis
    /// replay: <c>GetBlockHeaders</c>/<c>BlockHeaders</c> and
    /// <c>GetBlockBodies</c>/<c>BlockBodies</c>. Unrelated push traffic
    /// (<c>NewPooledTransactionHashes</c>, <c>BlockRangeUpdate</c>, …) is
    /// silently discarded.
    /// </para>
    /// <para>
    /// The dial logic mirrors <c>MainnetPeerHandshakeTests</c>: try each
    /// well-known Ethereum Foundation bootnode in sequence until one accepts.
    /// </para>
    /// </summary>
    public sealed class MainnetPeerSession : IAsyncDisposable, IDisposable, IEthPeer
    {
        /// <summary>
        /// Mainnet bootnodes. The first four are the Ethereum Foundation Go
        /// bootnodes (the canonical mainnet bootnode set the major clients
        /// all publish). The remainder
        /// are additional well-known mainnet peers published by Nethermind in
        /// <c>src/Nethermind/Chains/foundation.json</c>. Including more
        /// bootnodes is purely upside: a failed dial costs one connect attempt
        /// (recorded as RecordFailure for backoff), but every live one is an
        /// independent seed that survives the others going down. Losing the
        /// single dialled EF bootnode previously starved the pool for minutes.
        /// </summary>
        public static readonly string[] MainnetBootnodes = new[]
        {
            "enode://d860a01f9722d78051619d1e2351aba3f43f943f6f00718d1b9baa4101932a1f5011f16bb2b1bb35db20d6fe28fa0bf09636d26a87d31de9ec6203eeedb1f666@18.138.108.67:30303",
            "enode://22a8232c3abc76a16ae9d6c3b164f98775fe226f0917b0ca871128a74a8e9630b458460865bab457221f1d448dd9791d24c4e5d88786180ac185df813a68d4de@3.209.45.79:30303",
            "enode://2b252ab6a1d0f971d9722cb839a42cb81db019ba44c08754628ab4a823487071b5695317c8ccd085219c3a03af063495b2f1da8d18218da2d6a82981b45e6ffc@65.108.70.101:30303",
            "enode://4aeb4ab6c14b23e2c4cfdce879c04b0748a20d8e9b59e25ded2a08143e265c6c25936e74cbc8e641e3312ca288673d91f2f93f8e277de3cfa444ecdaaf982052@157.90.35.166:30303",
            "enode://81863f47e9bd652585d3f78b4b2ee07b93dad603fd9bc3c293e1244250725998adc88da0cef48f1de89b15ab92b15db8f43dc2b6fb8fbd86a6f217a1dd886701@193.70.55.37:30303",
            "enode://4afb3a9137a88267c02651052cf6fb217931b8c78ee058bb86643542a4e2e0a8d24d47d871654e1b78a276c363f3c1bc89254a973b00adc359c9e9a48f140686@144.217.139.5:30303",
            "enode://c16d390b32e6eb1c312849fe12601412313165df1a705757d671296f1ac8783c5cff09eab0118ac1f981d7148c85072f0f26407e5c68598f3ad49209fade404d@139.99.51.203:30303",
            "enode://4faf867a2e5e740f9b874e7c7355afee58a2d1ace79f7b692f1d553a1134eddbeb5f9210dd14dc1b774a46fd5f063a8bc1fa90579e13d9d18d1f59bac4a4b16b@139.99.160.213:30303",
            "enode://6a868ced2dec399c53f730261173638a93a40214cf299ccf4d42a76e3fa54701db410669e8006347a4b3a74fa090bb35af0320e4bc8d04cf5b7f582b1db285f5@163.172.131.191:30303",
            "enode://66a483383882a518fcc59db6c017f9cd13c71261f13c8d7e67ed43adbbc82a932d88d2291f59be577e9425181fc08828dc916fdd053af935a9491edf9d6006ba@212.47.247.103:30303",
            "enode://cd6611461840543d5b9c56fbf088736154c699c43973b3a1a32390cf27106f87e58a818a606ccb05f3866de95a4fe860786fea71bf891ea95f234480d3022aa3@163.172.157.114:30303",
            "enode://1d1f7bcb159d308eb2f3d5e32dc5f8786d714ec696bb2f7e3d982f9bcd04c938c139432f13aadcaf5128304a8005e8606aebf5eebd9ec192a1471c13b5e31d49@138.201.223.35:30303",
            "enode://a979fb575495b8d6db44f750317d0f4622bf4c2aa3365d6af7c284339968eef29b69ad0dce72a4d8db5ebb4968de0e3bec910127f134779fbcb0cb6d3331163c@52.16.188.185:30303",
            "enode://3f1d12044546b76342d59d4a05532c14b85aa669704bfe1f864fe079415aa2c02d743e03218e57a33fb94523adb54032871a6c51b2cc5514cb7c7e35b3ed0a99@13.93.211.84:30303",
            "enode://78de8a0916848093c73790ead81d1928bec737d565119932b98c6b100d944b7a95e94f847f689fc723399d2e31129d182f7ef3863f2b4c820abbf3ab2722344d@191.235.84.50:30303",
            "enode://158f8aab45f6d19c6cbf4a089c2670541a8da11978a2f90dbf6a502a4a3bab80d288afdbeb7ec0ef6d92de563767f3b1ea9e8e334ca711e9f8e2df5a0385e8e6@13.75.154.138:30303",
            "enode://1118980bf48b0a3640bdba04e0fe78b1add18e1cd99bf22d53daac1fd9972ad650df52176e7c7d89d1114cfef2bc23a2959aa54998a46afcf7d91809f0855082@52.74.57.123:30303",
            "enode://979b7fa28feeb35a4741660a16076f1943202cb72b6af70d327f053e248bab9ba81760f39d0701ef1d8f89cc1fbd2cacba0710a12cd5314d5e0c9021aa3637f9@5.1.83.226:30303",
            "enode://0cc5f5ffb5d9098c8b8c62325f3797f56509bff942704687b6530992ac706e2cb946b90a34f1f19548cd3c7baccbcaea354531e5983c7d1bc0dee16ce4b6440b@40.118.3.223:30305",
            "enode://1c7a64d76c0334b0418c004af2f67c50e36a3be60b5e4790bdac0439d21603469a85fad36f2473c9a80eb043ae60936df905fa28f1ff614c3e5dc34f15dcd2dc@40.118.3.223:30308",
            "enode://85c85d7143ae8bb96924f2b54f1b3e70d8c4d367af305325d30a61385a432f247d2c75c45c6b4a60335060d072d7f5b35dd1d4c45f76941f62a4f83b6e75daaf@40.118.3.223:30309",
            "enode://de471bccee3d042261d52e9bff31458daecc406142b401d4cd848f677479f73104b9fdeb090af9583d3391b7f10cb2ba9e26865dd5fca4fcdc0fb1e3b723c786@54.94.239.50:30303",
            "enode://4cd540b2c3292e17cff39922e864094bf8b0741fcc8c5dcea14957e389d7944c70278d872902e3d0345927f621547efa659013c400865485ab4bfa0c6596936f@138.201.144.135:30303",
            "enode://01f76fa0561eca2b9a7e224378dd854278735f1449793c46ad0c4e79e8775d080c21dcc455be391e90a98153c3b05dcc8935c8440de7b56fe6d67251e33f4e3c@51.15.42.252:30303",
            "enode://2c9059f05c352b29d559192fe6bca272d965c9f2290632a2cfda7f83da7d2634f3ec45ae3a72c54dd4204926fb8082dcf9686e0d7504257541c86fc8569bcf4b@163.172.171.38:30303",
            "enode://efe4f2493f4aff2d641b1db8366b96ddacfe13e7a6e9c8f8f8cf49f9cdba0fdf3258d8c8f8d0c5db529f8123c8f1d95f36d54d590ca1bb366a5818b9a4ba521c@163.172.187.252:30303",
            "enode://bcc7240543fe2cf86f5e9093d05753dd83343f8fda7bf0e833f65985c73afccf8f981301e13ef49c4804491eab043647374df1c4adf85766af88a624ecc3330e@136.243.154.244:30303",
            "enode://ed4227681ca8c70beb2277b9e870353a9693f12e7c548c35df6bca6a956934d6f659999c2decb31f75ce217822eefca149ace914f1cbe461ed5a2ebaf9501455@88.212.206.70:30303",
            "enode://cadc6e573b6bc2a9128f2f635ac0db3353e360b56deef239e9be7e7fce039502e0ec670b595f6288c0d2116812516ad6b6ff8d5728ff45eba176989e40dead1e@37.128.191.230:30303",
            "enode://595a9a06f8b9bc9835c8723b6a82105aea5d55c66b029b6d44f229d6d135ac3ecdd3e9309360a961ea39d7bee7bac5d03564077a4e08823acc723370aace65ec@46.20.235.22:30303",
            "enode://029178d6d6f9f8026fc0bc17d5d1401aac76ec9d86633bba2320b5eed7b312980c0a210b74b20c4f9a8b0b2bf884b111fa9ea5c5f916bb9bbc0e0c8640a0f56c@216.158.85.185:30303",
            "enode://fdd1b9bb613cfbc200bba17ce199a9490edc752a833f88d4134bf52bb0d858aa5524cb3ec9366c7a4ef4637754b8b15b5dc913e4ed9fdb6022f7512d7b63f181@212.47.247.103:30303",
            "enode://cc26c9671dffd3ee8388a7c8c5b601ae9fe75fc0a85cedb72d2dd733d5916fad1d4f0dcbebad5f9518b39cc1f96ba214ab36a7fa5103aaf17294af92a89f227b@52.79.241.155:30303",
            "enode://140872ce4eee37177fbb7a3c3aa4aaebe3f30bdbf814dd112f6c364fc2e325ba2b6a942f7296677adcdf753c33170cb4999d2573b5ff7197b4c1868f25727e45@52.78.149.82:30303"
        };

        /// <summary>Frozen post-Merge total difficulty (5.875 × 10²² wei-equivalent).</summary>
        private static readonly BigInteger TerminalTotalDifficulty =
            BigInteger.Parse("58750000000000000000000");

        // While waiting for a request reply, peers may push unsolicited NewBlock /
        // NewBlockHashes / Transactions. We need to drain them so we can find our
        // reply, but a hostile peer could push 16 MB frames indefinitely. Cap both
        // the message count and the cumulative bytes so a single starved request
        // can't gigabytes of memory.
        private const int MaxResponseDrainAttempts = 64;
        private const long MaxResponseDrainBytes = 32 * 1024 * 1024; // 32 MiB

        private readonly RlpxConnection _conn;
        private readonly int _ethVersion;
        private readonly int _ethOffset;
        private ulong _nextRequestId;

        public string PeerEnode { get; }
        public string PeerHost { get; }
        public string PeerClientId => _conn.RemoteHello.ClientId;
        public int EthVersion => _ethVersion;
        public ulong PeerLatestBlock { get; }
        public uint PeerForkHash { get; }

        public Guid Id { get; } = Guid.NewGuid();
        public RlpxConnection Connection => _conn;
        string IEthPeer.Enode => PeerEnode;
        string IEthPeer.Host => PeerHost;
        public event EventHandler<IEthPeer>? Disconnected;

        private MainnetPeerSession(
            string enodeUrl, string host,
            RlpxConnection conn, int ethVersion, int ethOffset,
            ulong peerLatestBlock, uint peerForkHash)
        {
            PeerEnode = enodeUrl;
            PeerHost = host;
            _conn = conn;
            _ethVersion = ethVersion;
            _ethOffset = ethOffset;
            _nextRequestId = 1;
            PeerLatestBlock = peerLatestBlock;
            PeerForkHash = peerForkHash;
            _conn.Disconnected += (_, _) => Disconnected?.Invoke(this, this);
        }

        public static async Task<MainnetPeerSession> ConnectToFirstAvailableBootnodeAsync(
            string[] bootnodes,
            TimeSpan perPeerTimeout,
            CancellationToken ct,
            Action<string> log = null)
        {
            log = log ?? (_ => { });
            Exception last = null;
            foreach (var enode in bootnodes)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    log($"Dialing {ParseHost(enode)} …");
                    return await ConnectAsync(enode, perPeerTimeout, ct);
                }
                catch (RlpxPeerRejectedException rej)
                {
                    log($"  {ParseHost(enode)} rejected us: {rej.Reason} (0x{(byte)rej.Reason:x2})");
                    last = rej;
                }
                catch (Exception ex)
                {
                    log($"  {ParseHost(enode)} failed: {ex.GetType().Name}: {ex.Message}");
                    last = ex;
                }
            }
            throw new InvalidOperationException(
                $"All {bootnodes.Length} bootnodes refused or timed out. Last: {last?.Message}", last);
        }

        public static async Task<MainnetPeerSession> ConnectAsync(
            string enodeUrl, TimeSpan timeout, CancellationToken ct,
            ulong minPeerLatestBlock = 0)
        {
            var (nodeId, host, port) = ParseEnode(enodeUrl);
            var localKey = EthECKey.GenerateKey();
            var cfg = new DevP2PConfig
            {
                ClientId = "Nethereum.SyncNode/0.1",
                ConnectTimeoutMs = (int)timeout.TotalMilliseconds,
                HandshakeTimeoutMs = (int)timeout.TotalMilliseconds,
                RequestTimeoutMs = (int)timeout.TotalMilliseconds,
                ReadTimeoutMs = (int)timeout.TotalMilliseconds,
                PingIntervalMs = 30_000
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            var conn = new RlpxConnection(localKey, cfg);
            try
            {
                await conn.ConnectAsync(host, port, nodeId, cts.Token);
                var ethCap = conn.SharedCapabilities.Find(c => c.Name == "eth")
                    ?? throw new InvalidOperationException("Peer did not negotiate any eth/* capability");
                var ethOffset = conn.GetCapabilityOffset("eth");
                var genesisHash = MainnetGenesisConstants.BlockHashHex.HexToByteArray();

                var peerInfo = await ReceiveStatusAsync(conn, ethCap.Version, ethOffset, genesisHash, cts.Token);

                // Peer quality filter: reject useless peers BEFORE we send
                // our status. A peer reporting Latest=0 has nothing to give
                // us; a peer reporting < our target head can't help advance.
                // Without this, the rotator keeps these peers and stalls
                // waiting for blocks that will never come.
                if (peerInfo.LatestBlock < minPeerLatestBlock)
                {
                    throw new UselessPeerException(
                        $"Peer Latest={peerInfo.LatestBlock} < required minimum {minPeerLatestBlock}");
                }

                // Echo the peer's own head back. This was the original behaviour
                // and is what makes eth header/body/receipt fetches flow at full
                // rate — peers prioritise serving us when we look in-sync.
                // Earlier today we tried latest=0 to unblock snap GetAccountRange
                // (peers refuse snap state to a node that claims to have it).
                // That fix worked for snap (zero disconnects) but cratered Phase 1
                // throughput from ~1000 blk/s to 1-2 blk/s — peers deprioritise
                // serving headers/bodies to a node claiming latest=0. Reverting
                // until we have a snap-specific fix that doesn't regress eth.
                await SendStatusAsync(conn, ethCap.Version, ethOffset,
                    forkHash: peerInfo.ForkHash,
                    latestBlock: peerInfo.LatestBlock,
                    latestBlockHash: peerInfo.LatestBlockHash ?? genesisHash,
                    genesisHash: genesisHash,
                    cts.Token);

                return new MainnetPeerSession(enodeUrl, host, conn, ethCap.Version, ethOffset, peerInfo.LatestBlock, peerInfo.ForkHash);
            }
            catch (UselessPeerException)
            {
                if (conn.IsConnected)
                {
                    try { await conn.DisconnectAsync(DisconnectReason.UselessPeer); } catch { }
                }
                conn.Dispose();
                throw;
            }
            catch (InvalidOperationException)
            {
                // Network/genesis mismatch or unexpected msgId — peer is on
                // wrong chain or violated protocol. Send the typed disconnect
                // so they know why before we close the socket.
                if (conn.IsConnected)
                {
                    try { await conn.DisconnectAsync(DisconnectReason.ProtocolBreach); } catch { }
                }
                conn.Dispose();
                throw;
            }
            catch
            {
                if (conn.IsConnected)
                {
                    try { await conn.DisconnectAsync(DisconnectReason.ClientQuitting); } catch { }
                }
                conn.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Thrown by <see cref="ConnectAsync"/> when the peer is structurally
        /// fine (RLPx handshake + eth/68 status both succeed) but the peer's
        /// reported chain head is too far behind to be useful. RotatingPeerSession
        /// uses this as a signal to ban the peer for the run rather than
        /// retry it next rotation cycle.
        /// </summary>
        public sealed class UselessPeerException : Exception
        {
            public UselessPeerException(string message) : base(message) { }
        }

        private struct PeerStatusInfo
        {
            public uint ForkHash;
            public ulong LatestBlock;
            public byte[] LatestBlockHash;
        }

        private static async Task<PeerStatusInfo> ReceiveStatusAsync(
            RlpxConnection conn, int ethVersion, int ethOffset, byte[] expectedGenesis, CancellationToken ct)
        {
            var (msgId, payload) = await conn.ReceiveMessageAsync(ct);
            if (msgId != ethOffset + EthMessageIds.Status)
                throw new InvalidOperationException($"Expected Status (0x10), got msgId=0x{msgId:x2}");

            var info = new PeerStatusInfo();
            ulong networkId;
            byte[] peerGenesis;
            if (ethVersion >= 69)
            {
                var s = Eth69StatusMessageEncoder.Decode(payload);
                networkId = s.NetworkId;
                peerGenesis = s.GenesisHash;
                info.ForkHash = s.ForkHash;
                info.LatestBlock = s.LatestBlock;
                info.LatestBlockHash = s.LatestBlockHash;
            }
            else
            {
                var s = Eth68StatusMessageEncoder.Decode(payload);
                networkId = s.NetworkId;
                peerGenesis = s.GenesisHash;
                info.ForkHash = s.ForkHash;
                info.LatestBlock = 0; // eth/68 only carries TD
                info.LatestBlockHash = s.BestHash;
            }

            if (networkId != (ulong)MainnetGenesisConstants.ChainId)
                throw new InvalidOperationException($"Peer is on network {networkId}, not mainnet");
            if (!ByteUtil.AreEqual(peerGenesis, expectedGenesis))
                throw new InvalidOperationException(
                    $"Peer genesis {peerGenesis.ToHex()} != expected mainnet genesis {expectedGenesis.ToHex()}");

            return info;
        }

        private static Task SendStatusAsync(
            RlpxConnection conn, int ethVersion, int ethOffset,
            uint forkHash, ulong latestBlock, byte[] latestBlockHash, byte[] genesisHash, CancellationToken ct)
        {
            byte[] payload;
            if (ethVersion >= 69)
            {
                payload = Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = ethVersion,
                    NetworkId = (ulong)MainnetGenesisConstants.ChainId,
                    GenesisHash = genesisHash,
                    ForkHash = forkHash,
                    ForkNext = 0,
                    EarliestBlock = 0,
                    LatestBlock = latestBlock,
                    LatestBlockHash = latestBlockHash
                });
            }
            else
            {
                payload = Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
                {
                    ProtocolVersion = ethVersion,
                    NetworkId = (ulong)MainnetGenesisConstants.ChainId,
                    TotalDifficulty = TerminalTotalDifficulty,
                    BestHash = latestBlockHash,
                    GenesisHash = genesisHash,
                    ForkHash = forkHash,
                    ForkNext = 0
                });
            }
            return conn.SendMessageAsync(ethOffset + EthMessageIds.Status, payload, ct);
        }

        /// <summary>
        /// Fetch up to <paramref name="limit"/> consecutive headers starting at
        /// <paramref name="startBlock"/>. Silently discards unrelated push
        /// messages (NewPooledTxHashes etc.) while waiting for the matching
        /// BlockHeaders reply.
        /// </summary>
        public Task<List<BlockHeader>> GetHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct)
            => GetHeadersAsync(startBlock, limit, skip: 0, reverse: false, ct);

        /// <summary>
        /// Fetch up to <paramref name="limit"/> headers anchored at
        /// <paramref name="startBlock"/>, optionally walking backwards or
        /// skipping a fixed step between responses. <paramref name="skip"/>
        /// follows eth-protocol semantics (devp2p/caps/eth.md): the number
        /// of blocks between the responses, so the on-the-wire stride is
        /// <c>skip + 1</c>.
        /// </summary>
        public async Task<List<BlockHeader>> GetHeadersAsync(
            ulong startBlock, ulong limit, ulong skip, bool reverse, CancellationToken ct)
        {
            var reqId = ++_nextRequestId;
            var req = new GetBlockHeadersMessage
            {
                RequestId = reqId,
                StartBlock = startBlock,
                Limit = limit,
                Skip = skip,
                Reverse = reverse
            };
            return await SendAndAwaitHeadersAsync(reqId, req, ct);
        }

        /// <summary>
        /// Fetch a single header by its block hash, with optional follow-on
        /// headers controlled by <paramref name="limit"/>/<paramref name="skip"/>/<paramref name="reverse"/>.
        /// Implements the anchor-by-hash path in
        /// devp2p/caps/eth.md § GetBlockHeaders.
        /// </summary>
        public async Task<List<BlockHeader>> GetHeadersByHashAsync(
            byte[] startHash, ulong limit, ulong skip, bool reverse, CancellationToken ct)
        {
            if (startHash == null || startHash.Length != 32)
                throw new ArgumentException("startHash must be a 32-byte block hash", nameof(startHash));

            var reqId = ++_nextRequestId;
            var req = new GetBlockHeadersMessage
            {
                RequestId = reqId,
                StartBlockHash = startHash,
                Limit = limit,
                Skip = skip,
                Reverse = reverse
            };
            return await SendAndAwaitHeadersAsync(reqId, req, ct);
        }

        private async Task<List<BlockHeader>> SendAndAwaitHeadersAsync(
            ulong reqId, GetBlockHeadersMessage req, CancellationToken ct)
        {
            await _conn.SendMessageAsync(_ethOffset + EthMessageIds.GetBlockHeaders,
                GetBlockHeadersMessageEncoder.Encode(req), ct);

            long drainedBytes = 0;
            for (int i = 0; i < MaxResponseDrainAttempts; i++)
            {
                var (msgId, payload) = await _conn.ReceiveMessageAsync(ct);
                drainedBytes += payload?.Length ?? 0;
                if (drainedBytes > MaxResponseDrainBytes)
                    throw new InvalidOperationException(
                        $"Peer pushed {drainedBytes} bytes of unrelated messages while waiting for BlockHeaders reply {reqId} (cap {MaxResponseDrainBytes}); aborting request");
                if (msgId != _ethOffset + EthMessageIds.BlockHeaders) continue;
                var resp = BlockHeadersMessageEncoder.Decode(payload);
                if (resp.RequestId != reqId) continue;
                return resp.Headers ?? new List<BlockHeader>();
            }
            throw new InvalidOperationException(
                $"Peer did not send BlockHeaders for request {reqId} within {MaxResponseDrainAttempts} message attempts");
        }

        /// <summary>Fetch the bodies for a batch of block hashes.</summary>
        public async Task<List<BlockBody>> GetBodiesAsync(List<byte[]> blockHashes, CancellationToken ct)
        {
            if (blockHashes.Count == 0) return new List<BlockBody>();
            var reqId = ++_nextRequestId;
            var req = new GetBlockBodiesMessage { RequestId = reqId, BlockHashes = blockHashes.ToArray() };
            await _conn.SendMessageAsync(_ethOffset + EthMessageIds.GetBlockBodies,
                GetBlockBodiesMessageEncoder.Encode(req), ct);

            long drainedBytes = 0;
            for (int i = 0; i < MaxResponseDrainAttempts; i++)
            {
                var (msgId, payload) = await _conn.ReceiveMessageAsync(ct);
                drainedBytes += payload?.Length ?? 0;
                if (drainedBytes > MaxResponseDrainBytes)
                    throw new InvalidOperationException(
                        $"Peer pushed {drainedBytes} bytes of unrelated messages while waiting for BlockBodies reply {reqId} (cap {MaxResponseDrainBytes}); aborting request");
                if (msgId != _ethOffset + EthMessageIds.BlockBodies) continue;
                var resp = BlockBodiesMessageEncoder.Decode(payload);
                if (resp.RequestId != reqId) continue;
                return resp.Bodies ?? new List<BlockBody>();
            }
            throw new InvalidOperationException(
                $"Peer did not send BlockBodies for request {reqId} within {MaxResponseDrainAttempts} message attempts");
        }

        /// <summary>
        /// Fetch receipts for a batch of block hashes. On eth/69 the peer
        /// drops the bloom field per EIP-7642 and prefixes each receipt list
        /// with the typed-transaction type byte; the decoder is selected from
        /// the negotiated <see cref="EthVersion"/>.
        /// </summary>
        public async Task<List<List<Receipt>>> GetReceiptsAsync(List<byte[]> blockHashes, CancellationToken ct)
        {
            if (blockHashes.Count == 0) return new List<List<Receipt>>();
            var reqId = ++_nextRequestId;
            var req = new GetReceiptsMessage { RequestId = reqId, BlockHashes = blockHashes.ToArray() };
            await _conn.SendMessageAsync(_ethOffset + EthMessageIds.GetReceipts,
                GetReceiptsMessageEncoder.Encode(req), ct);

            long drainedBytes = 0;
            for (int i = 0; i < MaxResponseDrainAttempts; i++)
            {
                var (msgId, payload) = await _conn.ReceiveMessageAsync(ct);
                drainedBytes += payload?.Length ?? 0;
                if (drainedBytes > MaxResponseDrainBytes)
                    throw new InvalidOperationException(
                        $"Peer pushed {drainedBytes} bytes of unrelated messages while waiting for Receipts reply {reqId} (cap {MaxResponseDrainBytes}); aborting request");
                if (msgId != _ethOffset + EthMessageIds.Receipts) continue;
                var resp = _ethVersion >= 69
                    ? ReceiptsMessageEth69Encoder.Decode(payload)
                    : ReceiptsMessageEncoder.Decode(payload);
                if (resp.RequestId != reqId) continue;
                return resp.ReceiptsByBlock ?? new List<List<Receipt>>();
            }
            throw new InvalidOperationException(
                $"Peer did not send Receipts for request {reqId} within {MaxResponseDrainAttempts} message attempts");
        }

        /// <summary>
        /// Fetch full transaction bodies from the peer's mempool for the
        /// given transaction hashes. Hashes the peer does not currently hold
        /// are silently omitted from the response per eth-protocol semantics.
        /// </summary>
        public async Task<List<ISignedTransaction>> GetPooledTransactionsAsync(List<byte[]> txHashes, CancellationToken ct)
        {
            if (txHashes.Count == 0) return new List<ISignedTransaction>();
            var reqId = ++_nextRequestId;
            var req = new GetPooledTransactionsMessage { RequestId = reqId, Hashes = new List<byte[]>(txHashes) };
            await _conn.SendMessageAsync(_ethOffset + EthMessageIds.GetPooledTransactions,
                GetPooledTransactionsMessageEncoder.Encode(req), ct);

            long drainedBytes = 0;
            for (int i = 0; i < MaxResponseDrainAttempts; i++)
            {
                var (msgId, payload) = await _conn.ReceiveMessageAsync(ct);
                drainedBytes += payload?.Length ?? 0;
                if (drainedBytes > MaxResponseDrainBytes)
                    throw new InvalidOperationException(
                        $"Peer pushed {drainedBytes} bytes of unrelated messages while waiting for PooledTransactions reply {reqId} (cap {MaxResponseDrainBytes}); aborting request");
                if (msgId != _ethOffset + EthMessageIds.PooledTransactions) continue;
                var resp = PooledTransactionsMessageEncoder.Decode(payload);
                if (resp.RequestId != reqId) continue;
                return resp.Transactions ?? new List<ISignedTransaction>();
            }
            throw new InvalidOperationException(
                $"Peer did not send PooledTransactions for request {reqId} within {MaxResponseDrainAttempts} message attempts");
        }

        /// <summary>
        /// True when the negotiated capability set includes <c>snap/1</c>. False
        /// when the peer offered eth/* only (common for archive nodes that
        /// don't serve snap-sync). Callers that issue snap requests should
        /// gate on this — sending a snap message past a peer that did not
        /// negotiate snap/1 sends bytes into the eth/* capability window
        /// instead and gets the connection dropped.
        /// </summary>
        public bool SupportsSnap => _conn.SharedCapabilities.Find(c => c.Name == "snap" && c.Version == 1) != null;

        /// <summary>Comma-separated "name/version" list of every shared
        /// capability the peer and we both negotiated. For diagnostic logging
        /// — pins exactly which sub-protocols are live on this session.</summary>
        public string CapabilitiesDescription =>
            string.Join(",", _conn.SharedCapabilities.Select(c => $"{c.Name}/{c.Version}"));

        private int GetSnapOffsetOrThrow()
        {
            if (!SupportsSnap)
                throw new InvalidOperationException(
                    "Peer did not negotiate snap/1 — snap requests cannot be issued on this session");
            return _conn.GetCapabilityOffset("snap");
        }

        /// <summary>
        /// Fetch a contiguous slice of the state trie's account range at the
        /// given <paramref name="stateRoot"/>, per devp2p/caps/snap.md
        /// §GetAccountRange. Throws <see cref="InvalidOperationException"/> if
        /// the peer did not negotiate snap/1.
        /// </summary>
        public async Task<AccountRangeMessage> GetAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
        {
            var snapOffset = GetSnapOffsetOrThrow();
            var reqId = ++_nextRequestId;
            var req = new GetAccountRangeMessage
            {
                RequestId = reqId,
                RootHash = stateRoot,
                StartingHash = startingHash,
                LimitHash = limitHash,
                ResponseBytes = responseBytes
            };
            var payload = GetAccountRangeMessageEncoder.Encode(req);

            var ethCap = _conn.SharedCapabilities.Find(c => c.Name == "eth");
            var snapCap = _conn.SharedCapabilities.Find(c => c.Name == "snap");
            var headLen = System.Math.Min(64, payload.Length);
            var head = new byte[headLen];
            System.Array.Copy(payload, 0, head, 0, headLen);
            var peerTag = $"{Id.ToString().Substring(0, 8)} @ {PeerHost}";
            System.Console.Error.WriteLine(
                $"[snap-diag] SEND GetAccountRange peer={peerTag} " +
                $"client=\"{PeerClientId}\" " +
                $"ethVer={ethCap?.Version} ethOffset=0x{ethCap?.Offset ?? 0:x2} ethLen={ethCap?.Length ?? 0} " +
                $"snapVer={snapCap?.Version} snapOffset=0x{snapOffset:x2} " +
                $"msgId=0x{snapOffset + SnapMessageIds.GetAccountRange:x2} reqId={reqId} " +
                $"payloadLen={payload.Length} payloadHead={head.ToHex()}");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _conn.SendMessageAsync(snapOffset + SnapMessageIds.GetAccountRange,
                    payload, ct);
                System.Console.Error.WriteLine(
                    $"[snap-diag] SEND-OK peer={peerTag} reqId={reqId} elapsedMs={sw.ElapsedMilliseconds}");
            }
            catch (System.Exception ex)
            {
                System.Console.Error.WriteLine(
                    $"[snap-diag] SEND-FAIL peer={peerTag} reqId={reqId} elapsedMs={sw.ElapsedMilliseconds} " +
                    $"err={ex.GetType().Name}: {ex.Message}");
                throw;
            }

            try
            {
                var resp = await AwaitSnapResponseAsync<AccountRangeMessage>(
                    reqId, snapOffset + SnapMessageIds.AccountRange,
                    AccountRangeMessageEncoder.Decode, m => m.RequestId, ct);
                System.Console.Error.WriteLine(
                    $"[snap-diag] RECV-OK peer={peerTag} reqId={reqId} elapsedMs={sw.ElapsedMilliseconds} " +
                    $"accounts={resp.Accounts.Count} proofs={resp.Proof?.Count ?? 0}");
                return resp;
            }
            catch (System.Exception ex)
            {
                System.Console.Error.WriteLine(
                    $"[snap-diag] RECV-FAIL peer={peerTag} reqId={reqId} elapsedMs={sw.ElapsedMilliseconds} " +
                    $"err={ex.GetType().Name}: {ex.Message}");
                throw;
            }

        }

        /// <summary>
        /// Fetch slot ranges for one or more accounts at the given
        /// <paramref name="stateRoot"/>, per devp2p/caps/snap.md
        /// §GetStorageRanges.
        /// </summary>
        public async Task<StorageRangesMessage> GetStorageRangesAsync(
            byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
        {
            var snapOffset = GetSnapOffsetOrThrow();
            var reqId = ++_nextRequestId;
            var req = new GetStorageRangesMessage
            {
                RequestId = reqId,
                RootHash = stateRoot,
                AccountHashes = accountHashes,
                StartingHash = startingHash,
                LimitHash = limitHash,
                ResponseBytes = responseBytes
            };
            await _conn.SendMessageAsync(snapOffset + SnapMessageIds.GetStorageRanges,
                GetStorageRangesMessageEncoder.Encode(req), ct);
            return await AwaitSnapResponseAsync<StorageRangesMessage>(
                reqId, snapOffset + SnapMessageIds.StorageRanges,
                StorageRangesMessageEncoder.Decode, m => m.RequestId, ct);
        }

        /// <summary>
        /// Fetch contract bytecode by keccak256 hash, per
        /// devp2p/caps/snap.md §GetByteCodes.
        /// </summary>
        public async Task<ByteCodesMessage> GetByteCodesAsync(
            List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
        {
            var snapOffset = GetSnapOffsetOrThrow();
            var reqId = ++_nextRequestId;
            var req = new GetByteCodesMessage
            {
                RequestId = reqId,
                Hashes = codeHashes,
                ResponseBytes = responseBytes
            };
            await _conn.SendMessageAsync(snapOffset + SnapMessageIds.GetByteCodes,
                GetByteCodesMessageEncoder.Encode(req), ct);
            return await AwaitSnapResponseAsync<ByteCodesMessage>(
                reqId, snapOffset + SnapMessageIds.ByteCodes,
                ByteCodesMessageEncoder.Decode, m => m.RequestId, ct);
        }

        /// <summary>
        /// Fetch raw state-trie nodes by path under the given
        /// <paramref name="stateRoot"/>, per devp2p/caps/snap.md §GetTrieNodes.
        /// </summary>
        public async Task<TrieNodesMessage> GetTrieNodesAsync(
            byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
        {
            var snapOffset = GetSnapOffsetOrThrow();
            var reqId = ++_nextRequestId;
            var req = new GetTrieNodesMessage
            {
                RequestId = reqId,
                RootHash = stateRoot,
                Paths = paths,
                ResponseBytes = responseBytes
            };
            await _conn.SendMessageAsync(snapOffset + SnapMessageIds.GetTrieNodes,
                GetTrieNodesMessageEncoder.Encode(req), ct);
            return await AwaitSnapResponseAsync<TrieNodesMessage>(
                reqId, snapOffset + SnapMessageIds.TrieNodes,
                TrieNodesMessageEncoder.Decode, m => m.RequestId, ct);
        }

        private async Task<TResponse> AwaitSnapResponseAsync<TResponse>(
            ulong reqId, int expectedMsgId,
            Func<byte[], TResponse> decode, Func<TResponse, ulong> requestIdOf, CancellationToken ct)
        {
            long drainedBytes = 0;
            for (int i = 0; i < MaxResponseDrainAttempts; i++)
            {
                var (msgId, payload) = await _conn.ReceiveMessageAsync(ct);
                drainedBytes += payload?.Length ?? 0;
                if (drainedBytes > MaxResponseDrainBytes)
                    throw new InvalidOperationException(
                        $"Peer pushed {drainedBytes} bytes of unrelated messages while waiting for snap reply {reqId} (cap {MaxResponseDrainBytes}); aborting request");
                if (msgId != expectedMsgId) continue;
                var resp = decode(payload);
                if (requestIdOf(resp) != reqId) continue;
                return resp;
            }
            throw new InvalidOperationException(
                $"Peer did not send snap response (msgId=0x{expectedMsgId:x2}) for request {reqId} within {MaxResponseDrainAttempts} message attempts");
        }

        public async ValueTask DisposeAsync()
        {
            try { await _conn.DisconnectAsync(DisconnectReason.ClientQuitting); }
            catch { /* peer already gone */ }
            _conn.Dispose();
        }

        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        public static string ParseHost(string enode)
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
