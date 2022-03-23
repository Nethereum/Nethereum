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

            var startDate = DateTime.Now.ToUniversalTime().AddDays(1);
            siweMessage.SetNotBefore(startDate);
            Assert.False(siweMessage.HasMessageDateStarted());
            startDate = DateTime.Now.ToUniversalTime();
            siweMessage.SetNotBefore(startDate);
            Assert.True(siweMessage.HasMessageDateStarted());

            var expiryDate = DateTime.Now.ToUniversalTime().AddDays(1);
            siweMessage.SetExpirationTime(expiryDate);
            Assert.False(siweMessage.HasMessageDateExpired());

            expiryDate = DateTime.Now.ToUniversalTime();
            siweMessage.SetExpirationTime(expiryDate);
            Assert.True(siweMessage.HasMessageDateExpired());

            siweMessage.SetExpirationTime(DateTime.Now.ToUniversalTime().AddDays(1));
            siweMessage.SetNotBefore(DateTime.Now.ToUniversalTime().AddDays(-1));
            Assert.True(siweMessage.HasMessageDateStartedAndNotExpired());
        }
    }
}