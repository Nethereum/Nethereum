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
            var userAuthentication = UserAuthentication.FromUrl("https://test:123@localhost:8545");
            Assert.NotNull(userAuthentication);
            Assert.Equal("test", userAuthentication.UserName);
            Assert.Equal("123", userAuthentication.Password);
            var header = userAuthentication.GetBasicAuthenticationHeaderValue();

            Assert.Equal("dGVzdDoxMjM=", header.Parameter);
            Assert.Equal("Basic", header.Scheme);

        }

        [Fact]
        public void ShouldBeAbleToAuthenticateUsingBasicScheme()
        {
        //    IClient client = new RpcClient(new Uri(""));
        //    var ethGetBlockNumber = new Eth.Blocks.EthBlockNumber(client);
        //    var number = ethGetBlockNumber.SendRequestAsync().Result;
        }
    }
}