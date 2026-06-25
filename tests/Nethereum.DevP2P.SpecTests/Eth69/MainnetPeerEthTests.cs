using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Nethereum.DevP2P.SpecTests.Eth69
{
    /// <summary>
    /// eth/68 + eth/69 protocol-coverage tests against a live mainnet peer.
    /// Each test exercises one specific request/response pair documented in
    /// devp2p/caps/eth.md and validates the wire-format round-trip against
    /// canonical mainnet data fetched via the peer's JSON-RPC endpoint.
    /// </summary>
    public class MainnetPeerEthTests : MainnetPeerTestBase
    {
        private const ulong AnchorBlock = 19_500_000UL;
        private const int AnchorBlockTxCount = 364;

        public MainnetPeerEthTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public Task GetReceipts_BlockWithKnownTxCount_TxCountMatches()
            => RunWithSessionAsync(async (session, ctx, ct) =>
            {
                SkipIfPeerBelow(ctx, AnchorBlock);

                var blockHash = await ResolveBlockHashAsync(ctx, AnchorBlock);
                Output.WriteLine($"  GetReceipts [0x{blockHash.ToHex()}] (eth/{session.EthVersion})");
                var receiptsByBlock = await session.GetReceiptsAsync(new List<byte[]> { blockHash }, ct);

                Assert.Single(receiptsByBlock);
                var receipts = receiptsByBlock[0];
                Output.WriteLine($"  receipts: count={receipts.Count}");
                Assert.Equal(AnchorBlockTxCount, receipts.Count);
            });

        [SkippableFact]
        public Task GetBlockHeaders_ByHash_ReturnsSameAsByNumber()
            => RunWithSessionAsync(async (session, ctx, ct) =>
            {
                SkipIfPeerBelow(ctx, AnchorBlock);

                var byNumber = await session.GetHeadersAsync(AnchorBlock, limit: 1, ct);
                Assert.Single(byNumber);
                var header = byNumber[0];

                var encoder = BlockHeaderEncoder.Current;
                var keccak = new Util.Sha3Keccack();
                var hash = keccak.CalculateHash(encoder.Encode(header));
                Output.WriteLine($"  header by number {AnchorBlock} → hash=0x{hash.ToHex()}");

                var byHash = await session.GetHeadersByHashAsync(hash, limit: 1, skip: 0, reverse: false, ct);
                Assert.Single(byHash);

                Assert.Equal((ulong)header.BlockNumber, (ulong)byHash[0].BlockNumber);
                Assert.Equal(header.ParentHash.ToHex(), byHash[0].ParentHash.ToHex());
                Assert.Equal(header.StateRoot.ToHex(), byHash[0].StateRoot.ToHex());
                Assert.Equal(header.TransactionsHash.ToHex(), byHash[0].TransactionsHash.ToHex());
                Assert.Equal(header.Timestamp, byHash[0].Timestamp);
            });

        [SkippableFact]
        public Task GetBlockHeaders_ReverseTrue_WalksBackward()
            => RunWithSessionAsync(async (session, ctx, ct) =>
            {
                const int count = 5;
                ulong head = AnchorBlock + count;
                SkipIfPeerBelow(ctx, head);

                Output.WriteLine($"  GetBlockHeaders {head} reverse=true limit={count}");
                var headers = await session.GetHeadersAsync(head, limit: count, skip: 0, reverse: true, ct);

                Assert.Equal(count, headers.Count);

                var encoder = BlockHeaderEncoder.Current;
                var keccak = new Util.Sha3Keccack();
                for (int i = 0; i < headers.Count; i++)
                {
                    var n = (ulong)headers[i].BlockNumber;
                    Output.WriteLine($"  header[{i}]: number={n}");
                    Assert.Equal(head - (ulong)i, n);
                }

                for (int i = 0; i < headers.Count - 1; i++)
                {
                    var childHash = keccak.CalculateHash(encoder.Encode(headers[i + 1]));
                    Assert.Equal(childHash.ToHex(), headers[i].ParentHash.ToHex());
                }
            });

        [SkippableFact]
        public Task GetBlockHeaders_WithSkip_ReturnsSpacedHeaders()
            => RunWithSessionAsync(async (session, ctx, ct) =>
            {
                const int count = 3;
                const ulong skip = 10;
                SkipIfPeerBelow(ctx, AnchorBlock + (skip + 1) * (count - 1));

                Output.WriteLine($"  GetBlockHeaders {AnchorBlock} skip={skip} limit={count}");
                var headers = await session.GetHeadersAsync(AnchorBlock, limit: count, skip: skip, reverse: false, ct);

                Assert.Equal(count, headers.Count);
                for (int i = 0; i < headers.Count; i++)
                {
                    var expected = AnchorBlock + (skip + 1) * (ulong)i;
                    var actual = (ulong)headers[i].BlockNumber;
                    Output.WriteLine($"  header[{i}]: number={actual} (expected {expected})");
                    Assert.Equal(expected, actual);
                }
            });

        [SkippableFact]
        public Task GetBlockBodies_MultipleHashes_ReturnsBatch()
            => RunWithSessionAsync(async (session, ctx, ct) =>
            {
                SkipIfPeerBelow(ctx, AnchorBlock + 2);

                var hashes = new List<byte[]>
                {
                    await ResolveBlockHashAsync(ctx, AnchorBlock),
                    await ResolveBlockHashAsync(ctx, AnchorBlock + 1),
                    await ResolveBlockHashAsync(ctx, AnchorBlock + 2)
                };

                Output.WriteLine($"  GetBlockBodies [{AnchorBlock}, {AnchorBlock + 1}, {AnchorBlock + 2}]");
                var bodies = await session.GetBodiesAsync(hashes, ct);

                Assert.Equal(3, bodies.Count);
                Output.WriteLine($"  body[0].txCount={bodies[0].Transactions.Count}");
                Assert.Equal(AnchorBlockTxCount, bodies[0].Transactions.Count);

                for (int i = 0; i < bodies.Count; i++)
                {
                    Assert.NotNull(bodies[i].Transactions);
                    Assert.True(bodies[i].Transactions.Count <= 2048,
                        $"body[{i}] tx count {bodies[i].Transactions.Count} exceeds plausible mainnet block ceiling");
                }
            });

        [SkippableFact]
        public Task GetBlockHeaders_AtBlockAboveOurCursor_ReturnsEmpty()
            => RunWithSessionAsync(async (session, ctx, ct) =>
            {
                ulong farFuture = ctx.PeerHead + 100_000UL;
                Output.WriteLine($"  GetBlockHeaders {farFuture} (peerHead={ctx.PeerHead})");
                var headers = await session.GetHeadersAsync(farFuture, limit: 5, ct);

                Output.WriteLine($"  headers returned: {headers.Count}");
                Assert.Empty(headers);
            });

        [SkippableFact]
        public Task GetPooledTransactions_KnownTxHash_RoundTrip()
            => RunWithSessionAsync(async (session, ctx, ct) =>
            {
                SkipIfPeerBelow(ctx, AnchorBlock);

                var block = await ctx.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                    .SendRequestAsync(new HexBigInteger(AnchorBlock));
                Skip.If(block?.TransactionHashes == null || block.TransactionHashes.Length == 0,
                    $"Block {AnchorBlock} has no transactions to sample for the pooled-tx round-trip");

                var historicalTxHash = block.TransactionHashes[0].HexToByteArray();
                Output.WriteLine($"  GetPooledTransactions [0x{historicalTxHash.ToHex()}] (block {AnchorBlock} tx[0])");
                var txs = await session.GetPooledTransactionsAsync(new List<byte[]> { historicalTxHash }, ct);

                Output.WriteLine($"  PooledTransactions returned: {txs.Count}");
                Assert.Empty(txs);
            });

        [SkippableFact]
        public Task Hello_AnnouncedCapabilities_EthVersionAtLeast68()
            => RunWithSessionAsync((session, ctx, ct) =>
            {
                var caps = session.Connection.SharedCapabilities;
                foreach (var cap in caps)
                    Output.WriteLine($"  cap: {cap.Name}/{cap.Version}");

                Assert.Contains(caps, c => c.Name == "eth" && c.Version >= 68);
                // snap/1 is offered by our outbound Hello but the peer chooses
                // whether to serve it. Archive nodes commonly omit snap because
                // it targets fast-sync clients. Surface presence/absence as
                // diagnostic output; don't assert.
                var snap = caps.Find(c => c.Name == "snap" && c.Version == 1);
                Output.WriteLine($"  snap/1 in shared capabilities: {(snap != null ? "yes" : "no (peer does not serve snap)")}");
                return Task.CompletedTask;
            });

        private void SkipIfPeerBelow(PeerContext ctx, ulong requiredHead)
        {
            Skip.If(ctx.PeerHead < requiredHead,
                $"Peer head {ctx.PeerHead} below required cross-check block {requiredHead}");
        }

        private static async Task<byte[]> ResolveBlockHashAsync(PeerContext ctx, ulong blockNumber)
        {
            var block = await ctx.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(new HexBigInteger(blockNumber));
            return block.BlockHash.HexToByteArray();
        }
    }
}
