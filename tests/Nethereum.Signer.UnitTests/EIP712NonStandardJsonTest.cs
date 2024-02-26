using Nethereum.Signer.EIP712;
using Xunit;
using Nethereum.ABI.EIP712;

namespace Nethereum.Signer.UnitTests
{
    public class EIP712NonStandardJsonTest
    {
        [Fact]
        public void ShouldBeAbleToSignNonStandardJsonWithoutDomainTypeDifferentMessageSelectorAndNotPrimaryTypeButOnlyOneType()
        {
            //This json has got only one type, so we use the Domain type as it matches it
            //No primaryType, but we use the first and only type
            //No message, but instead "value" so we set the "value" as none default.
            var json = @"{
                          ""types"": {
                                        ""FollowWithSig"": [
                                                  {
                                                    ""name"": ""profileIds"",
                                                    ""type"": ""uint256[]""
                                                  },
                                                  {
                                                    ""name"": ""datas"",
                                                    ""type"": ""bytes[]""
                                                  },
                                                  {
                                                    ""name"": ""nonce"",
                                                    ""type"": ""uint256""
                                                  },
                                                  {
                                                    ""name"": ""deadline"",
                                                    ""type"": ""uint256""
                                                  }
                                                ]
                                      },

                            ""value"": {
                                            ""nonce"": 4,
                                            ""deadline"": 1670438881,
                                            ""profileIds"": [
                                                ""0x5b0f""
                                                ],
                                            ""datas"": [
                                              ""0x""
                                                ]
                                        },
                          ""domain"": {
                                ""name"": ""Protocol"",
                                ""chainId"": ""100000000"",
                                ""verifyingContract"": ""0x60Ae865ee4C725cd04353b5AAb364553f56ceF82"",
                                ""version"": ""1""
                                }
                            }";



            var signer = new Eip712TypedDataSigner();
            var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
            string signature = signer.SignTypedDataV4<Domain>(json, key, "value");

            Assert.NotNull(signature);
        }

        [Fact]
        public void ShouldBeAbleToRecoverSignedNonStandardJsonWithoutDomainTypeDifferentMessageSelectorAndNotPrimaryTypeButOnlyOneType()
        {
            //This json has got only one type, so we use the Domain type as it matches it
            //No primaryType, but we use the first and only type
            //No message, but instead "value" so we set the "value" as none default.
            var json = @"{
                          ""types"": {
                                        ""FollowWithSig"": [
                                                  {
                                                    ""name"": ""profileIds"",
                                                    ""type"": ""uint256[]""
                                                  },
                                                  {
                                                    ""name"": ""datas"",
                                                    ""type"": ""bytes[]""
                                                  },
                                                  {
                                                    ""name"": ""nonce"",
                                                    ""type"": ""uint256""
                                                  },
                                                  {
                                                    ""name"": ""deadline"",
                                                    ""type"": ""uint256""
                                                  }
                                                ]
                                      },

                            ""value"": {
                                            ""nonce"": 4,
                                            ""deadline"": 1670438881,
                                            ""profileIds"": [
                                                ""0x5b0f""
                                                ],
                                            ""datas"": [
                                              ""0x""
                                                ]
                                        },
                          ""domain"": {
                                ""name"": ""Protocol"",
                                ""chainId"": ""100000000"",
                                ""verifyingContract"": ""0x60Ae865ee4C725cd04353b5AAb364553f56ceF82"",
                                ""version"": ""1""
                                }
                            }";



            var signer = new Eip712TypedDataSigner();
            var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
            string signature = signer.SignTypedDataV4<Domain>(json, key, "value");

            string result = signer.RecoverFromSignatureV4(json, signature, "value");
            Assert.Equal(result, key.GetPublicAddress());
        }

        [Fact]
        public void TestEncodingWithBytes32()
        {
            var domainSchema = @"{
        ""types"": {
                ""eip712domain"": [
                    {""name"": ""name"", ""type"": ""string""},
                    {""name"": ""version"", ""type"": ""string""},
                    {""name"": ""chainId"", ""type"": ""uint256""},
                    {""name"": ""verifyingContract"", ""type"": ""address""}
                ],
                ""Agent"": [
                     {""name"": ""source"", ""type"": ""string""},
                     {""name"": ""connectionId"", ""type"": ""bytes32""} 
                ]
            },
            ""domain"": {
                ""name"": ""yourdapp"",
                ""version"": ""1"",
                ""chainId"": 1,
                ""verifyingContract"": ""0xcccccccccccccccccccccccccccccccccccccccc""
            },
            ""message"": {
                ""source"": ""b"",
                ""connectionId"": ""e9d4fad2244749f8e85e3ffd5d7a9b97""
            },
            ""primaryType"": ""Agent""
       }";
            var typedEncoder = new Eip712TypedDataSigner();
            var encoder = typedEncoder.EncodeTypedData(domainSchema);

            var signer = new Eip712TypedDataSigner();
            var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
            string signature = signer.SignTypedDataV4<Domain>(domainSchema, key);

            string result = signer.RecoverFromSignatureV4(domainSchema, signature);
            Assert.Equal(result, key.GetPublicAddress());
        }
    }

   
}