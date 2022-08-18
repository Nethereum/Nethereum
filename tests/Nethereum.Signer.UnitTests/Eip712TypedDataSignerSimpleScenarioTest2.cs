using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Xunit;
using Nethereum.ABI.EIP712;

namespace Nethereum.Signer.UnitTests
{
    public class Eip712TypedDataSignerSimpleScenarioTest2
    {
        private readonly Eip712TypedDataSigner _signer = new Eip712TypedDataSigner();

        //Message types for easier input
        [Struct("Mail")]
        public class Mail
        {
            [Parameter("string", "contents", 1)]
            public string Contents { get; set; }
        }

       
        //The generic EIP712 Typed schema defintion for this message
        public TypedData<DomainWithNameVersionAndChainId> GetMailTypedDefinition()
        {
            return new TypedData<DomainWithNameVersionAndChainId>
            {
                Domain = new DomainWithNameVersionAndChainId
                {
                    Name = "SenseNet",
                    Version = "1",
                    ChainId = 1
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(DomainWithNameVersionAndChainId), typeof(Mail)),
                PrimaryType = nameof(Mail),
            };
        }

        [Fact]
        private void ComplexMessageTypedDataEncodingShouldBeCorrectForV4IncludingArraysAndTypes()
        {
            var typedData = GetMailTypedDefinition();

            var mail = new Mail
            {
                Contents = "salut"
            };

            typedData.Domain.ChainId = 1;

            var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");

            var signature = _signer.SignTypedDataV4(mail, typedData, key);

            Assert.Equal("0xba0ae54dd4e2a9e55f67cc096af195fadf70a6b8e310fdfe253a90bc79a130e43db1e1b0fa419ae364f43a8843e889ccd89f473326141b78bdf420a80fc891d31b", signature);

            var addressRecovered = _signer.RecoverFromSignatureV4(mail, typedData, signature);
            var address = key.GetPublicAddress();

            Assert.True(address.IsTheSameAddress(addressRecovered));

        }

    }
}