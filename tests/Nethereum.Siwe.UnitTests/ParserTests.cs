using System.Collections.Generic;
using Nethereum.Siwe.Core;
using Xunit;

namespace Nethereum.Siwe.UnitTests
{
    public class ParserTests
    {
        [Fact]
        public void ShouldParseWith2OptionalFields()
        {
            var message = "service.org wants you to sign in with your Ethereum account:\n0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2\n\nI accept the ServiceOrg Terms of Service: https://service.org/tos\n\nURI: https://service.org/login\nVersion: 1\nChain ID: 1\nNonce: 32891757\nIssued At: 2021-09-30T16:25:24.000Z\nResources:\n- ipfs://Qme7ss3ARVgxv6rXqVPiikMJ8u2NLgmgszg13pYrDKEoiu\n- https://example.com/my-web2-claim.json";
            var domain = "service.org";
            var address = "0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2";
            var statement = "I accept the ServiceOrg Terms of Service: https://service.org/tos";
            var uri = "https://service.org/login";
            var version = "1";
            var chainId = "1";
            var nonce = "32891757";
            var issuedAt = "2021-09-30T16:25:24.000Z";
            var resource1 = "ipfs://Qme7ss3ARVgxv6rXqVPiikMJ8u2NLgmgszg13pYrDKEoiu";
            var resource2 = "https://example.com/my-web2-claim.json";


            var decodedMessage = SiweMessageParser.Parse(message);
            
            Assert.Equal(domain, decodedMessage.Domain);
            Assert.Equal(address, decodedMessage.Address);
            Assert.Equal(statement, decodedMessage.Statement);
            Assert.Equal(uri, decodedMessage.Uri);
            Assert.Equal(version, decodedMessage.Version);
            Assert.Equal(chainId, decodedMessage.ChainId);
            Assert.Equal(nonce, decodedMessage.Nonce);
            Assert.Equal(issuedAt, decodedMessage.IssuedAt);
            Assert.Equal(resource1, decodedMessage.Resources[0]);
            Assert.Equal(resource2, decodedMessage.Resources[1]);

            var decodedMessage2 = SiweMessageParser.ParseUsingAbnf(message);

            Assert.Equal(domain, decodedMessage2.Domain);
            Assert.Equal(address, decodedMessage2.Address);
            Assert.Equal(statement, decodedMessage2.Statement);
            Assert.Equal(uri, decodedMessage2.Uri);
            Assert.Equal(version, decodedMessage2.Version);
            Assert.Equal(chainId, decodedMessage2.ChainId);
            Assert.Equal(nonce, decodedMessage2.Nonce);
            Assert.Equal(issuedAt, decodedMessage2.IssuedAt);
            Assert.Equal(resource1, decodedMessage2.Resources[0]);
            Assert.Equal(resource2, decodedMessage2.Resources[1]);

            var builtMessage = SiweMessageStringBuilder.BuildMessage(decodedMessage2);
            Assert.Equal(message, builtMessage);
        }
    }
}