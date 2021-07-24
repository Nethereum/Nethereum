using System;
using Nethereum.JsonRpc.Client;
using Xunit;

namespace Nethereum.RPC.UnitTests.FormatTesters
{
    public class UserAuthenticationTest
    {
        [Fact]
        public void ShouldDecodeUri()
        {
            var userAuthentication = UserAuthentication.GetBasicAuthenticationUserInfoFromUri(new Uri("https://test:123@localhost:8545"));
            Assert.NotNull(userAuthentication);
            Assert.Equal("test", userAuthentication.UserName);
            Assert.Equal("123", userAuthentication.Password);

            var header = BasicAuthenticationHeaderHelper.GetBasicAuthenticationHeaderValueFromUri(new Uri("https://test:123@localhost:8545"));

            Assert.Equal("dGVzdDoxMjM=", header.Parameter);
            Assert.Equal("Basic", header.Scheme);

        }
    }
}