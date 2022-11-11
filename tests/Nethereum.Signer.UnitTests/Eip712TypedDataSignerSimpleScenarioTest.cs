using System.Collections.Generic;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Xunit;
using Nethereum.ABI.EIP712;

namespace Nethereum.Signer.UnitTests
{
    public class Eip712TypedDataSignerSimpleScenarioTest
    {
        private readonly Eip712TypedDataSigner _signer = new Eip712TypedDataSigner();

        //Message types for easier input
        [Struct("Mail")]
        public class Mail
        {
            [Parameter("tuple", "from", 1, "Person")]
            public Person From { get; set; }

            [Parameter("tuple[]", "to", 2, "Person[]")]
            public List<Person> To { get; set; }

            [Parameter("string", "contents", 3)]
            public string Contents { get; set; }
        }

        [Struct("Person")]
        public class Person
        {
            [Parameter("string", "name", 1)]
            public string Name { get; set; }

            [Parameter("address[]", "wallets", 2)]
            public List<string> Wallets { get; set; }
        }

        [Struct("Group")]
        public class Group
        {
            [Parameter("string", "name", 1)]
            public string Name { get; set; }

            [Parameter("tuple[]", "members", 2, "Person[]")]
            public List<Person> Members { get; set; }
        }

        //The generic EIP712 Typed schema defintion for this message
        public TypedData<Domain> GetMailTypedDefinition()
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
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Group), typeof(Mail), typeof(Person)),
                PrimaryType = nameof(Mail),
            };
        }

        [Fact]
        private void ComplexMessageTypedDataEncodingShouldBeCorrectForV4IncludingArraysAndTypes()
        {
            var typedData = GetMailTypedDefinition();

            var mail = new Mail
            {
                From = new Person 
                { 
                  Name = "Cow", 
                  Wallets = new List<string> { "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF" } 
                },
                To = new List<Person>
                {
                    new Person 
                    { 
                        Name = "Bob", 
                        Wallets = new List<string> { "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB", "0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57", "0xB0B0b0b0b0b0B000000000000000000000000000" } 
                    }
                },
                Contents = "Hello, Bob!"
            };

            typedData.Domain.ChainId = 1;
            typedData.SetMessage(mail);

            var json = typedData.ToJson();
            var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");

            var signature = _signer.SignTypedDataV4(mail, typedData, key);
            
            Assert.Equal("0x943393c998ab7e067d2875385e2218c9b3140f563694267ac9f6276a9fcc53e15c1526abe460cd6e2f570a35418f132d9733363400c44791ff7b88f0e9c91d091b", signature);
            
            var addressRecovered = _signer.RecoverFromSignatureV4(mail, typedData, signature);
            var address = key.GetPublicAddress();
            
            Assert.True(address.IsTheSameAddress(addressRecovered));

        }

    }
}