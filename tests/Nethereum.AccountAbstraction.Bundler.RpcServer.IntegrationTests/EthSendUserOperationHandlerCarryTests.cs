using System.Numerics;
using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    /// <summary>
    /// Send-path carry: the handler must read the EIP-7702 auth side-channel off the wire and
    /// forward it to IBundlerService.SendUserOperationAsync - the exact value BundlerService then
    /// places on the MempoolEntry. A capturing stub proves the hop without a chain.
    /// </summary>
    public class EthSendUserOperationHandlerCarryTests
    {
        private const string EntryPoint = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";

        private sealed class CapturingBundlerService : IBundlerService
        {
            public Authorisation? CapturedAuth { get; private set; }
            public bool SendCalled { get; private set; }

            public Task<string> SendUserOperationAsync(PackedUserOperation userOp, string entryPoint, Authorisation? eip7702Auth = null)
            {
                SendCalled = true;
                CapturedAuth = eip7702Auth;
                return Task.FromResult("0x" + new string('a', 64));
            }

            public Task<Nethereum.RPC.AccountAbstraction.DTOs.UserOperationGasEstimate> EstimateUserOperationGasAsync(Nethereum.AccountAbstraction.UserOperation userOp, string entryPoint) => throw new NotImplementedException();
            public Task<Nethereum.RPC.AccountAbstraction.DTOs.UserOperationReceipt?> GetUserOperationReceiptAsync(string userOpHash) => throw new NotImplementedException();
            public Task<IncludedUserOperation?> GetUserOperationByHashAsync(string userOpHash) => throw new NotImplementedException();
            public Task<string[]> SupportedEntryPointsAsync() => throw new NotImplementedException();
            public Task<BigInteger> ChainIdAsync() => throw new NotImplementedException();
        }

        private static RpcRequestMessage BuildRequest(string userOpWire)
        {
            using var doc = JsonDocument.Parse(userOpWire);
            return new RpcRequestMessage(1, "eth_sendUserOperation", doc.RootElement.Clone(), EntryPoint);
        }

        [Fact]
        public async Task Handle_UserOpWithEip7702Auth_ForwardsTupleToBundler()
        {
            var stub = new CapturingBundlerService();
            var handler = new EthSendUserOperationHandler(stub);

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

            var response = await handler.HandleAsync(BuildRequest(wire), null!);

            Assert.Null(response.Error);
            Assert.True(stub.SendCalled);
            Assert.NotNull(stub.CapturedAuth);
            Assert.Equal(1, stub.CapturedAuth.ChainId.Value);
            Assert.Equal("0x1234567890123456789012345678901234567890", stub.CapturedAuth.Address);
            Assert.Equal(7, stub.CapturedAuth.Nonce.Value);
            Assert.Equal("0x1", stub.CapturedAuth.YParity);
            Assert.Equal("0x1111111111111111111111111111111111111111111111111111111111111111", stub.CapturedAuth.R);
            Assert.Equal("0x2222222222222222222222222222222222222222222222222222222222222222", stub.CapturedAuth.S);
        }

        [Fact]
        public async Task Handle_UserOpWithoutEip7702Auth_ForwardsNull()
        {
            var stub = new CapturingBundlerService();
            var handler = new EthSendUserOperationHandler(stub);

            var wire = @"{
                ""sender"": ""0x1111111111111111111111111111111111111111"",
                ""nonce"": ""0x1"",
                ""callData"": ""0xca11da7a"",
                ""callGasLimit"": ""0x186a0"",
                ""verificationGasLimit"": ""0x249f0"",
                ""preVerificationGas"": ""0xc350"",
                ""maxFeePerGas"": ""0x77359400"",
                ""maxPriorityFeePerGas"": ""0x3b9aca00"",
                ""signature"": ""0xdeadbeef""
            }";

            var response = await handler.HandleAsync(BuildRequest(wire), null!);

            Assert.Null(response.Error);
            Assert.True(stub.SendCalled);
            Assert.Null(stub.CapturedAuth);
        }
    }
}
