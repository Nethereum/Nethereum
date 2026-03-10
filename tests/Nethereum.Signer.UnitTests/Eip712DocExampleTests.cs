using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class Eip712DocExampleTests
    {
        private const string PrivateKey = "94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5";
        private readonly Eip712TypedDataSigner _signer = new Eip712TypedDataSigner();

        [Struct("Person")]
        public class Person
        {
            [Parameter("string", "name", 1)]
            public string Name { get; set; }

            [Parameter("address", "wallet", 2)]
            public string Wallet { get; set; }
        }

        [Struct("Mail")]
        public class Mail
        {
            [Parameter("tuple", "from", 1, "Person")]
            public Person From { get; set; }

            [Parameter("tuple", "to", 2, "Person")]
            public Person To { get; set; }

            [Parameter("string", "contents", 3)]
            public string Contents { get; set; }
        }

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

        private TypedData<Domain> GetMailTypedDefinition()
        {
            return new TypedData<Domain>
            {
                Domain = new Domain
                {
                    Name = "Ether Mail",
                    Version = "1",
                    ChainId = 1,
                    VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Mail), typeof(Person)),
                PrimaryType = nameof(Mail),
            };
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-signing", "Simple typed message")]
        public void ShouldSignAndRecoverSimpleTypedMessage()
        {
            var typedData = GetMailTypedDefinition();

            var mail = new Mail
            {
                From = new Person { Name = "Cow", Wallet = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826" },
                To = new Person { Name = "Bob", Wallet = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB" },
                Contents = "Hello, Bob!"
            };

            var key = new EthECKey(PrivateKey);
            var signature = _signer.SignTypedDataV4(mail, typedData, key);

            Assert.StartsWith("0x", signature);
            Assert.Equal(132, signature.Length);

            var recoveredAddress = _signer.RecoverFromSignatureV4(mail, typedData, signature);
            var expectedAddress = key.GetPublicAddress();

            Assert.True(expectedAddress.IsTheSameAddress(recoveredAddress));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-signing", "Sign and recover from JSON")]
        public void ShouldSignAndRecoverFromJson()
        {
            var typedDataJson =
                @"{
                    'domain':{'chainId':1,'name':'Ether Mail','verifyingContract':'0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC','version':'1'},
                    'message':{'contents':'Hello, Bob!','from':{'name':'Cow','wallet':'0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826'},'to':{'name':'Bob','wallet':'0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB'}},
                    'primaryType':'Mail',
                    'types':{
                        'EIP712Domain':[{'name':'name','type':'string'},{'name':'version','type':'string'},{'name':'chainId','type':'uint256'},{'name':'verifyingContract','type':'address'}],
                        'Mail':[{'name':'from','type':'Person'},{'name':'to','type':'Person'},{'name':'contents','type':'string'}],
                        'Person':[{'name':'name','type':'string'},{'name':'wallet','type':'address'}]
                    }
                }";

            var key = new EthECKey(PrivateKey);

            var signature = _signer.SignTypedDataV4(typedDataJson, key);
            Assert.StartsWith("0x", signature);
            Assert.Equal(132, signature.Length);

            var recoveredAddress = _signer.RecoverFromSignatureV4(typedDataJson, signature);
            var expectedAddress = key.GetPublicAddress();

            Assert.True(expectedAddress.IsTheSameAddress(recoveredAddress));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-signing", "ERC-2612 Permit signature")]
        public void ShouldSignErc2612Permit()
        {
            var typedData = new TypedData<Domain>
            {
                Domain = new Domain
                {
                    Name = "MyToken",
                    Version = "1",
                    ChainId = 1,
                    VerifyingContract = "0x1234567890abcdef1234567890abcdef12345678"
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Permit)),
                PrimaryType = nameof(Permit),
            };

            var permit = new Permit
            {
                Owner = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826",
                Spender = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB",
                Value = BigInteger.Parse("1000000000000000000"),
                Nonce = 0,
                Deadline = BigInteger.Parse("1000000000000")
            };

            var key = new EthECKey(PrivateKey);
            var signature = _signer.SignTypedDataV4(permit, typedData, key);

            Assert.NotNull(signature);
            Assert.StartsWith("0x", signature);
            Assert.Equal(132, signature.Length);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-signing", "Domain separator properties")]
        public void ShouldSetDomainSeparatorProperties()
        {
            var domain = new Domain
            {
                Name = "My Dapp",
                Version = "2",
                ChainId = 137,
                VerifyingContract = "0x1111111111111111111111111111111111111111"
            };

            Assert.Equal("My Dapp", domain.Name);
            Assert.Equal("2", domain.Version);
            Assert.Equal(137, domain.ChainId);
            Assert.Equal("0x1111111111111111111111111111111111111111", domain.VerifyingContract);

            var typedData = new TypedData<Domain>
            {
                Domain = domain,
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Mail), typeof(Person)),
                PrimaryType = nameof(Mail),
            };

            Assert.Equal(nameof(Mail), typedData.PrimaryType);
            Assert.NotNull(typedData.Types);
            Assert.True(typedData.Types.ContainsKey("EIP712Domain"));
            Assert.True(typedData.Types.ContainsKey("Mail"));
            Assert.True(typedData.Types.ContainsKey("Person"));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-signing", "Verify signature")]
        public void ShouldVerifySignatureByRecoveringAddress()
        {
            var typedData = GetMailTypedDefinition();

            var mail = new Mail
            {
                From = new Person { Name = "Cow", Wallet = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826" },
                To = new Person { Name = "Bob", Wallet = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB" },
                Contents = "Hello, Bob!"
            };

            var key = new EthECKey(PrivateKey);
            var signerAddress = key.GetPublicAddress();

            var signature = _signer.SignTypedDataV4(mail, typedData, key);

            var recoveredAddress = _signer.RecoverFromSignatureV4(mail, typedData, signature);

            Assert.True(signerAddress.IsTheSameAddress(recoveredAddress));

            var wrongAddress = "0x0000000000000000000000000000000000000001";
            Assert.False(wrongAddress.IsTheSameAddress(recoveredAddress));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-signing", "SignTypedData with auto-generated schema")]
        public void ShouldSignWithAutoGeneratedSchema()
        {
            var domain = new Domain
            {
                Name = "Ether Mail",
                Version = "1",
                ChainId = 1,
                VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
            };

            var permit = new Permit
            {
                Owner = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826",
                Spender = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB",
                Value = BigInteger.Parse("1000000000000000000"),
                Nonce = 0,
                Deadline = BigInteger.Parse("1000000000000")
            };

            var key = new EthECKey(PrivateKey);

            var signature = _signer.SignTypedData<Permit, Domain>(permit, domain, "Permit", key);

            Assert.NotNull(signature);
            Assert.StartsWith("0x", signature);
            Assert.Equal(132, signature.Length);

            var encodedData = Eip712TypedDataEncoder.Current.EncodeTypedData(permit, domain, "Permit");
            var recoveredAddress = new MessageSigner().EcRecover(
                Sha3Keccack.Current.CalculateHash(encodedData), signature);
            var expectedAddress = key.GetPublicAddress();

            Assert.True(expectedAddress.IsTheSameAddress(recoveredAddress));
        }
    }
}
