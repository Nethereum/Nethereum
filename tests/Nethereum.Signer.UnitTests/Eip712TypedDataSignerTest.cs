using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Xunit;
using System.Text;
using Nethereum.ABI.FunctionEncoding;
using System.Linq;
using Newtonsoft.Json;
using Nethereum.ABI.ABIDeserialisation;
using System.Text.RegularExpressions;
using System.Collections;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Xml.Linq;
using Nethereum.ABI.EIP712;

namespace Nethereum.Signer.UnitTests
{

    public class Eip712TypedDataSignerTest
    {

        private readonly Eip712TypedDataSigner _signer = new Eip712TypedDataSigner();

        [Fact]
        private void ComplexMessageTypedDataEncodingShouldBeCorrectForV4IncludingArrays()
        {
            var typedData = new TypedData<Domain>
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
                Message = new[]
                {
                    new MemberValue
                    {
                        TypeName = "Person", Value = new[]
                        {
                            new MemberValue {TypeName = "string", Value = "Cow"},
                            new MemberValue {TypeName = "address[]", Value = new List<string>{ "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF" } },
                        }
                    },
                    new MemberValue
                    {
                        TypeName = "Person[]", Value = new List<MemberValue[]>{
                            new[]
                            {
                                new MemberValue {TypeName = "string", Value = "Bob"},
                                new MemberValue {TypeName = "address[]", Value = new List<string>{ "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB", "0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57", "0xB0B0b0b0b0b0B000000000000000000000000000" } },
                            }
                        }
                    },
                    new MemberValue {TypeName = "string", Value = "Hello, Bob!"},

                }
            };

            var result = _signer.EncodeTypedData(typedData);
            var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
            var signature = _signer.SignTypedDataV4(typedData, key);
            Assert.Equal("0x943393c998ab7e067d2875385e2218c9b3140f563694267ac9f6276a9fcc53e15c1526abe460cd6e2f570a35418f132d9733363400c44791ff7b88f0e9c91d091b", signature);
            var addressRecovered = _signer.RecoverFromSignatureV4(typedData, signature);
            var address = key.GetPublicAddress();
            Assert.True(address.IsTheSameAddress(addressRecovered));


        }
      

        [Fact]
        private void ComplexMessageTypedDataEncodingShouldBeCorrectForV4IncludingArraysJson()
        {
            var typedDataJson = /*lang=json*/ "{'domain':{'chainId':1,'name':'Ether Mail','verifyingContract':'0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC','version':'1'},'message':{'contents':'Hello, Bob!','attachedMoneyInEth':4.2,'from':{'name':'Cow','wallets':['0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826','0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF']},'to':[{'name':'Bob','wallets':['0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB','0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57','0xB0B0b0b0b0b0B000000000000000000000000000']}]},'primaryType':'Mail','types':{'EIP712Domain':[{'name':'name','type':'string'},{'name':'version','type':'string'},{'name':'chainId','type':'uint256'},{'name':'verifyingContract','type':'address'}],'Group':[{'name':'name','type':'string'},{'name':'members','type':'Person[]'}],'Mail':[{'name':'from','type':'Person'},{'name':'to','type':'Person[]'},{'name':'contents','type':'string'}],'Person':[{'name':'name','type':'string'},{'name':'wallets','type':'address[]'}]}}";

            var rawTypedData = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(typedDataJson);

          
            var rawEncodedTypedData = _signer.EncodeTypedDataRaw(rawTypedData);

            var newTypedDataJsonString = TypedDataRawJsonConversion.SerialiseRawTypedDataToJson(rawTypedData);
            var newRawTypedData = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(newTypedDataJsonString);
            var rawNewEncodedTypedData = _signer.EncodeTypedDataRaw(newRawTypedData);

            var typedData = new TypedData<Domain>
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
                Message = new[]
                {
                    new MemberValue
                    {
                        TypeName = "Person", Value = new[]
                        {
                            new MemberValue {TypeName = "string", Value = "Cow"},
                            new MemberValue {TypeName = "address[]", Value = new List<string>{ "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF" } },
                        }
                    },
                    new MemberValue
                    {
                        TypeName = "Person[]", Value = new List<MemberValue[]>{
                            new[]
                            {
                                new MemberValue {TypeName = "string", Value = "Bob"},
                                new MemberValue {TypeName = "address[]", Value = new List<string>{ "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB", "0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57", "0xB0B0b0b0b0b0B000000000000000000000000000" } },
                            }
                        }
                    },
                    new MemberValue {TypeName = "string", Value = "Hello, Bob!"},

                }
            };

