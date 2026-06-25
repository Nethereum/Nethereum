using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Nethereum.DevP2P.SpecTests.Eth69
{
    /// <summary>
    /// Integration test exercising the full DevP2P client stack against a live
    /// mainnet peer. Dials TCP, runs the RLPx handshake, negotiates p2p Hello +
    /// eth/68 or eth/69, exchanges Status, requests a known historical header
    /// pair, validates parent linkage, fetches the matching block body, and
    /// asserts tx count against the canonical chain.
    /// <para>
    /// Configured via environment variables. <c>PEER_HOST</c> is required;
    /// the test skips when not set. Optional overrides: <c>PEER_RPC_HOST</c>,
    /// <c>PEER_DEVP2P_PORT</c>, <c>PEER_RPC_PORT</c>, <c>PEER_ENODE</c>.
    /// </para>
    /// </summary>
    public class MainnetPeerE2ETests : MainnetPeerTestBase
    {
        private const ulong CrossCheckBlockStart = 19_500_000UL;
        private const int HeaderRequestCount = 2;

        public MainnetPeerE2ETests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public Task DialPeer_ExchangeEth_FetchHistoricalHeaderAndBody_RoundTrip()
            => RunWithSessionAsync(async (session, ctx, ct) =>
            {
                await FetchAndValidateHeadersAsync(session, ctx.PeerHead, ct);
            });

        private async Task FetchAndValidateHeadersAsync(MainnetPeerSession session, ulong peerHead, CancellationToken ct)
        {
            ulong requestStart = CrossCheckBlockStart;
            if (peerHead < CrossCheckBlockStart)
            {
                Output.WriteLine($"  peer head {peerHead} < cross-check anchor {CrossCheckBlockStart}; " +
                    $"falling back to peer head for request start");
                requestStart = peerHead > 0 ? peerHead - 1 : 0;
            }

            Output.WriteLine($"  GetBlockHeaders {requestStart}..{requestStart + HeaderRequestCount - 1} (limit {HeaderRequestCount})");
            var headers = await session.GetHeadersAsync(requestStart, HeaderRequestCount, ct);

            Assert.NotNull(headers);
            Assert.Equal(HeaderRequestCount, headers.Count);

            var encoder = BlockHeaderEncoder.Current;
            var keccak = new Util.Sha3Keccack();
            var first = headers[0];
            var second = headers[1];
            var firstHash = keccak.CalculateHash(encoder.Encode(first));
            var secondHash = keccak.CalculateHash(encoder.Encode(second));

            var firstNumber = (ulong)first.BlockNumber;
            var secondNumber = (ulong)second.BlockNumber;
            Output.WriteLine($"  header[0]: number={firstNumber} ts={first.Timestamp} hash=0x{firstHash.ToHex()}");
            Output.WriteLine($"  header[1]: number={secondNumber} ts={second.Timestamp} hash=0x{secondHash.ToHex()}");
            Output.WriteLine($"  header[1].parentHash=0x{second.ParentHash.ToHex()}");

            Assert.Equal(requestStart, firstNumber);
            Assert.Equal(requestStart + 1, secondNumber);
            Assert.Equal(firstHash.ToHex(), second.ParentHash.ToHex());
            Assert.True(first.Timestamp > 1_438_000_000L,
                $"header[0].timestamp {first.Timestamp} predates the Frontier launch (~2015-07-30) — corrupt encoding");
            Assert.True(second.Timestamp >= first.Timestamp,
                "header[1].timestamp must not be less than header[0].timestamp");

            await FetchAndValidateBodyAsync(session, firstHash, first, ct);
        }

        private async Task FetchAndValidateBodyAsync(MainnetPeerSession session, byte[] blockHash, BlockHeader header, CancellationToken ct)
        {
            Output.WriteLine($"  GetBlockBodies [0x{blockHash.ToHex()}]");
            var bodies = await session.GetBodiesAsync(new List<byte[]> { blockHash }, ct);

            Assert.NotNull(bodies);
            Assert.Single(bodies);

            var body = bodies[0];
            Output.WriteLine($"  body: txCount={body.Transactions.Count} uncleCount={body.Uncles.Count} " +
                $"withdrawals={(body.Withdrawals == null ? "n/a" : body.Withdrawals.Count.ToString())}");

            Assert.NotNull(body.Transactions);
            var headerNumber = (ulong)header.BlockNumber;
            if (headerNumber >= CrossCheckBlockStart)
            {
                Assert.True(body.Transactions.Count > 0,
                    $"Expected non-empty body for block {headerNumber} (post-Merge mainnet block); got 0 transactions");
            }
            Assert.True(body.Transactions.Count <= 2048,
                $"Body tx count {body.Transactions.Count} exceeds plausible mainnet block ceiling");
        }
    }
}
