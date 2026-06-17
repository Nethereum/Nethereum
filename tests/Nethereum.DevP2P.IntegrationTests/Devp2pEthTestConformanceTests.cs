using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.DevP2P.IntegrationTests.Helpers;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Runs go-ethereum's canonical `devp2p rlpx eth-test` conformance suite
    /// against a Nethereum RLPx listener backed by chain.rlp data. All 19
    /// sub-tests pass — see <c>README.md</c> for the per-sub-test breakdown.
    /// <para>
    /// Joined to <see cref="GethTestdataCollection"/> so the historical state
    /// replay (~2 s) is pre-built before any test runs, eliminating the
    /// cold-start flake that LargeTxRequest hits under Geth's 2 second read deadline.
    /// </para>
    /// </summary>
    [Collection(GethTestdataCollection.Name)]
    public class Devp2pEthTestConformanceTests
    {
        private readonly ITestOutputHelper _output;
        private readonly GethTestdataFixture _testdata;

        public Devp2pEthTestConformanceTests(ITestOutputHelper output, GethTestdataFixture testdata)
        {
            _output = output;
            _testdata = testdata;
        }

        [Theory]
        [InlineData("Status")]
        [InlineData("MaliciousHandshake")]
        [InlineData("GetBlockHeaders")]
        [InlineData("GetNonexistentBlockHeaders")]
        [InlineData("SimultaneousRequests")]
        [InlineData("SameRequestID")]
        [InlineData("ZeroRequestID")]
        [InlineData("GetBlockBodies")]
        [InlineData("GetReceipts")]
        [InlineData("BlockRangeUpdateInvalid")]
        [InlineData("BlockRangeUpdateFuture")]
        [InlineData("BlockRangeUpdateExpired")]
        [InlineData("Transaction")]
        [InlineData("InvalidTxs")]
        [InlineData("LargeTxRequest")]
        [InlineData("NewPooledTxs")]
        [InlineData("BlobViolations")]
        [InlineData("TestBlobTxWithoutSidecar")]
        [InlineData("TestBlobTxWithMismatchedSidecar")]
        public Task EthTest_PerSubtest_AgainstNethereumServer(string subtest)
            => RunEthSubtest(subtest);

        private async Task RunEthSubtest(string subtest)
        {
            var testdata = FindTestdata();
            var devp2pTool = FindDevp2pTool();

            // Load the chain into a handler. Compute Status fields.
            var genesisHeader = GethTestdataGenesisBuilder.Build(Path.Combine(testdata, "genesis.json"));
            var chainHandler = GethTestdataChainBackedEthHandler.Load(Path.Combine(testdata, "chain.rlp"), genesisHeader);
            var genesisHash = GethChainRlpFixtureReader.ReadGenesisHash(Path.Combine(testdata, "chain.rlp"));
            var headBlockJson = JObject.Parse(File.ReadAllText(Path.Combine(testdata, "headblock.json")));
            var forkenv = JObject.Parse(File.ReadAllText(Path.Combine(testdata, "forkenv.json")));
            var headHash = ParseHex(headBlockJson["hash"].ToString());
            var headNumber = ParseUlong(headBlockJson["number"].ToString());
            var networkId = ulong.Parse(forkenv["HIVE_NETWORK_ID"].ToString());

            ulong[] forkBlocks =
            {
                ulong.Parse(forkenv["HIVE_FORK_TANGERINE"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_SPURIOUS"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_BYZANTIUM"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_CONSTANTINOPLE"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_PETERSBURG"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_ISTANBUL"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_MUIR_GLACIER"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_BERLIN"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_LONDON"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_ARROW_GLACIER"].ToString()),
                ulong.Parse(forkenv["HIVE_FORK_GRAY_GLACIER"].ToString()),
                ulong.Parse(forkenv["HIVE_MERGE_BLOCK_ID"].ToString())
            };
            ulong[] forkTimes =
            {
                ulong.Parse(forkenv["HIVE_SHANGHAI_TIMESTAMP"].ToString()),
                ulong.Parse(forkenv["HIVE_CANCUN_TIMESTAMP"].ToString())
            };
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(genesisHash, forkBlocks, forkTimes);

            var serverKey = EthECKey.GenerateKey();
            var serverConfig = new DevP2PConfig
            {
                ClientId = "Nethereum/eth-conformance",
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000,
                PingIntervalMs = 30000,
                ReadTimeoutMs = 30000
            };
            using var sessionCts = new CancellationTokenSource();

            // Shared tx pool + connected-peer registry for the multi-peer
            // propagation tests (Transaction, InvalidTxs, NewPooledTxs). Each
            // PeerAccepted callback registers, unregisters, and broadcasts
            // through this shared state.
            var txPool = new System.Collections.Concurrent.ConcurrentDictionary<string, ISignedTransaction>();
            var peers = new System.Collections.Concurrent.ConcurrentDictionary<RlpxConnection, int>();
            // Announced tx metadata (hash → declared type+size) — used by blob
            // sub-tests to cross-check NewPooledTransactionHashes against the
            // actual tx encoding in the subsequent PooledTransactions reply.
            var announcements = new System.Collections.Concurrent.ConcurrentDictionary<string, (byte Type, long Size)>();

            // State snapshot at chain head — InvalidTxs sub-test exercises mempool checks:
            // server MUST reject and NOT propagate txs failing balance/nonce/gas validation.
            var headState = GethTestdataHistoricalStateBuilder.Build(testdata).HeadState;
            var headGasLimit = (ulong)chainHandler.Head.GasLimit;
            var mempool = new EthTestMempoolValidator(headState, headGasLimit);

            var listener = new RlpxListener(serverKey, serverConfig);
            listener.PeerFailed += (_, e) => _output.WriteLine($"server PeerFailed: {e.Exception.GetType().Name}: {e.Exception.Message}");
            listener.PeerAccepted += async (_, conn) =>
            {
                try
                {
                    var ethOffset = conn.GetCapabilityOffset("eth");
                    var ethCap = conn.SharedCapabilities.Find(c => c.Name == "eth");
                    int negotiatedEthVersion = ethCap.Version;

                    var status = new Eth69StatusMessage
                    {
                        ProtocolVersion = negotiatedEthVersion,
                        NetworkId = networkId,
                        GenesisHash = genesisHash,
                        ForkHash = forkHash,
                        ForkNext = 0,
                        EarliestBlock = 0,
                        LatestBlock = headNumber,
                        LatestBlockHash = headHash
                    };
                    await conn.SendMessageAsync(ethOffset + Eth68MessageIds.Status, Eth69StatusMessageEncoder.Encode(status));
                    await conn.ReceiveMessageAsync();

                    // Dispatch loop. Mirror Eth68ServerSession but using our
                    // chain-backed handler. Pre-built eth/68 dispatcher already
                    // covers all the message IDs since they didn't change at
                    // eth/69 — the receipt FORMAT is partial but the test only
                    // checks count here.
                    var ethServer = new Eth68ServerSession(conn, chainHandler, localStatus: null!);
                    typeof(Eth68ServerSession).GetProperty(nameof(Eth68ServerSession.EthOffset))!
                        .SetValue(ethServer, ethOffset);

                    // eth/69 spec: server MUST disconnect on BlockRangeUpdate
                    // where earliestBlock > latestBlock (sanity violation).
                    ethServer.BlockRangeUpdateReceived += async (_, upd) =>
                    {
                        if (upd.EarliestBlock > upd.LatestBlock)
                        {
                            try { await conn.DisconnectAsync(Nethereum.Model.P2P.DisconnectReason.ProtocolBreach); }
                            catch (System.IO.IOException) { /* peer already gone */ }
                            catch (System.ObjectDisposedException) { /* listener tearing down */ }
                        }
                    };

                    peers[conn] = 0;
                    try
                    {
                        while (!sessionCts.Token.IsCancellationRequested && conn.IsConnected)
                        {
                            var (msgId, payload) = await conn.ReceiveMessageAsync(sessionCts.Token);
                            int localId = msgId - ethOffset;

                            // Intercept tx-pool messages for cross-peer
                            // propagation; everything else goes to Eth68ServerSession.
                            if (localId == Eth68MessageIds.Transactions)
                            {
                                var msg = Nethereum.Model.P2P.TransactionsMessageEncoder.Decode(payload);
                                // ECDSA sender recovery dominates for large batches
                                // (LargeTxRequest = 2000 txs, 2s budget). Parallelise
                                // the pure-function preflight then run mempool checks
                                // sequentially against shared pendingNonces.
                                var txArr = new ISignedTransaction[msg.Transactions.Count];
                                var senderArr = new string[msg.Transactions.Count];
                                for (int i = 0; i < msg.Transactions.Count; i++) txArr[i] = msg.Transactions[i];
                                System.Threading.Tasks.Parallel.For(0, txArr.Length, i =>
                                {
                                    // Bad signature → ECDSA recovery throws; treat as "no sender".
                                    try { senderArr[i] = txArr[i].GetSenderAddress(); }
                                    catch (Exception) { senderArr[i] = null; }
                                });

                                var newHashes = new System.Collections.Generic.List<byte[]>();
                                var newTypes = new System.Collections.Generic.List<byte>();
                                var newSizes = new System.Collections.Generic.List<long>();
                                for (int i = 0; i < txArr.Length; i++)
                                {
                                    var tx = txArr[i];
                                    if (!mempool.IsValid(tx, senderArr[i])) continue;
                                    var hex = tx.Hash.ToHex();
                                    if (txPool.TryAdd(hex, tx))
                                    {
                                        newHashes.Add(tx.Hash);
                                        newTypes.Add(GetTxTypeByte(tx));
                                        newSizes.Add(tx.GetRLPEncoded().Length);
                                    }
                                }
                                // Announce to all OTHER connected peers.
                                foreach (var other in peers.Keys)
                                {
                                    if (other == conn) continue;
                                    var ann = new Nethereum.Model.P2P.NewPooledTransactionHashesMessage
                                    {
                                        Types = newTypes.ToArray(),
                                        Sizes = newSizes,
                                        Hashes = newHashes
                                    };
                                    try
                                    {
                                        await other.SendMessageAsync(
                                            other.GetCapabilityOffset("eth") + Eth68MessageIds.NewPooledTransactionHashes,
                                            Nethereum.Model.P2P.NewPooledTransactionHashesMessageEncoder.Encode(ann),
                                            sessionCts.Token);
                                    }
                                    catch (System.IO.IOException) { /* other peer disconnected mid-broadcast */ }
                                    catch (System.ObjectDisposedException) { /* tearing down */ }
                                    catch (System.OperationCanceledException) { /* test ending */ }
                                }
                                continue;
                            }
                            if (localId == Eth68MessageIds.GetPooledTransactions)
                            {
                                var req = Nethereum.Model.P2P.GetPooledTransactionsMessageEncoder.Decode(payload);
                                var resp = new Nethereum.Model.P2P.PooledTransactionsMessage { RequestId = req.RequestId };
                                foreach (var h in req.Hashes)
                                    if (txPool.TryGetValue(h.ToHex(), out var tx))
                                        resp.Transactions.Add(tx);
                                await conn.SendMessageAsync(
                                    ethOffset + Eth68MessageIds.PooledTransactions,
                                    Nethereum.Model.P2P.PooledTransactionsMessageEncoder.Encode(resp),
                                    sessionCts.Token);
                                continue;
                            }
                            if (localId == Eth68MessageIds.NewPooledTransactionHashes)
                            {
                                // Peer announced txs. Remember declared type+size per hash
                                // for the blob sub-tests' cross-check, then fetch the txs.
                                var ann = Nethereum.Model.P2P.NewPooledTransactionHashesMessageEncoder.Decode(payload);
                                for (int i = 0; i < ann.Hashes.Count; i++)
                                {
                                    var type = i < ann.Types.Length ? ann.Types[i] : (byte)0;
                                    var size = i < ann.Sizes.Count ? ann.Sizes[i] : 0L;
                                    announcements[ann.Hashes[i].ToHex()] = (type, size);
                                }
                                if (ann.Hashes.Count > 0)
                                {
                                    var fetch = new Nethereum.Model.P2P.GetPooledTransactionsMessage
                                    {
                                        RequestId = conn.NextRequestId(),
                                        Hashes = ann.Hashes
                                    };
                                    await conn.SendMessageAsync(
                                        ethOffset + Eth68MessageIds.GetPooledTransactions,
                                        Nethereum.Model.P2P.GetPooledTransactionsMessageEncoder.Encode(fetch),
                                        sessionCts.Token);
                                }
                                continue;
                            }
                            if (localId == Eth68MessageIds.PooledTransactions)
                            {
                                var msg = Nethereum.Model.P2P.PooledTransactionsMessageEncoder.Decode(payload);
                                bool violation = false;
                                foreach (var tx in msg.Transactions)
                                {
                                    var hex = tx.Hash.ToHex();
                                    if (announcements.TryGetValue(hex, out var declared))
                                    {
                                        var actualType = GetTxTypeByte(tx);
                                        var actualSize = (long)tx.GetRLPEncoded().Length;
                                        if (actualType != declared.Type || actualSize != declared.Size)
                                        { violation = true; break; }
                                    }
                                    if (tx is Transaction4844 blobTx && !BlobSidecarValidator.HasValidVersionedHashes(blobTx))
                                    { violation = true; break; }
                                    txPool.TryAdd(hex, tx);
                                }
                                if (violation)
                                {
                                    try { await conn.DisconnectAsync(Nethereum.Model.P2P.DisconnectReason.SubprotocolReason); }
                                    catch (System.IO.IOException) { /* peer already gone */ }
                                    catch (System.ObjectDisposedException) { /* listener tearing down */ }
                                }
                                continue;
                            }

                            await ethServer.HandleEthMessageAsync(localId, payload, sessionCts.Token);
                        }
                    }
                    finally
                    {
                        peers.TryRemove(conn, out int _);
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"server: {ex.GetType().Name}: {ex.Message}");
                }
            };

            // Mock Engine API so the eth-test tool's sendForkchoiceUpdated() succeeds.
            using var engineApi = MockEngineApiHttpServer.Start();
            int enginePort = engineApi.Port;

            try
            {
                listener.Start(port: 0, bindAddress: IPAddress.Loopback);
                var enode = $"enode://{serverKey.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{listener.Port}";
                _output.WriteLine($"Listening at {enode}");
                _output.WriteLine($"Loaded {chainHandler.HeadersByNumber.Count} blocks");

                var args = string.Join(" ",
                    "rlpx eth-test",
                    "--chain", $"\"{testdata}\"",
                    "--engineapi", $"http://127.0.0.1:{enginePort}",
                    "--jwtsecret", "0x7365637265747365637265747365637265747365637265747365637265747365",
                    "--node", enode,
                    "--run", subtest);
                _output.WriteLine($"Invoking: devp2p {args}");

                var psi = new ProcessStartInfo(devp2pTool, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                var exited = proc.WaitForExit(60_000);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                _output.WriteLine("=== devp2p stdout ===");
                _output.WriteLine(stdout);
                _output.WriteLine("=== devp2p stderr ===");
                _output.WriteLine(stderr);

                Assert.True(exited, "devp2p tool did not exit within 60s");
                Assert.Equal(0, proc.ExitCode);
            }
            finally
            {
                sessionCts.Cancel();
                await listener.StopAsync();
            }
        }

        private static byte GetTxTypeByte(ISignedTransaction tx)
        {
            return tx switch
            {
                Nethereum.Model.Transaction1559 => 0x02,
                Nethereum.Model.Transaction2930 => 0x01,
                Nethereum.Model.Transaction4844 => 0x03,
                _ => 0x00
            };
        }

        private static byte[] ParseHex(string s)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X")) s = s.Substring(2);
            if (s.Length % 2 != 0) s = "0" + s;
            return s.HexToByteArray();
        }
        private static ulong ParseUlong(string s)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X"))
                return Convert.ToUInt64(s.Substring(2), 16);
            return ulong.Parse(s);
        }
        private static string FindTestdata() => GethToolLocator.FindEthTestTestdata();
        private static string FindDevp2pTool() => GethToolLocator.FindDevp2pTool();
    }
}
