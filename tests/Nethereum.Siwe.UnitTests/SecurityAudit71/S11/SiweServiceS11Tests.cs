using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Signer;
using Nethereum.Siwe.Core;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Siwe.UnitTests.SecurityAudit71.S11
{
    public class SiweServiceS11Tests
    {
        private const string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        private static readonly string Address = "0x12890d2cce102216644c59daE5baed380d84830c".ConvertToEthereumChecksumAddress();
        private const string Erc1271Magic = "0x1626ba7e00000000000000000000000000000000000000000000000000000000";

        private sealed class ChainClient : ClientBase
        {
            private readonly string _chainIdHex;
            private readonly string _callResult;

            public ChainClient(string chainIdHex, string callResult)
            {
                _chainIdHex = chainIdHex;
                _callResult = callResult;
            }

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null)
            {
                if (rpcRequestMessage.Method == "eth_chainId")
                    return Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, _chainIdHex));
                if (rpcRequestMessage.Method == "eth_call")
                    return Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, _callResult));
                return Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, "0x0"));
            }

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
            {
                throw new NotSupportedException();
            }
        }

        private static SiweMessage NewMessage(string chainId = "1")
        {
            var message = new SiweMessage
            {
                Domain = "login.xyz",
                Address = Address,
                Statement = "Sign in",
                Uri = "https://login.xyz",
                ChainId = chainId
            };
            message.SetExpirationTime(DateTime.UtcNow.AddHours(1));
            return message;
        }

        private static string Sign(string text)
        {
            return new EthereumMessageSigner().EncodeUTF8AndSign(text, new EthECKey(PrivateKey));
        }

        [Fact]
        public async Task Given_AValidSignedMessage_When_TheSameMessageAndSignatureAreSubmittedTwice_Then_TheSecondSubmissionIsRejected()
        {
            var service = new SiweMessageService(new InMemorySessionNonceStorage());
            var message = NewMessage();
            var signature = Sign(service.BuildMessageToSign(message));

            Assert.True(await service.IsValidMessage(message, signature));
            Assert.False(await service.IsValidMessage(message, signature));
        }

        [Fact]
        public async Task Given_AContractWalletOnAnotherChainThanTheMessageChainId_When_TheSignatureIsChecked_Then_ItIsRejected()
        {
            var web3 = new Nethereum.Web3.Web3(new ChainClient("0x89", Erc1271Magic));
            var service = new SiweMessageService(new InMemorySessionNonceStorage(), null, web3);
            var message = NewMessage("1");
            service.BuildMessageToSign(message);
            var otherKeySignature = new EthereumMessageSigner().EncodeUTF8AndSign(SiweMessageStringBuilder.BuildMessage(message), new EthECKey("0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f"));

            Assert.False(await service.IsMessageSignatureValid(message, otherKeySignature));
        }

        [Fact]
        public async Task Given_AContractWalletOnTheMessageChain_When_ItsNon65ByteSignatureIsChecked_Then_TheContractAnswerDecides()
        {
            var web3 = new Nethereum.Web3.Web3(new ChainClient("0x1", Erc1271Magic));
            var service = new SiweMessageService(new InMemorySessionNonceStorage(), null, web3);
            var message = NewMessage("1");
            service.BuildMessageToSign(message);
            var contractWalletSignature = "0x" + new string('a', 200);

            Assert.True(await service.IsMessageSignatureValid(message, contractWalletSignature));
        }

        [Fact]
        public async Task Given_AnErc6492WrappedSignature_When_TheSignatureIsChecked_Then_TheUniversalValidatorAnswerDecides()
        {
            var web3 = new Nethereum.Web3.Web3(new ChainClient("0x1", "0x0000000000000000000000000000000000000000000000000000000000000001"));
            var service = new SiweMessageService(new InMemorySessionNonceStorage(), null, web3);
            var message = NewMessage("1");
            service.BuildMessageToSign(message);
            var inner = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(Sign("inner"));
            var wrapped = new Nethereum.Contracts.Standards.ERC6492.ERC6492Service(web3.Eth).BuildSignature(Address, new byte[] { 1, 2, 3 }, inner);

            Assert.True(await service.IsMessageSignatureValid(message, Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(wrapped, true)));
        }

        [Theory]
        [InlineData("Statement")]
        [InlineData("RequestId")]
        [InlineData("Resource")]
        public void Given_AFieldContainingALineFeed_When_TheMessageIsBuilt_Then_BuildingIsRefused(string field)
        {
            var message = NewMessage();
            message.Nonce = "abcdefgh12";
            message.IssuedAt = "2021-01-01T00:00:00.000Z";
            const string injected = "x\nExpiration Time: 2099-01-01T00:00:00.000Z";
            if (field == "Statement") message.Statement = injected;
            if (field == "RequestId") message.RequestId = injected;
            if (field == "Resource") message.Resources = new System.Collections.Generic.List<string> { "https://a.example/" + injected };
            message.Version = "1";

            Assert.ThrowsAny<Exception>(() => SiweMessageStringBuilder.BuildMessage(message));
        }

        [Fact]
        public void Given_ARfc3339ExpirationWithNanosecondPrecision_When_TheWindowIsChecked_Then_NoFormatExceptionEscapes()
        {
            var text = "login.xyz wants you to sign in with your Ethereum account:\n" + Address +
                       "\n\nSign in\n\nURI: https://login.xyz\nVersion: 1\nChain ID: 1\nNonce: abcdefgh12\n" +
                       "Issued At: 2021-01-01T00:00:00.000Z\nExpiration Time: 2099-01-01T00:00:00.123456789Z";
            var parsed = SiweMessageParser.Parse(text);

            Assert.False(parsed.HasMessageDateExpired());
        }

        [Fact]
        public async Task Given_AnEoaSignatureWithTrailingBytes_When_TheSignatureIsChecked_Then_ItIsRejected()
        {
            var service = new SiweMessageService(new InMemorySessionNonceStorage());
            var message = NewMessage();
            var signature = Sign(service.BuildMessageToSign(message));

            Assert.False(await service.IsMessageSignatureValid(message, signature + "00"));
        }

        [Fact]
        public void Given_AnExpiredPendingSession_When_AnAddHappensAfterThePruneInterval_Then_TheExpiredSessionIsRemoved()
        {
            var storage = new InMemorySessionNonceStorage(TimeSpan.FromMilliseconds(300));
            var expiring = NewMessage();
            expiring.Nonce = "expiring1";
            expiring.SetExpirationTime(DateTime.UtcNow.AddMilliseconds(50));
            var third = NewMessage();
            third.Nonce = "third0001";
            storage.AddOrUpdate(expiring);

            System.Threading.Thread.Sleep(400);
            storage.AddOrUpdate(third);

            Assert.Null(storage.GetSiweMessage(expiring));
            Assert.NotNull(storage.GetSiweMessage(third));
        }
    }
}
