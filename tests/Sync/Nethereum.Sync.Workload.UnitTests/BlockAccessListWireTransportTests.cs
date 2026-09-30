using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Chain.TestData.Vectors;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class BlockAccessListWireTransportTests
    {
        private readonly ITestOutputHelper _out;

        public BlockAccessListWireTransportTests(ITestOutputHelper @out) => _out = @out;

        [Fact]
        public async Task Given_ASnap2PeerHoldingBlockAccessLists_When_TheyAreFetchedOverTheWire_Then_TheBytesReceivedMatchTheProducers()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 10, hardfork: "amsterdam");
            await new WorkloadV1().BuildAsync(sequencer);

            await using var server = await WireServerNode.StartAsync(sequencer, advertiseSnap2: true);
            var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(server.GenesisHash, server.NetworkId, advertiseSnap2: true),
                new PeerPoolOptions(TargetPeerCount: 1, MinPeerLatestBlock: 0));
            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(server.Enode);
            try
            {
                var session = await WaitForSessionAsync(pool);
                _out.WriteLine($"negotiated capabilities: {session.CapabilitiesDescription}");
                Assert.Contains("snap/2", session.CapabilitiesDescription);

                var (blockHash, expectedRlp) = await FindNonEmptyBlockAsync(sequencer);
                Assert.NotNull(expectedRlp);

                var serverOwnCopy = await server.Bundle.BlockAccessLists.GetByBlockHashAsync(blockHash);
                _out.WriteLine(
                    $"server's OWN bundle.BlockAccessLists holds {(serverOwnCopy?.Length ?? -1)} bytes for this block " +
                    $"(producer holds {expectedRlp.Length} bytes) — {(serverOwnCopy != null && serverOwnCopy.SequenceEqual(expectedRlp) ? "MATCH: the server genuinely has the real data" : "the server does NOT have the real data — a harness gap, not a protocol defect")}");
                Assert.True(
                    serverOwnCopy != null && serverOwnCopy.SequenceEqual(expectedRlp),
                    "harness gap: WireServerNode's own bundle never received the real block access list — fix the harness before blaming the wire");

                var response = await session.GetBlockAccessListsAsync(
                    new System.Collections.Generic.List<byte[]> { blockHash }, responseBytes: 1_000_000, CancellationToken.None);
                var fetchedRlp = response.BlockAccessListsByBlock.Single();

                _out.WriteLine(
                    $"snap/2 GetBlockAccessLists returned {fetchedRlp.Length} bytes for a block whose producer BAL is " +
                    $"{expectedRlp.Length} bytes, and whose SERVER already correctly holds it — " +
                    $"{(fetchedRlp.Length == expectedRlp.Length && fetchedRlp.SequenceEqual(expectedRlp) ? "MATCH" : "MISMATCH")}");

                Assert.True(
                    fetchedRlp.Length > 0 && fetchedRlp.SequenceEqual(expectedRlp),
                    "the bytes fetched over snap/2 differ from the block access list the producer holds" +
                    "(src/Nethereum.ChainNode.Hosting/ChainNodeServeListener.cs:119-134) constructs for every real ChainNode.StartAsync " +
                    "listener — has no IBlockAccessListStore in its constructor at all, so GetBlockAccessListsCoreAsync " +
                    "(src/Nethereum.DevP2P.Sync/Serving/PatriciaSnapRequestHandler.cs:316-333) can only ever answer every snap/2 " +
                    "GetBlockAccessLists request with an empty placeholder, even though (proven above) the server's own bundle holds the real data.");
            }
            finally
            {
                await pool.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_AnEth71PeerHoldingBlockAccessLists_When_TheyAreFetchedOverTheWire_Then_TheBytesReceivedMatchTheProducers()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 10, hardfork: "amsterdam");
            await new WorkloadV1().BuildAsync(sequencer);

            await using var server = await WireServerNode.StartAsync(sequencer);
            var (pool, _) = await SnapLoadTests.ConnectAsync(server);
            try
            {
                var session = pool.ActivePeers.OfType<SyncPeerSession>().First();
                _out.WriteLine($"negotiated capabilities: {session.CapabilitiesDescription}, EthVersion={session.EthVersion}");
                Assert.True(session.EthVersion >= 71, $"peer negotiated eth/{session.EthVersion}, need eth/71+");

                var height = (long)await sequencer.Blocks.GetHeightAsync();
                var nonEmptyHashes = new System.Collections.Generic.List<byte[]>();
                var expectedByHash = new System.Collections.Generic.Dictionary<string, byte[]>();
                for (var n = 1L; n <= height && nonEmptyHashes.Count < 10; n++)
                {
                    var hash = await sequencer.Blocks.GetHashByNumberAsync(n);
                    var rlp = await sequencer.BlockAccessLists.GetByBlockHashAsync(hash);
                    if (rlp == null || rlp.Length == 0) continue;
                    nonEmptyHashes.Add(hash);
                    expectedByHash[hash.ToHex()] = rlp;
                }
                Assert.True(nonEmptyHashes.Count > 0, "the workload produced no non-empty block access lists — the gate would be vacuous");

                var serverOwnCopy = await server.Bundle.BlockAccessLists.GetByBlockHashAsync(nonEmptyHashes[0]);
                Assert.True(
                    serverOwnCopy != null && serverOwnCopy.SequenceEqual(expectedByHash[nonEmptyHashes[0].ToHex()]),
                    "harness gap: WireServerNode's own bundle never received the real block access list — fix the harness before blaming the wire");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                System.Collections.Generic.List<System.Collections.Generic.List<AccountChanges>> decodedLists = null;
                var ex = await Record.ExceptionAsync(async () =>
                    decodedLists = await session.GetBlockAccessListsAsync(nonEmptyHashes, CancellationToken.None));
                sw.Stop();
                Assert.True(
                    ex == null,
                    "the bytes fetched over eth/71 differ from the block access list the producer holds" +
                    "the client correctly negotiated and declared eth/71 (EthVersion=" + session.EthVersion + " above), yet the request " +
                    $"failed after {sw.ElapsedMilliseconds} ms with {ex?.GetType().FullName}: {ex?.Message}. Root cause: PeerListener." +
                    "HandleSessionAsync (src/Nethereum.DevP2P.Sync/Serving/PeerListener.cs:116-235) performs its own eth Status exchange " +
                    "and constructs Eth68ServerSession (:202) but never calls Eth68ServerSession.ExchangeStatusAsync " +
                    "(src/Nethereum.DevP2P.Sync/Serving/Eth68ServerSession.cs:56-70), so RemoteStatus (:29) stays permanently null on every " +
                    "real inbound connection. The GetBlockAccessLists dispatch case (:115-122) treats RemoteStatus==null as eth/<71 and " +
                    "calls ProtocolBreachAsync (:214-219) -> DisconnectAsync(ProtocolBreach) (:223) unconditionally, regardless of what the " +
                    "peer actually negotiated.");

                Assert.Equal(nonEmptyHashes.Count, decodedLists.Count);

                for (var i = 0; i < nonEmptyHashes.Count; i++)
                {
                    var header = await sequencer.Blocks.GetByHashAsync(nonEmptyHashes[i]);
                    var fetchedHash = BlockAccessListRLPEncoder.Current.Hash(decodedLists[i]);
                    Assert.Equal(header.BlockAccessListHash.ToHex(), fetchedHash.ToHex());

                    var expectedRlp = expectedByHash[nonEmptyHashes[i].ToHex()];
                    var fetchedRlp = BlockAccessListRLPEncoder.Current.Encode(decodedLists[i]);
                    Assert.True(fetchedRlp.SequenceEqual(expectedRlp));
                }

                _out.WriteLine($"eth/71 GetBlockAccessLists verified {nonEmptyHashes.Count} non-empty block access lists against the header hash and the producer");
            }
            finally
            {
                await pool.DisposeAsync();
            }
        }

        [Fact]
        public async Task Given_APeerNegotiatingSnap1_When_BlockAccessListsAreRequested_Then_ItIsRefusedRatherThanAnsweredWrongly()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 10, hardfork: "amsterdam");
            await new WorkloadV1().BuildAsync(sequencer);

            await using var server = await WireServerNode.StartAsync(sequencer);
            var (pool, _) = await SnapLoadTests.ConnectAsync(server);
            try
            {
                var session = pool.ActivePeers.OfType<SyncPeerSession>().First();
                Assert.DoesNotContain("snap/2", session.CapabilitiesDescription);

                var (blockHash, expectedRlp) = await FindNonEmptyBlockAsync(sequencer);
                Assert.NotNull(expectedRlp);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var ex = await Record.ExceptionAsync(() => session.GetBlockAccessListsAsync(
                    new System.Collections.Generic.List<byte[]> { blockHash }, responseBytes: 1_000_000, CancellationToken.None));
                sw.Stop();

                _out.WriteLine(ex == null
                    ? "no exception — request completed"
                    : $"{ex.GetType().FullName} after {sw.ElapsedMilliseconds} ms: {ex.Message}");

                if (ex == null)
                {
                    _out.WriteLine(
                        "DEFECT: SyncPeerSession.GetBlockAccessListsAsync(List<byte[]>, ulong, CancellationToken) " +
                        "(src/Nethereum.DevP2P.Sync/Peering/SyncPeerSession.cs:757-772) carries no snap-version guard " +
                        "— unlike GetSnapOffsetForTrieNodesOrThrow's guard for GetTrieNodes on snap/2, or GetSnapOffsetOrThrow's " +
                        "SupportsSnap check every other snap method uses. A snap/1-only peer is silently answered rather than refused.");
                }

                Assert.True(ex != null, "expected the snap/1 peer's request to be refused; it was answered instead — see the diagnostic line above");
            }
            finally
            {
                await pool.DisposeAsync();
            }
        }

        private static async Task<SyncPeerSession> WaitForSessionAsync(PeerPoolManager pool)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                var session = pool.ActivePeers.OfType<SyncPeerSession>().FirstOrDefault();
                if (session != null) return session;
                await Task.Delay(100);
            }
            throw new TimeoutException("no peer connected within 30s");
        }

        private static async Task<(byte[] BlockHash, byte[] Rlp)> FindNonEmptyBlockAsync(InProcessSequencerDriver sequencer)
        {
            var height = (long)await sequencer.Blocks.GetHeightAsync();
            for (var n = 1L; n <= height; n++)
            {
                var hash = await sequencer.Blocks.GetHashByNumberAsync(n);
                var rlp = await sequencer.BlockAccessLists.GetByBlockHashAsync(hash);
                if (rlp != null && rlp.Length > 0) return (hash, rlp);
            }
            return (null, null);
        }
    }
}
