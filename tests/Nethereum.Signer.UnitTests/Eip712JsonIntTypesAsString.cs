using System.Collections.Generic;
using Nethereum.Signer.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Xunit;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.ABI.EIP712;
using System.Numerics;

namespace Nethereum.Signer.UnitTests
{
    public class Eip712JsonIntTypesAsString
    {

        [Fact]
        public void ShouldEncodeTypedAsBigInteger()
        {
            var signer = new Eip712TypedDataSigner();

            var typedData = GetOrderTypedDefinition();

            //The data we are going to sign (Primary type) mail
            var order = new Order
            {
                Trader = "0x7235d8d035c5ecd470f5a47bdfacf844ef1358c8",
                Side = 0,
                MatchingPolicy = "0x0000000000b92d5d043faf7cecf7e2ee6aaed232",
                Collection = "0x34d85c9cdeb23fa97cb08333b511ac86e1c4e258",
                TokenId = 0,
                Amount = 1,
                PaymentToken = "0x0000000000a39bb272e79075ade125fd351887ac",
                Price = 1000000000000000000,
                ListingTime = 1677201457,
                ExpirationTime = 1708737456,
                Fees = new List<Fee>(),
                Salt = BigInteger.Parse("176833519079950976564067085591514560244"),
                ExtraParams = "0x01".HexToByteArray(),
                Nonce = 0
            };

            //This type data is specific to the chainId 1
            typedData.SetMessage(order);
            typedData.Domain.ChainId = 1;
            var encoded = signer.EncodeTypedData(typedData);
            var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(encoded);
            Assert.Equal("4f08a7bb9d951d28787f2103ccb0e018bce02fbf08819953942ea013ccf208d5", hash.ToHex());
        }

        [Fact]
        public void ShouldEncodeJsonAsBigInteger()
        {

            var json = @"{
  types: {
    Order: [
      {
        name: ""trader"",
        type: ""address""
      },
      {
        name: ""side"",
        type: ""uint8""
      },
      {
        name: ""matchingPolicy"",
        type: ""address""
      },
      {
        name: ""collection"",
        type: ""address""
      },
      {
        name: ""tokenId"",
        type: ""uint256""
      },
      {
        name: ""amount"",
        type: ""uint256""
      },
      {
        name: ""paymentToken"",
        type: ""address""
      },
      {
        name: ""price"",
        type: ""uint256""
      },
      {
        name: ""listingTime"",
        type: ""uint256""
      },
      {
        name: ""expirationTime"",
        type: ""uint256""
      },
      {
        name: ""fees"",
        type: ""Fee[]""
      },
      {
        name: ""salt"",
        type: ""uint256""
      },
      {
        name: ""extraParams"",
        type: ""bytes""
      },
      {
        name: ""nonce"",
        type: ""uint256""
      }
    ],
    Fee: [
      {
        name: ""rate"",
        type: ""uint16""
      },
      {
        name: ""recipient"",
        type: ""address""
      }
    ],
    EIP712Domain: [
      {
        name: ""name"",
        type: ""string""
      },
      {
        name: ""version"",
        type: ""string""
      },
      {
        name: ""chainId"",
        type: ""uint256""
      },
      {
        name: ""verifyingContract"",
        type: ""address""
      }
    ]
  },
  domain: {
    name: ""Blur Exchange"",
    version: ""1.0"",
    chainId: ""1"",
    verifyingContract: ""0x000000000000ad05ccc4f10045630fb830b95127""
  },
  primaryType: ""Order"",
  message: {
    trader: ""0x7235d8d035c5ecd470f5a47bdfacf844ef1358c8"",
    side: ""0"",
    matchingPolicy: ""0x0000000000b92d5d043faf7cecf7e2ee6aaed232"",
    collection: ""0x34d85c9cdeb23fa97cb08333b511ac86e1c4e258"",
    tokenId: ""0"",
    amount: ""1"",
    paymentToken: ""0x0000000000a39bb272e79075ade125fd351887ac"",
    price: ""1000000000000000000"",
    listingTime: ""1677201457"",
    expirationTime: ""1708737456"",
    fees: [],
    salt: ""176833519079950976564067085591514560244"",
    extraParams: ""0x01"",
    nonce: ""0""
  }}";
            var encoder = new Eip712TypedDataEncoder();
            var encoded = encoder.EncodeTypedData(json);
            var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(encoded);
            Assert.Equal("4f08a7bb9d951d28787f2103ccb0e018bce02fbf08819953942ea013ccf208d5", hash.ToHex());

           
           var typedDataRaw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(json);
           var newJson = TypedDataRawJsonConversion.SerialiseRawTypedDataToJson(typedDataRaw);
           var encodedReversed = encoder.EncodeTypedData(newJson);
           var hashnew = Nethereum.Util.Sha3Keccack.Current.CalculateHash(encodedReversed);
           Assert.Equal("4f08a7bb9d951d28787f2103ccb0e018bce02fbf08819953942ea013ccf208d5", hashnew.ToHex());
        }

        public static TypedData<Domain> GetOrderTypedDefinition()
        {
            return new TypedData<Domain>
            {
                Domain = new Domain
                {
                    Name = "Blur Exchange",
                    Version = "1.0",
                    ChainId = 1,
                    VerifyingContract = "0x000000000000ad05ccc4f10045630fb830b95127"
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Order), typeof(Fee), typeof(Domain)),
                PrimaryType = nameof(Order),
            };
        }


        [Struct("Order")]
        public class Order
        {
            [Parameter("address", "trader", 1)]
            public string Trader { get; set; }

            [Parameter("uint8", "side", 2)]
            public BigInteger Side { get; set; }

            [Parameter("address", "matchingPolicy", 3)]
            public string MatchingPolicy { get; set; }

            [Parameter("address", "collection", 4)]
            public string Collection { get; set; }

            [Parameter("uint256", "tokenId", 5)]
            public BigInteger TokenId { get; set; }

            [Parameter("uint256", "amount", 6)]
            public BigInteger Amount { get; set; }

            [Parameter("address", "paymentToken", 7)]
            public string PaymentToken { get; set; }

            [Parameter("uint256", "price", 8)]
            public BigInteger Price { get; set; }

            [Parameter("uint256", "listingTime", 9)]
            public BigInteger ListingTime { get; set; }

            [Parameter("uint256", "expirationTime", 10)]
            public BigInteger ExpirationTime { get; set; }

            [Parameter("tuple[]", "fees", 11, "Fee[]")]
            public List<Fee> Fees { get; set; }

            [Parameter("uint256", "salt", 12)]
            public BigInteger Salt { get; set; }

            [Parameter("bytes", "extraParams", 13)]
            public byte[] ExtraParams { get; set; }

            [Parameter("uint256", "nonce", 14)]
            public BigInteger Nonce { get; set; }
        }

        [Struct("Fee")]
        public class Fee
        {
            [Parameter("uint16", "rate", 1)]
            public BigInteger Rate { get; set; }

            [Parameter("address", "recipient", 2)]
            public string Recipient { get; set; }
        }
    }

    
}