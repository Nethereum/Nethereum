using Newtonsoft.Json;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Rpc
{
    public class UserOperationRpcShapeTests
    {
        private const string SpecByHashResponse = @"{
            ""userOperation"": {
                ""sender"": ""0x1111111111111111111111111111111111111111"",
                ""nonce"": ""0x1"",
                ""callData"": ""0xca11da7a"",
                ""callGasLimit"": ""0x186a0"",
                ""verificationGasLimit"": ""0x249f0"",
                ""preVerificationGas"": ""0xc350"",
                ""maxFeePerGas"": ""0x77359400"",
                ""maxPriorityFeePerGas"": ""0x3b9aca00"",
                ""signature"": ""0xdeadbeef""
            },
            ""entryPoint"": ""0x433709009B8330FDa32311DF1C2AFA402eD8D009"",
            ""blockNumber"": ""0x14fe317"",
            ""blockHash"": ""0x2222222222222222222222222222222222222222222222222222222222222222"",
            ""transactionHash"": ""0x3333333333333333333333333333333333333333333333333333333333333333""
        }";

        private const string SpecReceiptResponse = @"{
            ""userOpHash"": ""0x4444444444444444444444444444444444444444444444444444444444444444"",
            ""entryPoint"": ""0x433709009B8330FDa32311DF1C2AFA402eD8D009"",
            ""sender"": ""0x1111111111111111111111111111111111111111"",
            ""nonce"": ""0x1"",
            ""actualGasCost"": ""0x2386f26fc10000"",
            ""actualGasUsed"": ""0x30d40"",
            ""success"": true,
            ""logs"": [
                {
                    ""address"": ""0x1111111111111111111111111111111111111111"",
                    ""topics"": [""0x5555555555555555555555555555555555555555555555555555555555555555""],
                    ""data"": ""0x"",
                    ""blockNumber"": ""0x14fe317"",
                    ""transactionHash"": ""0x3333333333333333333333333333333333333333333333333333333333333333"",
                    ""transactionIndex"": ""0x0"",
                    ""blockHash"": ""0x2222222222222222222222222222222222222222222222222222222222222222"",
                    ""logIndex"": ""0x0"",
                    ""removed"": false
                }
            ],
            ""receipt"": {
                ""transactionHash"": ""0x3333333333333333333333333333333333333333333333333333333333333333"",
                ""blockNumber"": ""0x14fe317"",
                ""status"": ""0x1""
            }
        }";

        [Fact]
        public void GetUserOperationByHash_SpecWrapper_DeserializesCompletely()
        {
            var result = JsonConvert.DeserializeObject<UserOperationByHashResult>(SpecByHashResponse);

            Assert.NotNull(result);
            Assert.NotNull(result.UserOperation);
            Assert.Equal("0x1111111111111111111111111111111111111111", result.UserOperation.Sender);
            Assert.Equal(1, result.UserOperation.Nonce.Value);
            Assert.Equal("0x433709009B8330FDa32311DF1C2AFA402eD8D009", result.EntryPoint);
            Assert.Equal(0x14fe317, result.BlockNumber.Value);
            Assert.Equal("0x2222222222222222222222222222222222222222222222222222222222222222", result.BlockHash);
            Assert.Equal("0x3333333333333333333333333333333333333333333333333333333333333333", result.TransactionHash);
        }

        [Fact]
        public void GetUserOperationByHash_PendingResponse_HasNullInclusionFields()
        {
            var pending = @"{
                ""userOperation"": { ""sender"": ""0x1111111111111111111111111111111111111111"", ""nonce"": ""0x1"" },
                ""entryPoint"": ""0x433709009B8330FDa32311DF1C2AFA402eD8D009"",
                ""blockNumber"": null,
                ""blockHash"": null,
                ""transactionHash"": null
            }";

            var result = JsonConvert.DeserializeObject<UserOperationByHashResult>(pending);

            Assert.NotNull(result.UserOperation);
            Assert.Null(result.BlockNumber);
            Assert.Null(result.BlockHash);
            Assert.Null(result.TransactionHash);
        }

        [Fact]
        public void GasEstimate_SpecPaymasterFields_Deserialize()
        {
            var estimateJson = @"{
                ""callGasLimit"": ""0x186a0"",
                ""verificationGasLimit"": ""0x249f0"",
                ""preVerificationGas"": ""0xc350"",
                ""paymasterVerificationGasLimit"": ""0x11170"",
                ""paymasterPostOpGasLimit"": ""0x5208""
            }";

            var estimate = JsonConvert.DeserializeObject<UserOperationGasEstimate>(estimateJson);

            Assert.Equal(0x11170, estimate.PaymasterVerificationGasLimit.Value);
            Assert.Equal(0x5208, estimate.PaymasterPostOpGasLimit.Value);
        }

        [Fact]
        public void EstimateGas_StateOverride_IsAddressKeyedSet()
        {
            var handler = new Nethereum.RPC.Eth.AccountAbstraction.EthEstimateUserOperationGas(
                new Nethereum.JsonRpc.Client.RpcClient(new System.Uri("http://localhost:1")));

            var overrides = new System.Collections.Generic.Dictionary<string, Nethereum.RPC.Eth.DTOs.StateChange>
            {
                ["0x1111111111111111111111111111111111111111"] = new Nethereum.RPC.Eth.DTOs.StateChange
                {
                    Balance = new Nethereum.Hex.HexTypes.HexBigInteger(1)
                }
            };

            var request = handler.BuildRequest(
                new Nethereum.RPC.AccountAbstraction.DTOs.UserOperation { Sender = "0x1111111111111111111111111111111111111111" },
                "0x433709009B8330FDa32311DF1C2AFA402eD8D009",
                stateOverrides: overrides);

            Assert.Equal(3, request.RawParameters.Length);
            Assert.Same(overrides, request.RawParameters[2]);
        }

        [Fact]
        public void UserOperationReceipt_SpecLogObjects_Deserialize()
        {
            var receipt = JsonConvert.DeserializeObject<UserOperationReceipt>(SpecReceiptResponse);

            Assert.NotNull(receipt);
            Assert.True(receipt.Success);
            Assert.NotNull(receipt.Logs);
            var log = Assert.Single(receipt.Logs);
            Assert.Equal("0x1111111111111111111111111111111111111111", log.Address);
            Assert.Single(log.Topics);
            Assert.Equal(0x14fe317, log.BlockNumber.Value);
        }
    }
}
