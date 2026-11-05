using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Model.UnitTests.Oracle
{
    [Trait("Requires", "Externals")]
    public class StreamingDecoderDifferentialTests
    {
        private static readonly Lazy<DifferentialCorpus> Corpus = new Lazy<DifferentialCorpus>(DifferentialCorpus.Load);
        private readonly ITestOutputHelper _output;
        private readonly DifferentialHarness _harness;

        public StreamingDecoderDifferentialTests(ITestOutputHelper output)
        {
            _output = output;
            _harness = new DifferentialHarness(output);
        }

        [Fact]
        public void Given_EveryTransactionInTheLocalCorpora_When_DecodedWithTheStreamingDecoder_Then_ItEqualsTheTreeOracleOrIsRefused()
        {
            var corpus = Corpus.Value;
            _output.WriteLine("files=" + corpus.Files + " blocks=" + corpus.Blocks + " gethTestdataBlocks=" + corpus.GethBlocks +" uniqueTransactions=" + corpus.Transactions.Count + " uniqueHeaders=" + corpus.Headers.Count);
            Assert.True(corpus.Transactions.Count > 1000, "corpus too small: " + corpus.Transactions.Count);

            foreach (var allowBlob in new[] { false, true })
            {
                var outcome = new DifferentialHarness.Outcome();
                _harness.Run(corpus.Transactions,
                    bytes => TreeDecodeOracle.DecodeTransaction(bytes, allowBlob),
                    bytes => TransactionFactory.CreateTransaction(bytes, allowBlob),
                    CompareTransactions, outcome);
                DifferentialHarness.AssertEquivalent(outcome, _harness.Report("transactions allowBlobWrapper=" + allowBlob, outcome));
            }
        }

        private static string CompareTransactions(ISignedTransaction expected, ISignedTransaction actual, byte[] input)
        {
            var difference = ObjectComparer.FirstDifference(expected, actual);
            if (difference != null) return difference;
            if (expected.TransactionType != actual.TransactionType) return "type";
            if (!actual.GetRLPEncoded().SequenceEqual(input) && !(actual is Transaction4844)) return "actual does not keep the wire bytes";
            if (!(expected is Transaction4844) && !expected.GetRLPEncoded().SequenceEqual(input)) return DifferentialHarness.NonCanonical;
            if (!expected.Hash.SequenceEqual(actual.Hash)) return "hash 0x" + expected.Hash.ToHex() + " != 0x" + actual.Hash.ToHex();
            if (!expected.RawHash.SequenceEqual(actual.RawHash)) return "rawHash";
            return null;
        }

        [Fact]
        public void Given_EveryInvalidRlpVectorOfTheEthereumTests_When_DecodedAsATransactionOrHeaderOrMessage_Then_ItIsRefused()
        {
            var external = DifferentialCorpus.ExternalRoot();
            Assert.NotNull(external);
            var path = Path.Combine(external, "ethereum-tests", "RLPTests", "invalidRLPTest.json");
            Assert.True(File.Exists(path), "missing " + path);

            var vectors = new List<byte[]>();
            using (var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (var test in document.RootElement.EnumerateObject())
                    vectors.Add(test.Value.GetProperty("out").GetString().HexToByteArray());
            }

            Assert.True(vectors.Count >= 26, "vectors " + vectors.Count);
            foreach (var vector in vectors)
            {
                Assert.ThrowsAny<Exception>(() => TransactionFactory.CreateTransaction(vector, true));
                Assert.ThrowsAny<Exception>(() => TreeDecodeOracle.DecodeTransaction(vector, true));
                Assert.ThrowsAny<Exception>(() => BlockHeaderEncoder.Current.Decode(vector));
                Assert.ThrowsAny<Exception>(() => TreeDecodeOracle.DecodeHeader(vector));
                Assert.ThrowsAny<Exception>(() => Nethereum.Model.P2P.TransactionsMessageEncoder.Decode(vector));
                Assert.ThrowsAny<Exception>(() => Nethereum.Model.P2P.PooledTransactionsMessageEncoder.Decode(vector));
                Assert.ThrowsAny<Exception>(() => Nethereum.Model.P2P.BlockHeadersMessageEncoder.Decode(vector));
            }
        }

        [Fact]
        public void Given_EveryBlockHeaderInTheLocalCorpora_When_DecodedWithTheStreamingDecoder_Then_ItEqualsTheTreeOracleOrIsRefused()
        {
            var corpus = Corpus.Value;
            Assert.True(corpus.Headers.Count > 1000, "corpus too small: " + corpus.Headers.Count);

            var outcome = new DifferentialHarness.Outcome();
            _harness.Run(corpus.Headers,
                TreeDecodeOracle.DecodeHeader,
                bytes => BlockHeaderEncoder.Current.Decode(bytes),
                (expected, actual, input) =>
                {
                    var difference = ObjectComparer.FirstDifference(expected, actual);
                    if (difference != null) return difference;
                    var left = BlockHeaderEncoder.Current.Encode(expected);
                    var right = BlockHeaderEncoder.Current.Encode(actual);
                    if (!left.SequenceEqual(right)) return "re-encoding differs";
                    return right.SequenceEqual(input) ? null : DifferentialHarness.NonCanonical;
                },
                outcome);
            DifferentialHarness.AssertEquivalent(outcome, _harness.Report("headers", outcome));
        }
    }
}
