using Nethereum.Signer.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Xunit;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.ABI.EIP712;
using System.Numerics;
using System;

namespace Nethereum.Signer.UnitTests
{
    public class Eip712JsonBytesTypesAsString
    {
        [Struct("BytesType")]
        class BytesType
        {
            [Parameter("bytes32", "first", 1)]
            public virtual byte[] First { get; set; }

            [Parameter("bytes", "second", 2)]
            public virtual byte[] Second { get; set; }

            [Parameter("bytes1", "third", 3)]
            public virtual byte[] Third { get; set; }

            [Parameter("bytes", "empty", 4)]
            public virtual byte[] Empty { get; set; }
        }

        [Fact]
        public void SerializeDeserialize()
        {
            var signer = new Eip712TypedDataSigner();

            var typedData = new TypedData<DomainWithSalt>
            {
                Domain = new DomainWithSalt
                {
                    Name = "a",
                    Version = "b",
                    ChainId = BigInteger.Parse("1"),
                    VerifyingContract = EthECKey.GenerateKey().GetPublicAddress(),
                    Salt = "0567ba82f82338342c115e01e2461857ecefd0db574456d345ffb092d0273f67".HexToByteArray(),
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(DomainWithSalt), typeof(BytesType)),
                PrimaryType = nameof(BytesType)
            };
            typedData.SetMessage(new BytesType()
            {
                First = "0567ba82f82338342c11".HexToByteArray(),
                Second = "0567ba82f82338342c115e01e2461857ecefd0db574456d345ffb092d0273f670567ba82f82338342c115e01e2461857ecefd0db574456d345ffb092d0273f67".HexToByteArray(),
                Third = "05".HexToByteArray(),
                Empty = new byte[0]
            });


            var json = typedData.ToJson();
            var typedDataRecovered = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData<DomainWithSalt>(json);

            var encoded = signer.EncodeTypedData(typedData);
            var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(encoded);

            var encodedRecovered = signer.EncodeTypedDataRaw(typedDataRecovered);
            var hashRecovered = Nethereum.Util.Sha3Keccack.Current.CalculateHash(encodedRecovered);

            Assert.Equal(hashRecovered.ToHex(), hash.ToHex());

        }
    }

    
}