using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;
using ClientUserOperation = Nethereum.AccountAbstraction.UserOperation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Rpc
{
    public class Eip7702AuthWireTests
    {
        private static Authorisation SampleAuth() => new Authorisation
        {
            ChainId = new HexBigInteger(1),
            Address = "0x1234567890123456789012345678901234567890",
            Nonce = new HexBigInteger(7),
            YParity = "0x1",
            R = "0x1111111111111111111111111111111111111111111111111111111111111111",
            S = "0x2222222222222222222222222222222222222222222222222222222222222222"
        };

        private static RpcUserOperation MinimalRpcUserOp() => new RpcUserOperation
        {
            Sender = "0x1111111111111111111111111111111111111111",
            Nonce = new HexBigInteger(1),
            CallData = "0xca11da7a",
            CallGasLimit = new HexBigInteger(0x186a0),
            VerificationGasLimit = new HexBigInteger(0x249f0),
            PreVerificationGas = new HexBigInteger(0xc350),
            MaxFeePerGas = new HexBigInteger(0x77359400),
            MaxPriorityFeePerGas = new HexBigInteger(0x3b9aca00),
            Signature = "0xdeadbeef"
        };

        [Fact]
        public void Newtonsoft_SerialisesEip7702Auth_AsSingleTupleWithSpecFieldNames()
        {
            var op = MinimalRpcUserOp();
            op.Eip7702Auth = SampleAuth();

            var json = JsonConvert.SerializeObject(op, DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings());
            var root = JObject.Parse(json);

            var auth = root["eip7702Auth"] as JObject;
            Assert.NotNull(auth);
            Assert.Equal(
                new[] { "chainId", "address", "nonce", "yParity", "r", "s" },
                ((System.Collections.Generic.IEnumerable<KeyValuePair<string, JToken>>)auth)
                    .Select(p => p.Key).ToArray());

            Assert.Equal("0x1", auth["chainId"].Value<string>());
            Assert.Equal("0x1234567890123456789012345678901234567890", auth["address"].Value<string>());
            Assert.Equal("0x7", auth["nonce"].Value<string>());
            Assert.Equal("0x1", auth["yParity"].Value<string>());
            Assert.Equal("0x1111111111111111111111111111111111111111111111111111111111111111", auth["r"].Value<string>());
            Assert.Equal("0x2222222222222222222222222222222222222222222222222222222222222222", auth["s"].Value<string>());
        }

        [Fact]
        public void Newtonsoft_RoundTripsEip7702Auth()
        {
            var op = MinimalRpcUserOp();
            op.Eip7702Auth = SampleAuth();

            var settings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            var json = JsonConvert.SerializeObject(op, settings);
            var back = JsonConvert.DeserializeObject<RpcUserOperation>(json, settings);

            Assert.NotNull(back.Eip7702Auth);
            Assert.Equal(op.Eip7702Auth.ChainId.Value, back.Eip7702Auth.ChainId.Value);
            Assert.Equal(op.Eip7702Auth.Address, back.Eip7702Auth.Address);
            Assert.Equal(op.Eip7702Auth.Nonce.Value, back.Eip7702Auth.Nonce.Value);
            Assert.Equal(op.Eip7702Auth.YParity, back.Eip7702Auth.YParity);
            Assert.Equal(op.Eip7702Auth.R, back.Eip7702Auth.R);
            Assert.Equal(op.Eip7702Auth.S, back.Eip7702Auth.S);
        }

        [Fact]
        public void Newtonsoft_OmitsEip7702Auth_WhenUnset_SerialisesIdenticallyToToday()
        {
            var settings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();

            var without = JsonConvert.SerializeObject(MinimalRpcUserOp(), settings);

            Assert.DoesNotContain("eip7702Auth", without);
            Assert.DoesNotContain("eip7702auth", without.ToLowerInvariant());
        }

        [Fact]
        public void Stj_DeserialisesEip7702Auth_ServerReceivePath()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var wire = @"{
                ""sender"": ""0x1111111111111111111111111111111111111111"",
                ""nonce"": ""0x1"",
                ""callData"": ""0xca11da7a"",
                ""callGasLimit"": ""0x186a0"",
                ""verificationGasLimit"": ""0x249f0"",
                ""preVerificationGas"": ""0xc350"",
                ""maxFeePerGas"": ""0x77359400"",
                ""maxPriorityFeePerGas"": ""0x3b9aca00"",
                ""signature"": ""0xdeadbeef"",
                ""eip7702Auth"": {
                    ""chainId"": ""0x1"",
                    ""address"": ""0x1234567890123456789012345678901234567890"",
                    ""nonce"": ""0x7"",
                    ""yParity"": ""0x1"",
                    ""r"": ""0x1111111111111111111111111111111111111111111111111111111111111111"",
                    ""s"": ""0x2222222222222222222222222222222222222222222222222222222222222222""
                }
            }";

            var op = System.Text.Json.JsonSerializer.Deserialize<RpcUserOperation>(wire, options);

            Assert.NotNull(op.Eip7702Auth);
            Assert.Equal(1, op.Eip7702Auth.ChainId.Value);
            Assert.Equal("0x1234567890123456789012345678901234567890", op.Eip7702Auth.Address);
            Assert.Equal(7, op.Eip7702Auth.Nonce.Value);
            Assert.Equal("0x1", op.Eip7702Auth.YParity);
        }

        [Fact]
        public void Stj_LeavesEip7702AuthNull_WhenAbsent()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var wire = @"{ ""sender"": ""0x1111111111111111111111111111111111111111"", ""nonce"": ""0x1"" }";

            var op = System.Text.Json.JsonSerializer.Deserialize<RpcUserOperation>(wire, options);

            Assert.Null(op.Eip7702Auth);
        }

        [Fact]
        public void Converter_ToRpcFormat_CarriesEip7702Auth_ClientToRpc()
        {
            var auth = SampleAuth();
            var clientOp = new ClientUserOperation
            {
                Sender = "0x1111111111111111111111111111111111111111",
                Nonce = 1,
                Eip7702Auth = auth
            };

            var rpc = UserOperationConverter.ToRpcFormat(clientOp);

            Assert.Same(auth, rpc.Eip7702Auth);
        }

        [Fact]
        public void Converter_ToRpcFormat_LeavesEip7702AuthNull_WhenUnset()
        {
            var clientOp = new ClientUserOperation
            {
                Sender = "0x1111111111111111111111111111111111111111",
                Nonce = 1
            };

            var rpc = UserOperationConverter.ToRpcFormat(clientOp);

            Assert.Null(rpc.Eip7702Auth);
        }
    }
}
