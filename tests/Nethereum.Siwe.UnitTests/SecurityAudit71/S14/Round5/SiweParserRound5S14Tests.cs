using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Signer;
using Nethereum.Siwe.Core;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Siwe.UnitTests.SecurityAudit71.S14.Round5
{
    [Trait("Category", "TimingSensitive")]
    public class SiweParserRound5S14Tests
    {
        private static readonly string LS = ((char)0x2028).ToString();
        private static readonly string PS = ((char)0x2029).ToString();
        private static readonly string NEL = ((char)0x85).ToString();
        private const string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private static readonly string Address = "0x12890d2cce102216644c59daE5baed380d84830c".ConvertToEthereumChecksumAddress();
        private static readonly string Header = "login.xyz wants you to sign in with your Ethereum account:\n" + Address + "\n\n";
        private const string Fixed = "\nVersion: 1\nChain ID: 1\nNonce: abcdefgh\nIssued At: 2021-01-01T00:00:00.0000000Z";

        private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

        private static SiweMessage Valid(string statement = "ToS", string uri = "https://login.xyz")
        {
            return new SiweMessage
            {
                Domain = "login.xyz", Address = Address, Statement = statement, Uri = uri, Version = "1",
                ChainId = "1", Nonce = "abcdefgh", IssuedAt = "2021-01-01T00:00:00.0000000Z"
            };
        }

        private static string Raw(string statement, string uri) =>
            Header + statement + "\n\nURI: " + uri + Fixed;

        private static long Millis(Action action)
        {
            var clock = Stopwatch.StartNew();
            Record.Exception(action);
            return clock.ElapsedMilliseconds;
        }

        [Fact]
        [Trait("Rule", "S14-R5-1")]
        public void Given_FieldsNearTheSizeCap_When_Parsed_Then_EachCallStaysFast()
        {
            var longStatement = Header + new string('a', 50_000) + "\n\nURI: x" + Fixed;
            var longUri = Header + "\nURI: " + new string('a', 50_000) + Fixed;
            var longDomain = new string('d', 50_000) + " wants you to sign in with your Ethereum account:\n" + Address + "\n\n\nURI: x" + Fixed;
            var manyResources = Header + "\nURI: x" + Fixed + "\nResources:" + Repeat("\n- a", 10_000);
            var manyCarriageReturns = Header + Repeat("\r", 50_000) + "\n\nURI: x" + Fixed;
            var trailingNewlines = Header + "\nURI: x" + Fixed + Repeat("\r\n", 25_000);
            var trailingBlankLines = Header + "\nURI: x" + Fixed + Repeat("\n", 50_000);
            var manyLines = Header + "\nURI: x" + Fixed + "\nResources:" + Repeat("\n", 50_000);
            var separators = Header + Repeat(LS + NEL, 10_000) + "\n\nURI: x" + Fixed;

            foreach (var message in new[] { longStatement, longUri, longDomain, manyResources, manyCarriageReturns, trailingNewlines, trailingBlankLines, manyLines, separators })
            {
                Assert.True(message.Length <= SiweMessageParser.MaxMessageLength, "length " + message.Length);
                var elapsed = Millis(() => SiweMessageParser.Parse(message));
                Assert.True(elapsed < 5000, $"{message.Length} char message took {elapsed} ms");
            }
        }

        [Fact]
        [Trait("Rule", "S14-R5-1")]
        public void Given_AMessageAtAndOneBeyondTheCap_When_Parsed_Then_OnlyTheOneBeyondIsRejected()
        {
            var baseline = Header + "X\n\nURI: x" + Fixed;
            var padding = SiweMessageParser.MaxMessageLength - baseline.Length;
            var atCap = Header + new string('s', padding + 1) + "\n\nURI: x" + Fixed;
            var oneOver = Header + new string('s', padding + 2) + "\n\nURI: x" + Fixed;

            Assert.Equal(SiweMessageParser.MaxMessageLength, atCap.Length);
            Assert.NotNull(SiweMessageParser.Parse(atCap));
            Assert.ThrowsAny<ArgumentException>(() => SiweMessageParser.Parse(oneOver));
            Assert.ThrowsAny<ArgumentException>(() => SiweMessageParser.Parse(atCap + "\n"));
        }

        [Fact]
        [Trait("Rule", "S14-R5-2")]
        public void Given_StatementsThatLookLikeOtherFields_When_Parsed_Then_TheyStayInTheStatementAndNothingElseChanges()
        {
            foreach (var statement in new[] { "URI: https://evil.example", "Chain ID: 5", "Nonce: zzzzzzzz", "Version: 2", "Resources:", "- https://evil.example", "Expiration Time: 2099-01-01T00:00:00Z" })
            {
                var text = SiweMessageStringBuilder.BuildMessage(Valid(statement));

                var parsed = SiweMessageParser.Parse(text);

                Assert.Equal(statement, parsed.Statement);
                Assert.Equal("https://login.xyz", parsed.Uri);
                Assert.Equal("1", parsed.ChainId);
                Assert.Equal("abcdefgh", parsed.Nonce);
                Assert.Null(parsed.ExpirationTime);
                Assert.Null(parsed.Resources);
                Assert.Equal(text, SiweMessageStringBuilder.BuildMessage(parsed));
            }
        }

        [Fact]
        [Trait("Rule", "S14-R5-2")]
        public void Given_NewlinesSmuggledIntoDomainUriStatementOrResource_When_Parsed_Then_TheMessageIsRejectedOrKeepsTheSameFieldsItWasBuiltFrom()
        {
            var injected = new[]
            {
                Raw("ok\n\nURI: https://evil.example", "https://login.xyz"),
                Raw("ok", "https://login.xyz\nVersion: 1\nChain ID: 9"),
                "login.xyz\n0x0000000000000000000000000000000000000001\n\nx wants you to sign in with your Ethereum account:\n" + Address + "\n\n\nURI: x" + Fixed
            };

            foreach (var text in injected)
            {
                SiweMessage parsed = null;
                var thrown = Record.Exception(() => parsed = SiweMessageParser.Parse(text));
                if (thrown == null)
                {
                    Assert.NotEqual("https://evil.example", parsed.Uri);
                    Assert.NotEqual("9", parsed.ChainId);
                    Assert.Equal(Address, parsed.Address);
                }
            }
        }

        [Fact]
        [Trait("Rule", "S14-R5-2")]
        public void Given_UnicodeLineSeparatorsAndCarriageReturnsInsideFields_When_Parsed_Then_TheyNeverSplitFields()
        {
            var message = Valid("a" + LS + "URI: https://evil.example" + NEL + "b\rc", "https://login.xyz/" + LS + "x" + PS + "y");
            message.Resources = new System.Collections.Generic.List<string> { "https://a" + LS + "b" };
            var text = Raw(message.Statement, message.Uri) + "\nResources:\n- " + message.Resources[0];

            var parsed = SiweMessageParser.Parse(text);

            Assert.Equal(message.Statement, parsed.Statement);
            Assert.Equal(message.Uri, parsed.Uri);
            Assert.Equal(message.Resources, parsed.Resources);
            Assert.ThrowsAny<ArgumentException>(() => SiweMessageStringBuilder.BuildMessage(parsed));
            Assert.ThrowsAny<ArgumentException>(() => SiweMessageParser.Parse(text.Replace("\n", "\r\n")));
        }

        [Fact]
        [Trait("Rule", "S14-R5-3")]
        public void Given_MalformedOptionalFields_When_Parsed_Then_TheMessageIsRejectedInsteadOfDroppingThem()
        {
            var good = SiweMessageStringBuilder.BuildMessage(Valid());
            var variants = new[]
            {
                good + "\nExpiration Time: garbage",
                good + "\nNot Before: 2021-13-45T00:00:00Z",
                good + "\nRequest ID: has space",
                good + "\nResources:",
                good + "\nResources:\nnot-a-resource",
                good + "\nExpiration Time: 2099-01-01T00:00:00Z\nExpiration Time: 2000-01-01T00:00:00Z",
                good + "\nNot Before: 2021-01-01T00:00:00Z\nExpiration Time: 2099-01-01T00:00:00Z",
                good + "\nExtra: field"
            };

            foreach (var text in variants)
                Assert.ThrowsAny<ArgumentException>(() => SiweMessageParser.Parse(text));
        }

        [Fact]
        [Trait("Rule", "S14-R5-3")]
        public void Given_AMessageWithAnEmptyDomain_When_Parsed_Then_ItIsRejectedBecauseDomainIsMandatory()
        {
            var text = " wants you to sign in with your Ethereum account:\n" + Address + "\n\n\nURI: https://login.xyz" + Fixed;

            Assert.ThrowsAny<ArgumentException>(() => SiweMessageParser.Parse(text));
        }

        [Fact]
        [Trait("Rule", "S14-R5-3")]
        public void Given_AMessageWithAnEmptyUri_When_Parsed_Then_ItIsRejectedBecauseUriIsMandatory()
        {
            var text = Header + "\nURI: " + Fixed;

            Assert.ThrowsAny<ArgumentException>(() => SiweMessageParser.Parse(text));
        }

        [Fact]
        [Trait("Rule", "S14-R5-3")]
        public void Given_AcceptedMessages_When_CheckedForMandatoryFields_Then_NoneIsMissing()
        {
            var texts = new[]
            {
                SiweMessageStringBuilder.BuildMessage(Valid()),
                SiweMessageStringBuilder.BuildMessage(Valid(null)),
                SiweMessageStringBuilder.BuildMessage(Valid("")) + "\n\n",
                SiweMessageStringBuilder.BuildMessage(Valid()) + "\r\n"
            };

            foreach (var text in texts)
            {
                var parsed = SiweMessageParser.Parse(text);
                Assert.True(parsed.HasRequiredFields());
                Assert.False(string.IsNullOrEmpty(parsed.Uri));
            }
        }

        [Theory]
        [InlineData("2021-01-01T00:00:00.0000000Z")]
        [InlineData("2021-01-01T00:00:00Z")]
        [InlineData("2021-01-01T00:00:60.0000000Z")]
        [InlineData("2021-01-01T00:00:00.1Z")]
        [InlineData("2021-01-01t00:00:00z")]
        [InlineData("0000-01-01T00:00:00.0000000Z")]
        [InlineData("99999-01-01T00:00:00.0000000Z")]
        [InlineData("2021-01-01T00:00:00.0000000+05:30")]
        [InlineData("2021-01-01T00:00:00.0000000-23:59")]
        [InlineData("2021-02-31T00:00:00.0000000Z")]
        [Trait("Rule", "S14-R5-4")]
        public void Given_ExpirationAndNotBeforeAcceptedByTheGrammar_When_TheServiceChecksTheWindow_Then_ItIsEitherRefusedOrExactlyRight(string time)
        {
            var past = Valid();
            past.ExpirationTime = time;
            var parsedPast = SiweMessageParser.Parse(SiweMessageStringBuilder.BuildMessage(past));
            var notBefore = Valid();
            notBefore.NotBefore = time;
            var parsedNotBefore = SiweMessageParser.Parse(SiweMessageStringBuilder.BuildMessage(notBefore));

            Assert.Equal(time, parsedPast.ExpirationTime);
            Assert.Equal(time, parsedNotBefore.NotBefore);

            bool? expired = null;
            var thrown = Record.Exception(() => expired = parsedPast.HasMessageDateExpired());
            if (thrown == null)
            {
                var truth = DateTimeOffset.Parse(time, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
                Assert.Equal(DateTimeOffset.UtcNow > truth, expired);
            }
            else
            {
                Assert.IsAssignableFrom<Exception>(thrown);
            }

            bool? started = null;
            var thrownStart = Record.Exception(() => started = parsedNotBefore.HasMessageDateStarted());
            if (thrownStart == null)
            {
                var truth = DateTimeOffset.Parse(time, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
                Assert.Equal(DateTimeOffset.UtcNow > truth, started);
            }
        }

        [Fact]
        [Trait("Rule", "S14-R5-4")]
        public async Task Given_ASignedMessage_When_ParsedBackFromVariantTexts_Then_OnlyTheSignedFieldsVerify()
        {
            var service = new SiweMessageService(new InMemorySessionNonceStorage());
            var message = Valid();
            message.Nonce = null;
            var text = service.BuildMessageToSign(message);
            var signature = new EthereumMessageSigner().EncodeUTF8AndSign(text, new EthECKey(PrivateKey));

            var same = SiweMessageParser.Parse(text);
            var withTrailing = SiweMessageParser.Parse(text + "\n");
            var otherDomain = SiweMessageParser.Parse(text.Replace("login.xyz wants", "evil.xyz wants"));
            var otherChain = SiweMessageParser.Parse(text.Replace("Chain ID: 1", "Chain ID: 10"));
            var otherUri = SiweMessageParser.Parse(text.Replace("URI: https://login.xyz", "URI: https://evil.xyz"));

            Assert.True(await service.IsMessageSignatureValid(same, signature));
            Assert.True(await service.IsMessageSignatureValid(withTrailing, signature));
            Assert.False(await service.IsMessageSignatureValid(otherDomain, signature));
            Assert.False(await service.IsMessageSignatureValid(otherChain, signature));
            Assert.False(await service.IsMessageSignatureValid(otherUri, signature));
            Assert.True(service.IsMessageTheSameAsSessionStored(same));
            Assert.False(service.IsMessageTheSameAsSessionStored(otherDomain));
            Assert.False(service.IsMessageTheSameAsSessionStored(otherChain));
            Assert.False(service.IsMessageTheSameAsSessionStored(otherUri));
        }

        [Fact]
        [Trait("Rule", "S14-R5-4")]
        public async Task Given_AParsedMessageWithANonChecksumAddress_When_TheSignatureIsChecked_Then_ItFailsClosed()
        {
            var service = new SiweMessageService(new InMemorySessionNonceStorage());
            var message = Valid();
            message.Nonce = null;
            var text = service.BuildMessageToSign(message);
            var signature = new EthereumMessageSigner().EncodeUTF8AndSign(text, new EthECKey(PrivateKey));
            var lowered = SiweMessageParser.Parse(text.Replace(Address, Address.ToLowerInvariant()));

            var outcome = await Record.ExceptionAsync(async () =>
            {
                Assert.False(await service.IsMessageSignatureValid(lowered, signature));
            });

            Assert.True(outcome == null || outcome is FormatException);
        }

        [Fact]
        [Trait("Rule", "S14-R5-5")]
        public void Given_BuilderProducedMessagesOfEveryOptionalFieldCombination_When_ParsedAndRebuilt_Then_TheTextIsIdentical()
        {
            var combinations = from statement in new[] { null, "", "I accept" }
                               from expiry in new[] { null, "2030-01-01T00:00:00.0000000Z" }
                               from before in new[] { null, "2020-01-01T00:00:00.0000000+01:00" }
                               from request in new[] { null, "req-1.a_b~c" }
                               from resources in new[] { null, new[] { "https://a.example", "ipfs://Qm" } }
                               select new { statement, expiry, before, request, resources };

            foreach (var c in combinations)
            {
                var message = Valid(c.statement);
                message.ExpirationTime = c.expiry;
                message.NotBefore = c.before;
                message.RequestId = c.request;
                message.Resources = c.resources?.ToList();
                var text = SiweMessageStringBuilder.BuildMessage(message);

                var parsed = SiweMessageParser.Parse(text);

                Assert.Equal(text, SiweMessageStringBuilder.BuildMessage(parsed));
                Assert.True(parsed.HasRequiredFields());
            }
        }
    }
}
