using System.Collections.Generic;
using Nethereum.Signer.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Xunit;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Diagnostics;
using Nethereum.ABI.EIP712;

namespace Nethereum.Signer.UnitTests
{
    public class Eip712TypedDataStructArraysAsTheOnlyElementType
    {

        //GetMailTypedDefinition is the generic function that contains all the metadata required to sign this domain specific message
        //All the different types (Domain, Group, Mail, Person) are defined as classes in a similar way to Nethereum Function Messages
        //In the standard you need to provide the Domain, this can be extended with your own type,
        //The different types that are pare of the domain
        //and the PrimaryType which is the message entry point
        public static TypedData<Domain> GetMailTypedDefinition()
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

        //The domain type Mail is defined in a similar way to a Nethereum Function Message, with the attribute Struct("Mail")
        //Parameters are the same, although when working with tuples we need to provide the name of the Tuple like "Person" or "Person[]" if it is an array
        [Struct("Mail")]
        public class Mail
        {

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

        [Fact]
        public void ShouldEncodeUsingSimpleTypeDefinitions()
        {

            //The mail typed definition, this provides the typed data schema used for this specific domain
            var typedData = GetMailTypedDefinition();

            //The data we are going to sign (Primary type) mail
            var mail = new Mail
            {
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

            //This type data is specific to the chainId 1
            typedData.Domain.ChainId = 1;

            var encoded = Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(mail, typedData);
            Assert.True(encoded.ToHex().IsTheSameHex("a7f60314a32adc242676f7264d7246d13f70989540ce0584605f811c69556bf7"));
        }

        [Fact]
        public void ShouldEncodeUsingUntypedTypedDefinitions()
        {
            var typedData = new TypedData<Domain>()
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
                    ["Mail"] = new[]
                {
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
                Message = new[]
            {
                new MemberValue
                {
                    TypeName = "Person[]",
                    Value = new object[]
                    {
                        new MemberValue[]
                        {
                            new MemberValue
                            {
                                TypeName = "string",
                                Value = "Bob",
                            },
                            new MemberValue
                            {
                                TypeName = "address[]",
                                Value = new[]
                                {
                                    "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB",
                                    "0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57",
                                    "0xB0B0b0b0b0b0B000000000000000000000000000"
                                },
                            },
                        },
                    },
                },
                new MemberValue
                {
                    TypeName = "string",
                    Value = "Hello, Bob!",
                },
            },
            };

            var encoder = new Eip712TypedDataEncoder();
            var encoded = encoder.EncodeAndHashTypedData(typedData);
            Assert.True(encoded.ToHex().IsTheSameHex("a7f60314a32adc242676f7264d7246d13f70989540ce0584605f811c69556bf7"));

        }
    }

    
}