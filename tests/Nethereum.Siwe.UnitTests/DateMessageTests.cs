using System;
using Nethereum.Siwe.Core;
using Xunit;

namespace Nethereum.Siwe.UnitTests
{
    public class DateMessageTests
    {

        [Fact]
        public void ShouldValidateDates()
        {
            var siweMessage = new SiweMessage();

            var startDate = DateTime.Now.AddDays(1);
            siweMessage.SetNotBefore(startDate);
            Assert.False(siweMessage.HasMessageDateStarted());
            startDate = DateTime.Now.AddHours(-1);
            siweMessage.SetNotBefore(startDate);
            Assert.True(siweMessage.HasMessageDateStarted());

            var expiryDate = DateTime.Now.AddDays(1);
            siweMessage.SetExpirationTime(expiryDate);
            Assert.False(siweMessage.HasMessageDateExpired());

            expiryDate = DateTime.Now.AddMinutes(-2);
            siweMessage.SetExpirationTime(expiryDate);
            Assert.True(siweMessage.HasMessageDateExpired());

            siweMessage.SetExpirationTime(DateTime.Now.AddDays(1));
            siweMessage.SetNotBefore(DateTime.Now);
            Assert.True(siweMessage.HasMessageDateStartedAndNotExpired());
        }
    }
}