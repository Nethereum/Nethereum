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
            var address = "0x9D85ca56217D2bb651b00f15e694EB7E713637D4";
            var statement = "Sign-In With Ethereum Example Statement";
            var uri = "https://login.xyz";
            var version = "1";
            var chainId = "1";
            var nonce = "bTyXgcQxn2htgkjJn";
            var issuedAt = "2022-01-27T17:09:38.578Z";
            var expirationTime = "2100-01-07T14:31:43.952Z";
            var signature = "0xdc35c7f8ba2720df052e0092556456127f00f7707eaa8e3bbff7e56774e7f2e05a093cfc9e02964c33d86e8e066e221b7d153d27e5a2e97ccd5ca7d3f2ce06cb1b";
            var message =
                "login.xyz wants you to sign in with your Ethereum account:\n0x9D85ca56217D2bb651b00f15e694EB7E713637D4\n\nSign-In With Ethereum Example Statement\n\nURI: https://login.xyz\nVersion: 1\nChain ID: 1\nNonce: bTyXgcQxn2htgkjJn\nIssued At: 2022-01-27T17:09:38.578Z\nExpiration Time: 2100-01-07T14:31:43.952Z";
            
            
            var siweMessage = new SiweMessage();
            siweMessage.Domain = domain;
            siweMessage.Address = address;
            siweMessage.Statement = statement;
            siweMessage.Uri = uri;
            siweMessage.Version = version;
            siweMessage.ChainId = chainId;
            siweMessage.Nonce = nonce;
            siweMessage.IssuedAt = issuedAt;
           
            siweMessage.ExpirationTime = expirationTime;
         

            var builtMessage = SiweMessageStringBuilder.BuildMessage(siweMessage);
            Assert.Equal(message, builtMessage);
            var messageSigner = new EthereumMessageSigner();
            var accountRecovered = messageSigner.EncodeUTF8AndEcRecover(builtMessage, signature);
            Assert.True(accountRecovered.IsTheSameAddress(address));
        }

     
    }
}