using System;
using Nethereum.Siwe.Core;
using Xunit;

namespace Nethereum.Siwe.UnitTests
{
    public class SiweDateReadingTests
    {
        [Theory]
        [InlineData("2021-09-30T16:25:24Z", true)]
        [InlineData("2021-09-30T16:25:24.000Z", true)]
        [InlineData("2021-09-30T16:25:24.1234567Z", true)]
        [InlineData("2021-09-30T16:25:24.0000000+05:30", true)]
        [InlineData("2999-09-30T16:25:24Z", false)]
        public void Given_EipFormDateTimes_When_ExpiryIsChecked_Then_TheyAreReadExactly(string time, bool expired)
        {
            var message = new SiweMessage { ExpirationTime = time, NotBefore = time };

            Assert.Equal(expired, message.HasMessageDateExpired());
            Assert.Equal(expired, message.HasMessageDateStarted());
        }

        [Theory]
        [InlineData("2021-01-01T00:00:60Z")]
        [InlineData("99999-01-01T00:00:00Z")]
        [InlineData("0000-01-01T00:00:00Z")]
        [InlineData("2021-02-31T00:00:00Z")]
        [InlineData("2021-01-01t00:00:00z")]
        public void Given_DateTimesTheServiceCannotRepresent_When_ExpiryIsChecked_Then_ItFailsWithAFormatException(string time)
        {
            var message = new SiweMessage { ExpirationTime = time };

            Assert.Throws<FormatException>(() => message.HasMessageDateExpired());
        }

        [Fact]
        public void Given_BuilderProducedDateTimes_When_Read_Then_TheyRoundTrip()
        {
            var message = new SiweMessage();
            var instant = new DateTime(2030, 5, 6, 7, 8, 9, 123, DateTimeKind.Utc).AddTicks(4567);
            message.SetExpirationTime(instant);
            message.SetNotBefore(instant);

            Assert.Equal(instant, message.GetExpirationTimeAsDateTime());
            Assert.Equal(instant, message.GetNotBeforeAsDateTime());
        }
    }
}
