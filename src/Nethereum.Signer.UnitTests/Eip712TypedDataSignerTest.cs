using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class Eip712TypedDataSignerTest
    {
        private readonly Eip712TypedDataSigner _signer = new Eip712TypedDataSigner();

        [Fact]
        public void ComplexMessageTypedDataEncodingShouldBeCorrect()
        {
            var typedData = new TypedData
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

            Assert.Equal(Sha3Keccack.Current.CalculateHash(result).ToHex(true), "0xbe609aee343fb3c4b28e1df9e632fca64fcfaede20f02e86244efddf30957bd2", ignoreCase: true);
        }

        [Fact]
        public void FlatMessageObjectEncodingShouldBeCorrect()
        {
            var domain = new Domain
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

            var encodedMessage = _signer.EncodeTypedData(param, domain, "SafeTx");
            Assert.Equal(
                encodedMessage.ToHex(true),
                "0x1901a15700103df744480601949aa3add5a0c0ebf6d258bf881eb6abac9736ead7f43a707a87afefa511211636c16608979d6ce2fc81e3c6979d4b80fb4bf3ff1080",
                ignoreCase: true
            );

            var testPrivateKey = "8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
            var signature = _signer.SignTypedData(param, domain, "SafeTx", new EthECKey(testPrivateKey));
            Assert.Equal(
                signature,
                "0x12bc2897d54cf62b8cc4864f02e6e154ddf416e51b5919a608e32978fa80422e369a3d6b24eeb2875e3fabc233b4ebbd8306c209a81958c440172780ad9a99841c",
                ignoreCase: true
            );

            var recoveredAddress = new EthereumMessageSigner().EcRecover(Sha3Keccack.Current.CalculateHash(encodedMessage), signature);
            Assert.Equal(
                recoveredAddress,
                "0x63FaC9201494f0bd17B9892B9fae4d52fe3BD377",
                ignoreCase: true
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