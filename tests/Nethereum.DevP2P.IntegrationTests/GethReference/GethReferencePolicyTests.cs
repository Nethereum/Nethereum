using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Testing;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    [Trait("Requires", "Externals")]
    [Collection("GethReference")]
    public class GethReferencePolicyTests : IDisposable
    {
        private readonly GethReferenceProcess _geth = new GethReferenceProcess(TimeSpan.FromMinutes(5));
        private readonly ObjectGenerator _generator = new ObjectGenerator(new Random(20261028));

        public void Dispose() => _geth.Dispose();

        private (GethAnswer Geth, NethereumVerdict Nethereum) Run(string kind, RlpNode input) => Run(kind, input.Encode());

        private (GethAnswer Geth, NethereumVerdict Nethereum) Run(string kind, byte[] input)
        {
            var answer = _geth.Query(new[] { new KeyValuePair<string, byte[]>(kind, input) })[0];
            return (answer, NethereumDecoders.ByKind[kind](input));
        }

        private static bool GethRefuses(GethAnswer answer) => !answer.Ok || !answer.ItemsOk || answer.PolicyRefusal != null;

        private RlpNode DistinctLegacyTransactions(int count)
        {
            var seen = new HashSet<string>();
            var items = new List<RlpNode>();
            while (items.Count < count)
            {
                var tx = _generator.Legacy();
                if (seen.Add(Convert.ToHexString(tx.Encode()))) items.Add(tx);
            }
            return RlpNode.List(items);
        }

        [Fact]
        public void Given_ATransactionsMessageOfMoreThan5000Items_When_Decoded_Then_GethAndNethereumBothRefuseIt()
        {
            var (geth, nethereum) = Run("eth_Transactions", DistinctLegacyTransactions(5001));

            Assert.Contains("handlers.go:628", geth.PolicyRefusal);
            Assert.False(nethereum.Accepted);
        }

        [Fact]
        public void Given_ATransactionsMessageOfExactly5000Items_When_Decoded_Then_GethAndNethereumBothAcceptIt()
        {
            var (geth, nethereum) = Run("eth_Transactions", DistinctLegacyTransactions(5000));

            Assert.False(GethRefuses(geth));
            Assert.True(nethereum.Accepted, nethereum.Error);
            Assert.Equal(5000, nethereum.Count);
        }

        [Fact]
        public void Given_ABlobTransactionInATransactionsBroadcast_When_Decoded_Then_GethAndNethereumBothRefuseIt()
        {
            var (geth, nethereum) = Run("eth_Transactions", RlpNode.List(_generator.Tx4844()));

            Assert.Contains("handler_eth.go:96", geth.PolicyRefusal);
            Assert.False(nethereum.Accepted);
        }

        [Fact]
        public void Given_ABlobSidecarInsideABlockBody_When_DecodedAsBodyBlockOrBlockBodies_Then_GethAndNethereumBothRefuseIt()
        {
            var transactions = RlpNode.List(_generator.Tx4844WithSidecar(1));
            var body = RlpNode.List(transactions, RlpNode.List());
            var block = RlpNode.List(_generator.Header(15), transactions, RlpNode.List());
            var bodies = RlpNode.List(RlpNode.Int(1), RlpNode.List(body));

            foreach (var (kind, input) in new[] { ("body", body), ("block", block), ("eth_BlockBodies", bodies) })
            {
                var (geth, nethereum) = Run(kind, input);
                Assert.Contains("block_validator.go:95", geth.PolicyRefusal);
                Assert.False(nethereum.Accepted, kind);
            }
        }

        [Fact]
        public void Given_NewPooledTransactionHashesWithMismatchedFieldLengths_When_Decoded_Then_GethAndNethereumBothRefuseThem()
        {
            var announcement = RlpNode.List(
                RlpNode.Str(new byte[] { 2, 2 }),
                RlpNode.List(RlpNode.Int(100)),
                RlpNode.List(_generator.Hash(), _generator.Hash()));

            var (geth, nethereum) = Run("eth_NewPooledTransactionHashes", announcement);

            Assert.False(geth.Ok);
            Assert.False(nethereum.Accepted);
        }

        [Fact]
        public void Given_AccountRangeAndStorageRangesProofsOf129Nodes_When_Decoded_Then_GethAndNethereumBothRefuseThem()
        {
            var proof = RlpNode.List(Enumerable.Range(0, 129).Select(_ => RlpNode.Str(new byte[] { 1 })));
            var accountRange = RlpNode.List(RlpNode.Int(1), RlpNode.List(), proof);
            var storageRanges = RlpNode.List(RlpNode.Int(1), RlpNode.List(), proof);

            foreach (var (kind, input) in new[] { ("snap_AccountRange", accountRange), ("snap_StorageRanges", storageRanges) })
            {
                var (geth, nethereum) = Run(kind, input);
                Assert.False(geth.Ok, kind);
                Assert.False(nethereum.Accepted, kind);
            }
        }

        [Fact]
        public void Given_AGetTrieNodesPathsetWithoutItems_When_Decoded_Then_GethAndNethereumBothRefuseIt()
        {
            var request = RlpNode.List(RlpNode.Int(1), _generator.Hash(), RlpNode.List(RlpNode.List()), RlpNode.Int(100));

            var (geth, nethereum) = Run("snap_GetTrieNodes", request);

            Assert.False(geth.ItemsOk);
            Assert.False(nethereum.Accepted);
        }

        [Fact]
        public void Given_AccountRangeKeysThatAreNotAscending_When_VerifiedAgainstARootProof_Then_NethereumRefusesThemAtTheProofLayer()
        {
            var verifier = PatriciaRangeProofVerifier.Current;
            var low = Enumerable.Repeat((byte)1, 32).ToArray();
            var high = Enumerable.Repeat((byte)2, 32).ToArray();

            var result = verifier.Verify(new byte[32], low, new List<byte[]> { high, low }, new List<byte[]> { new byte[] { 1 }, new byte[] { 1 } }, new List<byte[]>());

            Assert.False(result.Valid);
        }

        [Theory]
        [InlineData("maxHeadersServe", "eth/protocols/eth/handler.go", 1024, nameof(Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxHeadersPerRequest))]
        [InlineData("maxBodiesServe", "eth/protocols/eth/handler.go", 1024, nameof(Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxBodiesPerRequest))]
        [InlineData("maxReceiptsServe", "eth/protocols/eth/handler.go", 1024, nameof(Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxReceiptsPerRequest))]
        [InlineData("maxBALsServe", "eth/protocols/eth/handler.go", 1024, nameof(Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxBlockAccessListsPerRequest))]
        [InlineData("maxCodeLookups", "eth/protocols/snap/handler.go", 1024, nameof(Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler.MaxCodeLookups))]
        [InlineData("maxAccessListLookups", "eth/protocols/snap/handler.go", 1024, nameof(Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler.MaxBlockAccessListLookups))]
        [InlineData("maxTrieNodeLookups", "eth/protocols/snap/handler.go", 1024, nameof(SnapServingLimits.MaxTrieNodeLookups))]
        public void Given_AServingCapPinnedToTheGethSource_When_ComparedWithNethereum_Then_TheValuesAreEqual(string constant, string file, int expected, string nethereumConstant)
        {
            var source = File.ReadAllText(Path.Combine(TestEnvironment.RequireExternalPath("GoEthereum"), file.Replace('/', Path.DirectorySeparatorChar)));
            var match = Regex.Match(source, @"\b" + constant + @"\s*=\s*(\d+)");
            Assert.True(match.Success, constant + " not found in " + file + "");
            Assert.Equal(expected, int.Parse(match.Groups[1].Value));

            var actual = new Dictionary<string, int>
            {
                [nameof(Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxHeadersPerRequest)] = Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxHeadersPerRequest,
                [nameof(Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxBodiesPerRequest)] = Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxBodiesPerRequest,
                [nameof(Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxReceiptsPerRequest)] = Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxReceiptsPerRequest,
                [nameof(Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxBlockAccessListsPerRequest)] = Nethereum.DevP2P.Sync.Serving.Eth68ServerSession.MaxBlockAccessListsPerRequest,
                [nameof(Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler.MaxCodeLookups)] = Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler.MaxCodeLookups,
                [nameof(Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler.MaxBlockAccessListLookups)] = Nethereum.DevP2P.Sync.Serving.PatriciaSnapRequestHandler.MaxBlockAccessListLookups,
                [nameof(SnapServingLimits.MaxTrieNodeLookups)] = SnapServingLimits.MaxTrieNodeLookups
            };
            Assert.Equal(expected, actual[nethereumConstant]);
        }
    }
}
