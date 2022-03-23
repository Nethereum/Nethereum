using System;
using Nethereum.Signer;
using Nethereum.Siwe.Core;
using Xunit;

namespace Nethereum.Siwe.UnitTests
{
    public class ServiceTests
    {
        [Fact]
        public void ShouldBuildANewMessageWithANewNonceAndValidateAfterSigning()
        {
            var domain = "login.xyz";
            var address = "0x12890d2cce102216644c59daE5baed380d84830c";
            var statement = "Sign-In With Ethereum Example Statement";
            var uri = "https://login.xyz";
            var chainId = "1";
            var siweMessage = new SiweMessage();
            siweMessage.Domain = domain;
            siweMessage.Address = address;
            siweMessage.Statement = statement;
            siweMessage.Uri = uri;
            siweMessage.ChainId = chainId;
            siweMessage.SetExpirationTime(DateTime.Now.ToUniversalTime().AddHours(1));
            var service = new SiweMessageService(new SimpleNonceManagement());
            var message = service.BuildMessageToSign(siweMessage);
            var messageSigner = new EthereumMessageSigner();
            var signature = messageSigner.EncodeUTF8AndSign(message,
                new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));
            siweMessage.Signature = signature;
            Assert.True(service.HasMessageDateStartedAndNotExpired(siweMessage));
            Assert.True(service.IsMessageSessionNonceValid(siweMessage));
            Assert.True(service.IsMessageSignatureValid(siweMessage));
            Assert.True(service.IsValidMessage(siweMessage));
        }

        [Fact]
        public void ShouldBuildANewMessageWithANewNonceAndValidateAfterSigning_UsingInMemoryNonceManagement()
        {
            var domain = "login.xyz";
            var address = "0x12890d2cce102216644c59daE5baed380d84830c";
            var statement = "Sign-In With Ethereum Example Statement";
            var uri = "https://login.xyz";
            var chainId = "1";
            var siweMessage = new SiweMessage();
            siweMessage.Domain = domain;
            siweMessage.Address = address;
            siweMessage.Statement = statement;
            siweMessage.Uri = uri;
            siweMessage.ChainId = chainId;
            siweMessage.SetExpirationTime(DateTime.Now.ToUniversalTime().AddHours(1));
            var service = new SiweMessageService(new InMemoryStorageSessionNonceManagement());
            var message = service.BuildMessageToSign(siweMessage);
            var messageSigner = new EthereumMessageSigner();
            var signature = messageSigner.EncodeUTF8AndSign(message,
                new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7"));
            siweMessage.Signature = signature;
            Assert.True(service.HasMessageDateStartedAndNotExpired(siweMessage));
            Assert.True(service.IsMessageSessionNonceValid(siweMessage));
            Assert.True(service.IsMessageSignatureValid(siweMessage));
            Assert.True(service.IsValidMessage(siweMessage));
        }

    }
}