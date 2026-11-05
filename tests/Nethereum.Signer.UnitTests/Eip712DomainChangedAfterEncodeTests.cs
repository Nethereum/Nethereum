using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Documentation;
using Nethereum.Signer.EIP712;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class Eip712DomainChangedAfterEncodeTests
    {
        private const string ContractA = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC";
        private const string ContractB = "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF";

        private readonly Eip712TypedDataSigner _signer = new Eip712TypedDataSigner();
        private readonly EthECKey _key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");

        [Struct("Permit")]
        public class Permit
        {
            [Parameter("address", "owner", 1)]
            public string Owner { get; set; }

            [Parameter("address", "spender", 2)]
            public string Spender { get; set; }

            [Parameter("uint256", "value", 3)]
            public BigInteger Value { get; set; }

            [Parameter("uint256", "nonce", 4)]
            public BigInteger Nonce { get; set; }

            [Parameter("uint256", "deadline", 5)]
            public BigInteger Deadline { get; set; }
        }

        private static Permit NewPermit() => new Permit
        {
            Owner = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826",
            Spender = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB",
            Value = 1000,
            Nonce = 0,
            Deadline = 4102444800
        };

        private static Domain NewDomain(int chainId, string contract) => new Domain
        {
            Name = "Token",
            Version = "1",
            ChainId = chainId,
            VerifyingContract = contract
        };

        private static TypedData<Domain> NewTypedData(Domain domain) => new TypedData<Domain>
        {
            Domain = domain,
            Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Permit)),
            PrimaryType = nameof(Permit)
        };

        private string SignatureFor(Domain domain, Permit permit)
        {
            return _signer.SignTypedDataV4(permit, NewTypedData(domain), _key);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-domain-changed-after-encode", "A domain changed after the first encode is the domain signed")]
        public void Given_a_signed_typed_data_When_a_new_Domain_is_assigned_and_signed_again_Then_the_signature_is_for_the_new_domain()
        {
            var permit = NewPermit();
            var typedData = NewTypedData(NewDomain(1, ContractA));
            var first = _signer.SignTypedDataV4(permit, typedData, _key);

            typedData.Domain = NewDomain(11155111, ContractB);
            var second = _signer.SignTypedDataV4(permit, typedData, _key);

            var expectedNew = SignatureFor(NewDomain(11155111, ContractB), permit);
            var expectedOld = SignatureFor(NewDomain(1, ContractA), permit);
            Assert.Equal(expectedOld, first);
            Assert.NotEqual(first, second);
            Assert.Equal(expectedNew, second);

            var recoveredNew = _signer.RecoverFromSignatureV4(permit, NewTypedData(NewDomain(11155111, ContractB)), second);
            var recoveredOld = _signer.RecoverFromSignatureV4(permit, NewTypedData(NewDomain(1, ContractA)), second);
            Assert.True(_key.GetPublicAddress().Equals(recoveredNew, System.StringComparison.OrdinalIgnoreCase));
            Assert.False(_key.GetPublicAddress().Equals(recoveredOld, System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Given_a_signed_typed_data_When_ChainId_and_VerifyingContract_change_in_place_Then_the_signature_is_for_the_new_domain()
        {
            var permit = NewPermit();
            var typedData = NewTypedData(NewDomain(1, ContractA));
            var first = _signer.SignTypedDataV4(permit, typedData, _key);

            typedData.Domain.ChainId = 11155111;
            typedData.Domain.VerifyingContract = ContractB;
            var second = _signer.SignTypedDataV4(permit, typedData, _key);

            Assert.NotEqual(first, second);
            Assert.Equal(SignatureFor(NewDomain(11155111, ContractB), permit), second);

            var recoveredNew = _signer.RecoverFromSignatureV4(permit, typedData, second);
            var recoveredOld = _signer.RecoverFromSignatureV4(permit, NewTypedData(NewDomain(1, ContractA)), second);
            Assert.True(_key.GetPublicAddress().Equals(recoveredNew, System.StringComparison.OrdinalIgnoreCase));
            Assert.False(_key.GetPublicAddress().Equals(recoveredOld, System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Given_an_unchanged_domain_When_signed_repeatedly_Then_the_signatures_are_identical()
        {
            var permit = NewPermit();
            var typedData = NewTypedData(NewDomain(1, ContractA));

            var first = _signer.SignTypedDataV4(permit, typedData, _key);
            var second = _signer.SignTypedDataV4(permit, typedData, _key);
            var third = _signer.SignTypedDataV4(permit, typedData, _key);

            Assert.Equal(first, second);
            Assert.Equal(first, third);
            Assert.Equal(SignatureFor(NewDomain(1, ContractA), permit), first);
        }

        [Fact]
        public void Given_typed_data_parsed_from_json_When_hashed_Then_it_matches_the_typed_domain_encoding()
        {
            var permit = NewPermit();
            var typedData = NewTypedData(NewDomain(1, ContractA));
            typedData.SetMessage(permit);
            var json = typedData.ToJson();

            var raw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(json);
            var fromRaw = _signer.EncodeTypedDataRaw(raw);
            var fromRawAgain = _signer.EncodeTypedDataRaw(raw);
            var fromTyped = _signer.EncodeTypedData(permit, NewTypedData(NewDomain(1, ContractA)));

            Assert.Equal(fromTyped, fromRaw);
            Assert.Equal(fromRaw, fromRawAgain);
            Assert.Equal(_signer.SignTypedDataV4(json, _key), SignatureFor(NewDomain(1, ContractA), permit));
        }
    }
}
