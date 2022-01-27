using System.Collections.Generic;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class Eip712TypedDataSignerSimpleScenarioTest
    {
        private readonly Eip712TypedDataSigner _signer = new Eip712TypedDataSigner();

        //Message types for easier input
        public class Mail
        {
            [Parameter("tuple", "from", 1, "Person")]
            public Person From { get; set; }

            [Parameter("tuple[]", "to", 2, "Person[]")]
            public List<Person> To { get; set; }

            [Parameter("string", "contents", 2)]
            public string Contents { get; set; }
        }

        public class Person
        {
            [Parameter("string", "name", 1)]
            public string Name { get; set; }

            [Parameter("address[]", "wallets", 2)]
            public List<string> Wallets { get; set; }
        }

        //The generic Typed schema defintion for this message
        public TypedData GetMailTypedDefintion()
        {
            return new TypedData
            {
                Domain = new Domain
                {
                    Name = "Ether Mail",
                    Version = "1",
                    ChainId = 1,
                    VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
                },
                Types = new Dictionary<string, MemberDescription[]>
                {
                    ["EIP712Domain"] = new[]
                    {
                        new MemberDescription {Name = "name", Type = "string"},
                        new MemberDescription {Name = "version", Type = "string"},
                        new MemberDescription {Name = "chainId", Type = "uint256"},
                        new MemberDescription {Name = "verifyingContract", Type = "address"},
                    },
                    ["Group"] = new[]
                    {
                        new MemberDescription {Name = "name", Type = "string"},
                        new MemberDescription {Name = "members", Type = "Person[]"},

                    },
                    ["Mail"] = new[]
                    {
                        new MemberDescription {Name = "from", Type = "Person"},
                        new MemberDescription {Name = "to", Type = "Person[]"},
                        new MemberDescription {Name = "contents", Type = "string"},
                    },
                    ["Person"] = new[]
                    {
                        new MemberDescription {Name = "name", Type = "string"},
                        new MemberDescription {Name = "wallets", Type = "address[]"},
                    },
                },
                PrimaryType = "Mail",
            };
        }

        [Fact]
        private void ComplexMessageTypedDataEncodingShouldBeCorrectForV4IncludingArraysAndTypes()
        {
            var typedData = GetMailTypedDefintion();

            var mail = new Mail
            {
                From = new Person { Name = "Cow", Wallets = new List<string> { "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF" } },
                To = new List<Person>(),
                Contents = "Hello, Bob!"
            };
            mail.To.Add(new Person { Name = "Bob", Wallets = new List<string> { "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB", "0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57", "0xB0B0b0b0b0b0B000000000000000000000000000" } });
      
            var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
            var signature = _signer.SignTypedDataV4(mail, typedData, key);
            Assert.Equal("0x943393c998ab7e067d2875385e2218c9b3140f563694267ac9f6276a9fcc53e15c1526abe460cd6e2f570a35418f132d9733363400c44791ff7b88f0e9c91d091b", signature);
            var addressRecovered = _signer.RecoverFromSignatureV4(mail, typedData, signature);
            var address = key.GetPublicAddress();
            Assert.True(address.IsTheSameAddress(addressRecovered));

        }

    }
}