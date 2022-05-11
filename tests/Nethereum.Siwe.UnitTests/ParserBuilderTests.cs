using System.Collections.Generic;
using Nethereum.Siwe.Core;
using Xunit;

namespace Nethereum.Siwe.UnitTests
{
    public class ParserBuilderTests
    {
        [Fact]
        public void ShouldParseAndBuildWith2OptionalFields()
        {
            var message = "service.org wants you to sign in with your Ethereum account:\n0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2\n\nI accept the ServiceOrg Terms of Service: https://service.org/tos\n\nURI: https://service.org/login\nVersion: 1\nChain ID: 1\nNonce: 32891757\nIssued At: 2021-09-30T16:25:24.000Z\nResources:\n- ipfs://Qme7ss3ARVgxv6rXqVPiikMJ8u2NLgmgszg13pYrDKEoiu\n- https://example.com/my-web2-claim.json";
            var domain = "service.org";
            var address = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
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


        [Fact]
        public void ShouldParseAndBuildWithNoOptionalFields()
        {
            var message = "service.org wants you to sign in with your Ethereum account:\n0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2\n\nI accept the ServiceOrg Terms of Service: https://service.org/tos\n\nURI: https://service.org/login\nVersion: 1\nChain ID: 1\nNonce: 32891757\nIssued At: 2021-09-30T16:25:24.000Z";
            var domain = "service.org";
            var address = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
            var statement = "I accept the ServiceOrg Terms of Service: https://service.org/tos";
            var uri = "https://service.org/login";
            var version = "1";
            var chainId = "1";
            var nonce = "32891757";
            var issuedAt = "2021-09-30T16:25:24.000Z";


            var decodedMessage = SiweMessageParser.Parse(message);

            Assert.Equal(domain, decodedMessage.Domain);
            Assert.Equal(address, decodedMessage.Address);
            Assert.Equal(statement, decodedMessage.Statement);
            Assert.Equal(uri, decodedMessage.Uri);
            Assert.Equal(version, decodedMessage.Version);
            Assert.Equal(chainId, decodedMessage.ChainId);
            Assert.Equal(nonce, decodedMessage.Nonce);
            Assert.Equal(issuedAt, decodedMessage.IssuedAt);


            var decodedMessage2 = SiweMessageParser.ParseUsingAbnf(message);

            Assert.Equal(domain, decodedMessage2.Domain);
            Assert.Equal(address, decodedMessage2.Address);
            Assert.Equal(statement, decodedMessage2.Statement);
            Assert.Equal(uri, decodedMessage2.Uri);
            Assert.Equal(version, decodedMessage2.Version);
            Assert.Equal(chainId, decodedMessage2.ChainId);
            Assert.Equal(nonce, decodedMessage2.Nonce);
            Assert.Equal(issuedAt, decodedMessage2.IssuedAt);
 

            var builtMessage = SiweMessageStringBuilder.BuildMessage(decodedMessage2);
            Assert.Equal(message, builtMessage);
        }


        [Fact]
        public void ShouldParseAndBuildTimestampWithoutMicroseconds()
        {
            var message = "service.org wants you to sign in with your Ethereum account:\n0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2\n\nI accept the ServiceOrg Terms of Service: https://service.org/tos\n\nURI: https://service.org/login\nVersion: 1\nChain ID: 1\nNonce: 32891757\nIssued At: 2021-09-30T16:25:24Z";
            var domain = "service.org";
            var address = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
            var statement = "I accept the ServiceOrg Terms of Service: https://service.org/tos";
            var uri = "https://service.org/login";
            var version = "1";
            var chainId = "1";
            var nonce = "32891757";
            var issuedAt = "2021-09-30T16:25:24Z";


            var decodedMessage = SiweMessageParser.Parse(message);

            Assert.Equal(domain, decodedMessage.Domain);
            Assert.Equal(address, decodedMessage.Address);
            Assert.Equal(statement, decodedMessage.Statement);
            Assert.Equal(uri, decodedMessage.Uri);
            Assert.Equal(version, decodedMessage.Version);
            Assert.Equal(chainId, decodedMessage.ChainId);
            Assert.Equal(nonce, decodedMessage.Nonce);
            Assert.Equal(issuedAt, decodedMessage.IssuedAt);


            var decodedMessage2 = SiweMessageParser.ParseUsingAbnf(message);

            Assert.Equal(domain, decodedMessage2.Domain);
            Assert.Equal(address, decodedMessage2.Address);
            Assert.Equal(statement, decodedMessage2.Statement);
            Assert.Equal(uri, decodedMessage2.Uri);
            Assert.Equal(version, decodedMessage2.Version);
            Assert.Equal(chainId, decodedMessage2.ChainId);
            Assert.Equal(nonce, decodedMessage2.Nonce);
            Assert.Equal(issuedAt, decodedMessage2.IssuedAt);


            var builtMessage = SiweMessageStringBuilder.BuildMessage(decodedMessage2);
            Assert.Equal(message, builtMessage);
        }

        [Fact]
        public void ShouldParseAndBuildWithDomainRFC3986AndUserInfo()
        {
            var message = "test@127.0.0.1 wants you to sign in with your Ethereum account:\n0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2\n\nI accept the ServiceOrg Terms of Service: https://service.org/tos\n\nURI: https://service.org/login\nVersion: 1\nChain ID: 1\nNonce: 32891757\nIssued At: 2021-09-30T16:25:24.000Z";
            var domain = "test@127.0.0.1";
            var address = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
            var statement = "I accept the ServiceOrg Terms of Service: https://service.org/tos";
            var uri = "https://service.org/login";
            var version = "1";
            var chainId = "1";
            var nonce = "32891757";
            var issuedAt = "2021-09-30T16:25:24.000Z";


            var decodedMessage = SiweMessageParser.Parse(message);

            Assert.Equal(domain, decodedMessage.Domain);
            Assert.Equal(address, decodedMessage.Address);
            Assert.Equal(statement, decodedMessage.Statement);
            Assert.Equal(uri, decodedMessage.Uri);
            Assert.Equal(version, decodedMessage.Version);
            Assert.Equal(chainId, decodedMessage.ChainId);
            Assert.Equal(nonce, decodedMessage.Nonce);
            Assert.Equal(issuedAt, decodedMessage.IssuedAt);


            var decodedMessage2 = SiweMessageParser.ParseUsingAbnf(message);

            Assert.Equal(domain, decodedMessage2.Domain);
            Assert.Equal(address, decodedMessage2.Address);
            Assert.Equal(statement, decodedMessage2.Statement);
            Assert.Equal(uri, decodedMessage2.Uri);
            Assert.Equal(version, decodedMessage2.Version);
            Assert.Equal(chainId, decodedMessage2.ChainId);
            Assert.Equal(nonce, decodedMessage2.Nonce);
            Assert.Equal(issuedAt, decodedMessage2.IssuedAt);


            var builtMessage = SiweMessageStringBuilder.BuildMessage(decodedMessage2);
            Assert.Equal(message, builtMessage);
        }

        [Fact]
        public void ShouldParseAndBuildWithDomainRFC3986AndPort()
        {
            var message = "127.0.0.1:8080 wants you to sign in with your Ethereum account:\n0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2\n\nI accept the ServiceOrg Terms of Service: https://service.org/tos\n\nURI: https://service.org/login\nVersion: 1\nChain ID: 1\nNonce: 32891757\nIssued At: 2021-09-30T16:25:24.000Z";
            var domain = "127.0.0.1:8080";
            var address = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
            var statement = "I accept the ServiceOrg Terms of Service: https://service.org/tos";
            var uri = "https://service.org/login";
            var version = "1";
            var chainId = "1";
            var nonce = "32891757";
            var issuedAt = "2021-09-30T16:25:24.000Z";


            var decodedMessage = SiweMessageParser.Parse(message);

            Assert.Equal(domain, decodedMessage.Domain);
            Assert.Equal(address, decodedMessage.Address);
            Assert.Equal(statement, decodedMessage.Statement);
            Assert.Equal(uri, decodedMessage.Uri);
            Assert.Equal(version, decodedMessage.Version);
            Assert.Equal(chainId, decodedMessage.ChainId);
            Assert.Equal(nonce, decodedMessage.Nonce);
            Assert.Equal(issuedAt, decodedMessage.IssuedAt);


            var decodedMessage2 = SiweMessageParser.ParseUsingAbnf(message);

            Assert.Equal(domain, decodedMessage2.Domain);
            Assert.Equal(address, decodedMessage2.Address);
            Assert.Equal(statement, decodedMessage2.Statement);
            Assert.Equal(uri, decodedMessage2.Uri);
            Assert.Equal(version, decodedMessage2.Version);
            Assert.Equal(chainId, decodedMessage2.ChainId);
            Assert.Equal(nonce, decodedMessage2.Nonce);
            Assert.Equal(issuedAt, decodedMessage2.IssuedAt);


            var builtMessage = SiweMessageStringBuilder.BuildMessage(decodedMessage2);
            Assert.Equal(message, builtMessage);
        }

        [Fact]
        public void ShouldParseAndBuildWithDomainRFC3986WithUserInfoAndPort()
        {
            var message = "test@127.0.0.1:8080 wants you to sign in with your Ethereum account:\n0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2\n\nI accept the ServiceOrg Terms of Service: https://service.org/tos\n\nURI: https://service.org/login\nVersion: 1\nChain ID: 1\nNonce: 32891757\nIssued At: 2021-09-30T16:25:24.000Z";
            var domain = "test@127.0.0.1:8080";
            var address = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
            var statement = "I accept the ServiceOrg Terms of Service: https://service.org/tos";
            var uri = "https://service.org/login";
            var version = "1";
            var chainId = "1";
            var nonce = "32891757";
            var issuedAt = "2021-09-30T16:25:24.000Z";


            var decodedMessage = SiweMessageParser.Parse(message);

            Assert.Equal(domain, decodedMessage.Domain);
            Assert.Equal(address, decodedMessage.Address);
            Assert.Equal(statement, decodedMessage.Statement);
            Assert.Equal(uri, decodedMessage.Uri);
            Assert.Equal(version, decodedMessage.Version);
            Assert.Equal(chainId, decodedMessage.ChainId);
            Assert.Equal(nonce, decodedMessage.Nonce);
            Assert.Equal(issuedAt, decodedMessage.IssuedAt);


            var decodedMessage2 = SiweMessageParser.ParseUsingAbnf(message);

            Assert.Equal(domain, decodedMessage2.Domain);
            Assert.Equal(address, decodedMessage2.Address);
            Assert.Equal(statement, decodedMessage2.Statement);
            Assert.Equal(uri, decodedMessage2.Uri);
            Assert.Equal(version, decodedMessage2.Version);
            Assert.Equal(chainId, decodedMessage2.ChainId);
            Assert.Equal(nonce, decodedMessage2.Nonce);
            Assert.Equal(issuedAt, decodedMessage2.IssuedAt);


            var builtMessage = SiweMessageStringBuilder.BuildMessage(decodedMessage2);
            Assert.Equal(message, builtMessage);
        }

        [Fact]
        public void ShouldParseAndBuildWithAllOptionalFields()
        {
            var message = "test@127.0.0.1:8080 wants you to sign in with your Ethereum account:\n0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2\n\nI accept the ServiceOrg Terms of Service: https://service.org/tos\n\nURI: https://service.org/login\nVersion: 1\nChain ID: 1\nNonce: 32891757\nIssued At: 2021-09-30T16:25:24.000Z\nExpiration Time: 2021-09-30T16:25:24.000Z\nNot Before: 2021-09-30T16:25:24.000Z\nRequest ID: 200\nResources:\n- ipfs://Qme7ss3ARVgxv6rXqVPiikMJ8u2NLgmgszg13pYrDKEoiu\n- https://example.com/my-web2-claim.json";
            var domain = "test@127.0.0.1:8080";
            var address = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
            var statement = "I accept the ServiceOrg Terms of Service: https://service.org/tos";
            var uri = "https://service.org/login";
            var version = "1";
            var chainId = "1";
            var nonce = "32891757";
            var issuedAt = "2021-09-30T16:25:24.000Z";
            var expirationTime = "2021-09-30T16:25:24.000Z";
            var notBefore = "2021-09-30T16:25:24.000Z";
            var requestId = "200";
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
            Assert.Equal(expirationTime, decodedMessage.ExpirationTime);
            Assert.Equal(notBefore, decodedMessage.NotBefore);
            Assert.Equal(requestId, decodedMessage.RequestId);

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
            Assert.Equal(expirationTime, decodedMessage2.ExpirationTime);
            Assert.Equal(notBefore, decodedMessage2.NotBefore);
            Assert.Equal(requestId, decodedMessage2.RequestId);
            Assert.Equal(resource1, decodedMessage2.Resources[0]);
            Assert.Equal(resource2, decodedMessage2.Resources[1]);


            var builtMessage = SiweMessageStringBuilder.BuildMessage(decodedMessage2);
            Assert.Equal(message, builtMessage);
        }

    }
}