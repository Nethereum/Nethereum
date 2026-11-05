using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Nethereum.Siwe.Core;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Siwe.UnitTests.SecurityAudit71.S14.Round6
{
    public class SiweDateReadingRound6S14Tests
    {
        private static readonly string Address = "0x12890d2cce102216644c59daE5baed380d84830c".ConvertToEthereumChecksumAddress();

        private static SiweMessage Parsed(string expiration = null, string notBefore = null, string issuedAt = "2021-01-01T00:00:00Z")
        {
            var message = new SiweMessage
            {
                Domain = "login.xyz", Address = Address, Uri = "https://login.xyz", Version = "1", ChainId = "1",
                Nonce = "abcdefgh", IssuedAt = issuedAt, ExpirationTime = expiration, NotBefore = notBefore
            };
            return SiweMessageParser.Parse(SiweMessageStringBuilder.BuildMessage(message));
        }

        private static string Eip(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        private static string Round(DateTime utc) => utc.ToString("o", CultureInfo.InvariantCulture);

        private static T UnderCulture<T>(string culture, Func<T> action)
        {
            var thread = Thread.CurrentThread;
            var previous = thread.CurrentCulture;
            var previousUi = thread.CurrentUICulture;
            try
            {
                thread.CurrentCulture = new CultureInfo(culture);
                thread.CurrentUICulture = new CultureInfo(culture);
                return action();
            }
            finally
            {
                thread.CurrentCulture = previous;
                thread.CurrentUICulture = previousUi;
            }
        }

        [Theory]
        [InlineData("2030-01-01T00:00:00Z")]
        [InlineData("2030-01-01T00:00:00.000Z")]
        [InlineData("2030-01-01T00:00:00.1234567Z")]
        [InlineData("2030-01-01T05:30:00+05:30")]
        [InlineData("2029-12-31T16:00:00-08:00")]
        [InlineData("2030-01-01T00:00:00.0000000+00:00")]
        [Trait("Rule", "S14-R6-1")]
        public void Given_EipFormTimesInEveryOffsetShape_When_Read_Then_TheUtcInstantIsExactAndKindIsUtc(string time)
        {
            var message = Parsed(time);
            var truth = DateTimeOffset.Parse(time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).UtcDateTime;

            var read = message.GetExpirationTimeAsDateTime();

            Assert.Equal(truth, read);
            Assert.Equal(DateTimeKind.Utc, read.Kind);
            Assert.False(message.HasMessageDateExpired());
        }

        [Theory]
        [InlineData("tr-TR")]
        [InlineData("ar-SA")]
        [InlineData("th-TH")]
        [InlineData("fa-IR")]
        [InlineData("he-IL")]
        [InlineData("ja-JP")]
        [Trait("Rule", "S14-R6-2")]
        public void Given_NonInvariantCurrentCultures_When_TheWindowIsChecked_Then_EveryReadIsInvariant(string culture)
        {
            var now = DateTime.UtcNow;
            var cases = new[]
            {
                (Expiration: Eip(now.AddMinutes(10)), NotBefore: Eip(now.AddMinutes(-10)), Active: true),
                (Expiration: Round(now.AddMinutes(-10)), NotBefore: Round(now.AddMinutes(-20)), Active: false),
                (Expiration: Eip(now.AddMinutes(20)), NotBefore: Eip(now.AddMinutes(10)), Active: false),
                (Expiration: "2030-01-01T05:30:00+05:30", NotBefore: "2020-12-31T16:00:00-08:00", Active: true),
                (Expiration: "2030-01-01T00:00:00.000Z", NotBefore: "2000-01-01T00:00:00.000Z", Active: true)
            };

            foreach (var c in cases)
            {
                var message = Parsed(c.Expiration, c.NotBefore);
                var inCulture = UnderCulture(culture, () => new
                {
                    Active = message.HasMessageDateStartedAndNotExpired(),
                    Expiration = message.GetExpirationTimeAsDateTime(),
                    NotBefore = message.GetNotBeforeAsDateTime(),
                    Issued = message.GetIssuedAtAsDateTime()
                });

                var invariant = new
                {
                    Active = message.HasMessageDateStartedAndNotExpired(),
                    Expiration = message.GetExpirationTimeAsDateTime(),
                    NotBefore = message.GetNotBeforeAsDateTime(),
                    Issued = message.GetIssuedAtAsDateTime()
                };

                Assert.Equal(invariant, inCulture);
                Assert.Equal(c.Active, inCulture.Active);
            }
        }

        [Fact]
        [Trait("Rule", "S14-R6-3")]
        public void Given_ExpiryAndNotBeforeAroundNow_When_TheWindowIsChecked_Then_TheBoundaryFallsOnTheRightSide()
        {
            var now = DateTime.UtcNow;

            Assert.True(Parsed(Round(now.AddSeconds(3))).HasMessageDateStartedAndNotExpired());
            Assert.False(Parsed(Round(now.AddSeconds(-3))).HasMessageDateStartedAndNotExpired());
            Assert.False(Parsed(null, Round(now.AddSeconds(3))).HasMessageDateStartedAndNotExpired());
            Assert.True(Parsed(null, Round(now.AddSeconds(-3))).HasMessageDateStartedAndNotExpired());
            Assert.True(Parsed(Eip(now.AddSeconds(3)), Eip(now.AddSeconds(-3))).HasMessageDateStartedAndNotExpired());
            Assert.False(Parsed(Eip(now.AddSeconds(-3)), Eip(now.AddSeconds(-30))).HasMessageDateStartedAndNotExpired());
        }

        [Theory]
        [InlineData("2030-01-01T00:00:60Z")]
        [InlineData("99999-01-01T00:00:00Z")]
        [InlineData("0000-01-01T00:00:00Z")]
        [InlineData("2030-02-31T00:00:00Z")]
        [InlineData("2030-01-01t00:00:00Z")]
        [InlineData("2030-01-01T00:00:00z")]
        [Trait("Rule", "S14-R6-4")]
        public void Given_UnrepresentableTimesTheGrammarAccepts_When_Read_Then_OnlyAFormatExceptionEscapes(string time)
        {
            var message = Parsed(time);

            Assert.IsType<FormatException>(Record.Exception(() => message.GetExpirationTimeAsDateTime()));
            Assert.IsType<FormatException>(Record.Exception(() => message.HasMessageDateExpired()));
            Assert.IsType<FormatException>(Record.Exception(() => Parsed(null, time).HasMessageDateStarted()));
        }

        [Theory]
        [InlineData("2030-01-01T00:00:00.123456789Z", "2030-01-01T00:00:00.1234567Z")]
        [InlineData("2030-01-01T00:00:00.12345678+05:30", "2029-12-31T18:30:00.1234567Z")]
        [Trait("Rule", "S14-R6-4")]
        public void Given_ATimeWithMoreThanSevenFractionalDigits_When_Read_Then_ItIsAcceptedAndTruncatedToSevenDigits(string time, string truncatedUtc)
        {
            var expected = DateTime.ParseExact(truncatedUtc, "o", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

            Assert.Equal(expected, Parsed(time).GetExpirationTimeAsDateTime());
            Assert.False(Parsed(time).HasMessageDateExpired());
            Assert.False(Parsed(null, time).HasMessageDateStarted());
        }

        [Theory]
        [InlineData("0001-01-01T00:00:00+23:59")]
        [InlineData("0001-01-01T00:00:00.0000000+14:00")]
        [InlineData("9999-12-31T23:59:59-23:59")]
        [InlineData("9999-12-31T23:59:59.9999999Z")]
        [InlineData("9999-12-31T23:59:59+00:00")]
        [InlineData("0001-01-01T00:00:00Z")]
        [Trait("Rule", "S14-R6-4")]
        public void Given_InstantsAtTheEdgesOfTheDateTimeRange_When_Read_Then_NothingButAFormatExceptionOrAnArgumentExceptionEscapes(string time)
        {
            var message = Parsed(time);

            var thrown = Record.Exception(() => message.HasMessageDateExpired());
            var thrownStart = Record.Exception(() => Parsed(null, time).HasMessageDateStarted());

            Assert.True(thrown == null || thrown is FormatException || thrown is ArgumentException, thrown?.GetType().Name);
            Assert.True(thrownStart == null || thrownStart is FormatException || thrownStart is ArgumentException, thrownStart?.GetType().Name);
        }

        public static TheoryData<string> HostileTimes() => new TheoryData<string>
        {
            "2030-01-01T00:00:60Z", "99999-01-01T00:00:00Z", "0000-01-01T00:00:00Z", "2030-02-31T00:00:00Z",
            "2030-01-01t00:00:00z", "2030-01-01T00:00:00.123456789Z", "0001-01-01T00:00:00+23:59",
            "9999-12-31T23:59:59-23:59", "12345678901234567890-01-01T00:00:00Z", "2030-01-01T00:00:00+23:59",
            "2030-06-31T12:00:00Z", "2030-04-31T12:00:00Z", "2100-02-29T12:00:00Z", "2030-01-01T23:59:60.5Z",
            "2030-01-01T00:00:00." + "1234567890123456789012345678901234567890" + "Z"
        };

        [Theory]
        [MemberData(nameof(HostileTimes))]
        [Trait("Rule", "S14-R6-5")]
        public void Given_HostileTokensThroughTheSameCallsGetUserAsyncMakes_When_Validated_Then_OnlyArgumentOrFormatExceptionsEscape(string time)
        {
            var storage = new InMemorySessionNonceStorage();
            var service = new SiweMessageService(storage);

            foreach (var variant in new[] { (time, (string)null, "2021-01-01T00:00:00Z"), (null, time, "2021-01-01T00:00:00Z"), (null, null, time) })
            {
                var text = SiweMessageStringBuilder.BuildMessage(new SiweMessage
                {
                    Domain = "login.xyz", Address = Address, Uri = "https://login.xyz", Version = "1", ChainId = "1",
                    Nonce = "abcdefgh", IssuedAt = variant.Item3, ExpirationTime = variant.Item1, NotBefore = variant.Item2
                });
                Exception thrown = null;
                try
                {
                    var parsed = SiweMessageParser.Parse(text);
                    storage.AddOrUpdate(parsed);
                    var current = service.IsMessageTheSameAsSessionStored(parsed) && service.HasMessageDateStartedAndNotExpired(parsed);
                    Assert.True(current || !current);
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }

                Assert.True(thrown == null || thrown is ArgumentException || thrown is FormatException,
                    $"{time}: {thrown?.GetType().FullName}");
            }
        }

        [Fact]
        [Trait("Rule", "S14-R6-5")]
        public void Given_HostileFieldValuesInOtherFields_When_ParsedAndValidated_Then_OnlyArgumentOrFormatExceptionsEscape()
        {
            var storage = new InMemorySessionNonceStorage();
            var service = new SiweMessageService(storage);
            var random = new Random(66);
            var seen = new HashSet<string>();
            var valid = SiweMessageStringBuilder.BuildMessage(new SiweMessage
            {
                Domain = "login.xyz", Address = Address, Uri = "https://login.xyz", Version = "1", ChainId = "1",
                Nonce = "abcdefgh", IssuedAt = "2021-01-01T00:00:00Z", ExpirationTime = "2099-01-01T00:00:00Z"
            });
            var pieces = new[] { "\n", "\r", ": ", "0x", "99999999999999999999999999999999", "-", "\0", "é", "Z", "(", "?", "#", " ", "9", "a", "T" };

            for (var i = 0; i < 6000; i++)
            {
                var text = valid;
                for (var k = random.Next(1, 4); k > 0; k--)
                {
                    var position = random.Next(text.Length + 1);
                    text = random.Next(3) == 0 && position < text.Length
                        ? text.Remove(position, 1)
                        : text.Insert(position, pieces[random.Next(pieces.Length)]);
                }

                try
                {
                    var parsed = SiweMessageParser.Parse(text);
                    storage.AddOrUpdate(parsed);
                    service.IsMessageTheSameAsSessionStored(parsed);
                    service.HasMessageDateStartedAndNotExpired(parsed);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
                {
                }
                catch (Exception ex)
                {
                    seen.Add(ex.GetType().FullName);
                }
            }

            Assert.Empty(seen);
        }

        [Fact]
        [Trait("Rule", "S14-R6-6")]
        public void Given_AnEmptyDomainOrEmptyUri_When_Parsed_Then_TheMessageIsRejected()
        {
            var header = "login.xyz wants you to sign in with your Ethereum account:\n" + Address + "\n\n\nURI: https://login.xyz";
            var tail = "\nVersion: 1\nChain ID: 1\nNonce: abcdefgh\nIssued At: 2021-01-01T00:00:00Z";

            Assert.ThrowsAny<ArgumentException>(() => SiweMessageParser.Parse(" wants you to sign in with your Ethereum account:\n" + Address + "\n\n\nURI: https://login.xyz" + tail));
            Assert.ThrowsAny<ArgumentException>(() => SiweMessageParser.Parse("login.xyz wants you to sign in with your Ethereum account:\n" + Address + "\n\n\nURI: " + tail));
            Assert.NotNull(SiweMessageParser.Parse(header + tail));
        }
    }
}