            var result = _signer.EncodeTypedData(typedData);

            Assert.Equal(rawEncodedTypedData.ToHex(), result.ToHex());
            Assert.Equal(rawNewEncodedTypedData.ToHex(), result.ToHex());
        }


        [Fact]
        public void ComplexMessageTypedDataEncodingShouldBeCorrectForV4()
        {
            var typedData = new TypedData<Domain>
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
                    ["Person"] = new[]
                    {
                        new MemberDescription {Name = "name", Type = "string"},
                        new MemberDescription {Name = "wallet", Type = "address"},
                    },
                    ["Mail"] = new[]
                    {
                        new MemberDescription {Name = "from", Type = "Person"},
                        new MemberDescription {Name = "to", Type = "Person"},
                        new MemberDescription {Name = "contents", Type = "string"},
                    }
                },
                PrimaryType = "Mail",
                Message = new[]
                {
                    new MemberValue
                    {
                        TypeName = "Person", Value = new[]
                        {
                            new MemberValue {TypeName = "string", Value = "Cow"},
                            new MemberValue {TypeName = "address", Value = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826"},
                        }
                    },
                    new MemberValue
                    {
                        TypeName = "Person", Value = new[]
                        {
                            new MemberValue {TypeName = "string", Value = "Bob"},
                            new MemberValue {TypeName = "address", Value = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB"},
                        }
                    },
                    new MemberValue {TypeName = "string", Value = "Hello, Bob!"},
                }
            };

          
            var typedDataJson = typedData.ToJson();

            var result = _signer.EncodeTypedData(typedData);
            
            Assert.Equal("0xbe609aee343fb3c4b28e1df9e632fca64fcfaede20f02e86244efddf30957bd2", Sha3Keccack.Current.CalculateHash(result).ToHex(true), ignoreCase: true);
            var encodedJson = _signer.EncodeTypedData(typedDataJson);

            Assert.Equal("0xbe609aee343fb3c4b28e1df9e632fca64fcfaede20f02e86244efddf30957bd2", Sha3Keccack.Current.CalculateHash(encodedJson).ToHex(true), ignoreCase: true);

            var key = new EthECKey("83f8964bd55c98a4806a7b100bd9d885798d7f936f598b88916e11bade576204");
            var signature = _signer.SignTypedDataV4(typedData, key);
            Assert.Equal("0xf714d2cd123498a5551cafee538d073c139c5c237c2d0a98937a5cce109bfefb7c6585fed974543c649b0cae34ac8763ee0ac536a56a82980c14470f0029907b1b",
                signature);
            
            var signatureFromJson = _signer.SignTypedDataV4(typedDataJson, key);
            Assert.Equal("0xf714d2cd123498a5551cafee538d073c139c5c237c2d0a98937a5cce109bfefb7c6585fed974543c649b0cae34ac8763ee0ac536a56a82980c14470f0029907b1b",
                signatureFromJson);

            var addressRecovered = new MessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(result), signature);
            var address = key.GetPublicAddress();
            Assert.True(address.IsTheSameAddress(addressRecovered));

            addressRecovered = _signer.RecoverFromSignatureV4(typedData, signature);
            Assert.True(address.IsTheSameAddress(addressRecovered));

            addressRecovered = _signer.RecoverFromSignatureV4(result, signature);
            Assert.True(address.IsTheSameAddress(addressRecovered));

            addressRecovered = _signer.RecoverFromSignatureHashV4(Sha3Keccack.Current.CalculateHash(result), signature);
            Assert.True(address.IsTheSameAddress(addressRecovered));

            addressRecovered = _signer.RecoverFromSignatureV4(typedDataJson, signature);
            Assert.True(address.IsTheSameAddress(addressRecovered));
        }
            

        [Fact]
        public void ComplexMessageTypedDataEncodingShouldBeCorrect()
        {
            var typedData = new TypedData<Domain>
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
                    ["Person"] = new[]
                    {
                        new MemberDescription {Name = "name", Type = "string"},
                        new MemberDescription {Name = "wallet", Type = "address"},
                    },
                    ["Mail"] = new[]
                    {
                        new MemberDescription {Name = "from", Type = "Person"},
                        new MemberDescription {Name = "to", Type = "Person"},
                        new MemberDescription {Name = "contents", Type = "string"},
                    }
                },
                PrimaryType = "Mail",
                Message = new[]
                {
                    new MemberValue
                    {
                        TypeName = "Person", Value = new[]
                        {
                            new MemberValue {TypeName = "string", Value = "Cow"},
                            new MemberValue {TypeName = "address", Value = "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826"},
                        }
                    },
                    new MemberValue
                    {
                        TypeName = "Person", Value = new[]
                        {
                            new MemberValue {TypeName = "string", Value = "Bob"},
                            new MemberValue {TypeName = "address", Value = "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB"},
                        }
                    },
                    new MemberValue {TypeName = "string", Value = "Hello, Bob!"},
                }
            };

            var result = _signer.EncodeTypedData(typedData);

            Assert.Equal("0xbe609aee343fb3c4b28e1df9e632fca64fcfaede20f02e86244efddf30957bd2", Sha3Keccack.Current.CalculateHash(result).ToHex(true), ignoreCase: true);
            var key = new EthECKey(Sha3Keccack.Current.CalculateHash("cow"));
            var signature = _signer.SignTypedData(typedData, key);
            var addressRecovered = new EthereumMessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(result), signature);
            var address = key.GetPublicAddress();
        }


        [Struct("EIP712Domain")]
        public class MyFlatDomain : IDomain
        {

            [Parameter("address", "verifyingContract", 1)]
            public virtual string VerifyingContract { get; set; }

        }

        [Fact]
        public void FlatMessageObjectEncodingShouldBeCorrect()
        {
            var domain = new MyFlatDomain()
            {
                VerifyingContract = "0x0fced4cc7788ede6d93e23e0b54bb56a98114ce2"
            };

            var param = new EncodeTransactionDataFunction
            {
                To = "0x4f96fe3b7a6cf9725f59d353f723c1bdb64ca6aa",
                Value = 0,
                Data = "0x095ea7b3000000000000000000000000e7bc397dbd069fc7d0109c0636d06888bb50668c00000000000000000000000000000000000000000000000000000000ffffffff".HexToByteArray(),
                Operation = 0,
                SafeTxGas = 0,
                BaseGas = 0,
                GasPrice = 0,
                GasToken = AddressUtil.AddressEmptyAsHex,
                RefundReceiver = AddressUtil.AddressEmptyAsHex,
                Nonce = 1
            };

            var encodedMessage = Eip712TypedDataEncoder.Current.EncodeTypedData(param, domain, "SafeTx");
            Assert.Equal(
                "0x1901a15700103df744480601949aa3add5a0c0ebf6d258bf881eb6abac9736ead7f43a707a87afefa511211636c16608979d6ce2fc81e3c6979d4b80fb4bf3ff1080",
                encodedMessage.ToHex(true), ignoreCase: true
            );

            var testPrivateKey = "8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
            var signature = _signer.SignTypedData(param, domain, "SafeTx", new EthECKey(testPrivateKey));
            Assert.Equal(
                "0x12bc2897d54cf62b8cc4864f02e6e154ddf416e51b5919a608e32978fa80422e369a3d6b24eeb2875e3fabc233b4ebbd8306c209a81958c440172780ad9a99841c",
                signature, ignoreCase: true
            );

            var recoveredAddress = new EthereumMessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(encodedMessage), signature);
            Assert.Equal(
                "0x63FaC9201494f0bd17B9892B9fae4d52fe3BD377",
                recoveredAddress, ignoreCase: true
            );
        }

        private class EncodeTransactionDataFunction
        {
            [Parameter("address", "to", 1)] public virtual string To { get; set; }
            [Parameter("uint256", "value", 2)] public virtual BigInteger Value { get; set; }
            [Parameter("bytes", "data", 3)] public virtual byte[] Data { get; set; }
            [Parameter("uint8", "operation", 4)] public virtual byte Operation { get; set; }
            [Parameter("uint256", "safeTxGas", 5)] public virtual BigInteger SafeTxGas { get; set; }
            [Parameter("uint256", "baseGas", 6)] public virtual BigInteger BaseGas { get; set; }
            [Parameter("uint256", "gasPrice", 7)] public new virtual BigInteger GasPrice { get; set; }
            [Parameter("address", "gasToken", 8)] public virtual string GasToken { get; set; }

            [Parameter("address", "refundReceiver", 9)]
            public virtual string RefundReceiver { get; set; }

            [Parameter("uint256", "nonce", 10)] public new virtual BigInteger Nonce { get; set; }
        }
    }
}