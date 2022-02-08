using Nethereum.Signer;
using Nethereum.Siwe.Core;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Siwe.UnitTests
{
    public class SignMessageTests
    {

        [Fact]
        public void ShouldValidateSignature()
        {
            var domain = "login.xyz";
            var address = "0xb8a316ea8a9e48ebd25b73c71bc0f22f5c337d1f";
            var statement = "Sign-In With Ethereum Example Statement";
            var uri = "https://login.xyz";
            var version = "1";
            var chainId = "1";
            var nonce = "uolthxpe";
            var issuedAt = "2021-11-25T02:36:37.013Z";
            var signature = "0x6eabbdf0861ca83b6cf98381dcbc3db16dffce9a0449dc8b359718d13b0093c3285b6dea7e84ad1aa4871b63899319a988ddf39df3080bcdc60f68dd0942e8221c";
            var message =
                "login.xyz wants you to sign in with your Ethereum account:\n0xb8a316ea8a9e48ebd25b73c71bc0f22f5c337d1f\n\nSign-In With Ethereum Example Statement\n\nURI: https://login.xyz\nVersion: 1\nChain ID: 1\nNonce: uolthxpe\nIssued At: 2021-11-25T02:36:37.013Z";
            var siweMessage = new SiweMessage();
            siweMessage.Domain = domain;
            siweMessage.Address = address;
            siweMessage.Statement = statement;
            siweMessage.Uri = uri;
            siweMessage.Version = version;
            siweMessage.ChainId = chainId;
            siweMessage.Nonce = nonce;
            siweMessage.IssuedAt = issuedAt;
            siweMessage.Signature = signature;
         

            var builtMessage = SiweMessageStringBuilder.BuildMessage(siweMessage);
            Assert.Equal(message, builtMessage);
            var messageSigner = new EthereumMessageSigner();
            var accountRecovered = messageSigner.EncodeUTF8AndEcRecover(builtMessage, signature);
            Assert.True(accountRecovered.IsTheSameAddress(address));
        } 
    }
}