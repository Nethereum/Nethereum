using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    [Trait("Requires", "Externals")]
    [Collection("GethReference")]
    public class GethReferenceHarnessTests
    {
        private static GethAnswer Answer(bool ok, string canonical = null, string hash = null, string extraJson = null)
        {
            var answer = new GethAnswer { Ok = ok, Canonical = canonical, Hash = hash };
            if (extraJson != null) answer.Extra = JsonDocument.Parse(extraJson).RootElement.Clone();
            return answer;
        }

        [Fact]
        public void Given_GethAndNethereumVerdicts_When_Compared_Then_EveryKindOfDisagreementIsReported()
        {
            var accepted = new NethereumVerdict { Accepted = true, Canonical = "aa", Hash = "bb", Count = 2 };

            Assert.Equal(Agreement.AgreeAccept, DecodeComparer.Compare("tx_rlp", Answer(true, "AA", "BB", "{\"count\":2}"), accepted).Agreement);
            Assert.Equal(Agreement.AgreeRefuse, DecodeComparer.Compare("tx_rlp", Answer(false), new NethereumVerdict()).Agreement);
            Assert.Equal(Agreement.Disagree, DecodeComparer.Compare("tx_rlp", Answer(false), accepted).Agreement);
            Assert.Equal(Agreement.Disagree, DecodeComparer.Compare("tx_rlp", Answer(true, "aa", "bb"), new NethereumVerdict()).Agreement);
            Assert.Equal(Agreement.Disagree, DecodeComparer.Compare("tx_rlp", Answer(true, "ab", "bb"), accepted).Agreement);
            Assert.Equal(Agreement.Disagree, DecodeComparer.Compare("tx_rlp", Answer(true, "aa", "bc"), accepted).Agreement);
            Assert.Equal(Agreement.Disagree, DecodeComparer.Compare("tx_rlp", Answer(true, "aa", "bb", "{\"count\":3}"), accepted).Agreement);
            Assert.Equal(Agreement.Disagree, DecodeComparer.Compare("tx_rlp", Answer(true, extraJson: "{\"items_ok\":false}"), accepted).Agreement);
        }

        [Fact]
        public void Given_APolicyRefusalOfGeth_When_NethereumAcceptsIt_Then_OnlyTheDeferredRulesAreNotDisagreements()
        {
            var accepted = new NethereumVerdict { Accepted = true };
            var rangeRule = Answer(true, extraJson: "{\"policy_refuse\":\"earliest > latest (handlers.go:661)\"}");
            var zeroHash = Answer(true, extraJson: "{\"policy_refuse\":\"zero latest hash (handlers.go:661)\"}");

            var statusRange = Answer(true, extraJson: "{\"policy_refuse\":\"invalid block range in status: zero latest hash (handshake.go:93)\"}");

            Assert.Equal(Agreement.PolicyDeferred, DecodeComparer.Compare("eth_BlockRangeUpdate", rangeRule, accepted).Agreement);
            Assert.Equal(Agreement.PolicyDeferred, DecodeComparer.Compare("eth_BlockRangeUpdate", zeroHash, accepted).Agreement);
            Assert.Equal(Agreement.PolicyDeferred, DecodeComparer.Compare("eth_Status", statusRange, accepted).Agreement);
            Assert.Equal(Agreement.Disagree, DecodeComparer.Compare("eth_Transactions", zeroHash, accepted).Agreement);
            Assert.Equal(Agreement.Disagree, DecodeComparer.Compare("eth_Transactions", rangeRule, accepted).Agreement);
            Assert.Equal(Agreement.AgreeRefuse, DecodeComparer.Compare("eth_Transactions", rangeRule, new NethereumVerdict()).Agreement);
        }

        [Theory]
        [InlineData("tx_binary", "ScalarWiderThanItsFieldException: RLP_WRONGVALUE_VALUE: the scalar is 33 bytes", DecodeComparer.ClassWiderScalar)]
        [InlineData("header", "InvalidOperationException: RLP_INVALID_FIELD_OVERFLOW_33: header field 'number' is 33 bytes.", DecodeComparer.ClassWiderScalar)]
        [InlineData("header", "ArgumentException: A block header with 19 fields matches no fork this build knows.", DecodeComparer.ClassUnknownHeaderShape)]
        [InlineData("tx_binary", "TransactionFieldFormatException: RLP_INVALID_SIGNATURER: the field is empty.", DecodeComparer.ClassEmptySignature)]
        [InlineData("tx_binary", "InvalidOperationException: RLP integer has a leading zero", null)]
        public void Given_AGethAcceptedInputNethereumRefuses_When_Compared_Then_OnlyTheSignedOffClassesAreClassified(string kind, string error, string expectedClass)
        {
            var result = DecodeComparer.Compare(kind, Answer(true, "aa"), new NethereumVerdict { Error = error });

            Assert.Equal(Agreement.Disagree, result.Agreement);
            Assert.Equal(expectedClass, result.Class);
        }

        [Fact]
        public void Given_AnEmptyStringBlockAccessListEntry_When_GethRefusesItAndNethereumAccepts_Then_ItIsTheUnavailableMarkerClass()
        {
            var geth = Answer(true, extraJson: "{\"items_ok\":false}");
            geth.Error = "items: unavailable marker (empty string entry): rlp: expected List";

            var result = DecodeComparer.Compare("eth_BlockAccessLists", geth, new NethereumVerdict { Accepted = true });

            Assert.Equal(DecodeComparer.ClassUnavailableMarker, result.Class);
            Assert.Null(DecodeComparer.Compare("eth_BlockAccessLists", new GethAnswer { Ok = false, Error = "other" }, new NethereumVerdict { Accepted = true }).Class);
        }

        [Fact]
        public void Given_AFixedSeed_When_MutantsAreSampledTwice_Then_TheyAreIdenticalAndStructurallyDifferentFromTheSeed()
        {
            var tree = new ObjectGenerator(new Random(1)).Tx1559();

            var first = StructuralMutator.Sample(tree, 40, new Random(5), binary: true).Select(m => Convert.ToHexString(m.Bytes)).ToList();
            var second = StructuralMutator.Sample(tree, 40, new Random(5), binary: true).Select(m => Convert.ToHexString(m.Bytes)).ToList();

            Assert.Equal(first, second);
            Assert.DoesNotContain(Convert.ToHexString(tree.EncodeBinary()), first);
            Assert.True(first.Distinct().Count() > 30);
        }

        [Fact]
        public void Given_EveryMutationOperator_When_AppliedToAnObject_Then_EachOneIsProduced()
        {
            var tree = new ObjectGenerator(new Random(2)).Tx7702();

            var operators = StructuralMutator.Sites(tree).Select(s => s.Op).Distinct().ToList();

            foreach (var op in Enum.GetValues(typeof(MutationOp)).Cast<MutationOp>())
                Assert.Contains(op, operators);
            Assert.Equal(12, StructuralMutator.BoundaryValueCount);
        }

        [Fact]
        public void Given_GeneratedObjects_When_EncodedParsedAndEncodedAgain_Then_TheBytesAreStable()
        {
            var generator = new ObjectGenerator(new Random(3));
            foreach (var node in new[] { generator.Legacy(), generator.Header(23), generator.Body(true), generator.BlockAccessList(), generator.AccountRange() })
            {
                var bytes = node.Encode();
                Assert.True(RlpNode.TryParse(bytes, out var parsed));
                Assert.Equal(bytes, parsed.Encode());
            }
        }

        [Fact]
        public void Given_ANonCanonicalLength_When_ParsedAndEncoded_Then_ItIsPreserved()
        {
            var node = RlpNode.List(RlpNode.Str(new byte[] { 1, 2, 3 }));
            node.Items[0].Form = LengthForm.LongFormLeadingZero;
            var bytes = node.Encode();

            Assert.True(RlpNode.TryParse(bytes, out var parsed));
            Assert.Equal(LengthForm.LongFormLeadingZero, parsed.Items[0].Form);
            Assert.Equal(bytes, parsed.Encode());
        }

        [Fact]
        public void Given_TheReferenceProcess_When_DisposedAfterAQuery_Then_NoDecoderProcessOutlivesIt()
        {
            var before = Process.GetProcessesByName("geth-reference-decoder").Select(p => p.Id).ToHashSet();
            IReadOnlyList<GethAnswer> answers;
            using (var geth = new GethReferenceProcess(TimeSpan.FromSeconds(30)))
            {
                answers = geth.Query(new[] { new KeyValuePair<string, byte[]>("tx_rlp", new byte[] { 0xc0 }) });
                Assert.Single(Process.GetProcessesByName("geth-reference-decoder").Where(p => !before.Contains(p.Id)));
            }

            Assert.False(answers[0].Ok);
            Assert.Empty(Process.GetProcessesByName("geth-reference-decoder").Where(p => !before.Contains(p.Id)));
        }

        [Fact]
        public void Given_EveryGethKind_When_ListedBySubprocessAndByTheHarness_Then_TheyAreTheSameSetOrDocumentedAsUnpaired()
        {
            var toolKinds = ListToolKinds();
            var covered = NethereumDecoders.ByKind.Keys.Concat(NethereumDecoders.KindsWithoutNethereumDecoder).OrderBy(k => k).ToList();

            Assert.Equal(covered, toolKinds.OrderBy(k => k).ToList());
            Assert.Equal(InputPlanner.Kinds.OrderBy(k => k).ToList(), NethereumDecoders.ByKind.Keys.OrderBy(k => k).ToList());
        }

        private static List<string> ListToolKinds()
        {
            var path = Nethereum.Testing.TestEnvironment.Tool(GethReferenceProcess.ToolKey);
            var info = new ProcessStartInfo(path, "--kinds") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(info);
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(10000)) { process.Kill(true); throw new TimeoutException("--kinds did not finish"); }
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}
