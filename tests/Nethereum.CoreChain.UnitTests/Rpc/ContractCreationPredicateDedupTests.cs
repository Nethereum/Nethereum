using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class ContractCreationPredicateDedupTests
    {
        private const string Caller = "0x1234567890123456789012345678901234567890";
        private const string Recipient = "0x9999999999999999999999999999999999999999";

        private const string InitCodeReturningEmpty = "0x60006000f3";

        private static async Task<DevChainNode> StartAsync()
        {
            var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = "prague" });
            await node.StartAsync(new[] { Caller });
            return node;
        }

        [Theory]
        [InlineData("0x")]
        [InlineData(null)]
        public void Given_ToIsEmptyOrNull_When_IsContractCreationRecipientChecked_Then_ItIsTrue(string to)
        {
            Assert.True(SignedTransactionExtensions.IsContractCreationRecipient(to));
        }

        [Fact]
        public void Given_ToIsARealAddress_When_IsContractCreationRecipientChecked_Then_ItIsFalse()
        {
            Assert.False(SignedTransactionExtensions.IsContractCreationRecipient(Recipient));
        }

        private static async Task<HexBigInteger> EstimateGasAsync(DevChainNode node, string to)
        {
            var response = await new EthEstimateGasHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_estimateGas", new { from = Caller, to, data = InitCodeReturningEmpty }, "latest"),
                new RpcContext(node, chainId: 1, services: null));

            Assert.Null(response.Error);
            return (HexBigInteger)response.Result;
        }

        [Fact]
        public async Task Given_EthEstimateGas_When_ToIsNullOrEmptyHex_Then_BothAreTreatedAsContractCreation_WithTheSameEstimate()
        {
            using var node = await StartAsync();

            var estimateForNullTo = await EstimateGasAsync(node, null);
            var estimateForEmptyHexTo = await EstimateGasAsync(node, "0x");

            Assert.Equal(estimateForNullTo.Value, estimateForEmptyHexTo.Value);
        }

        [Fact]
        public async Task Given_EthEstimateGas_When_ToIsARealAddress_Then_ItIsNotTreatedAsContractCreation()
        {
            using var node = await StartAsync();

            var creationEstimate = await EstimateGasAsync(node, null);
            var callEstimate = await EstimateGasAsync(node, Recipient);

            Assert.NotEqual(creationEstimate.Value, callEstimate.Value);
        }

        private static async Task<AccessListGasUsed> CreateAccessListAsync(DevChainNode node, string to)
        {
            var response = await new EthCreateAccessListHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_createAccessList", new { from = Caller, to, data = InitCodeReturningEmpty }, "latest"),
                new RpcContext(node, chainId: 1, services: null));

            Assert.Null(response.Error);
            return (AccessListGasUsed)response.Result;
        }

        [Fact]
        public async Task Given_EthCreateAccessList_When_ToIsNullOrEmptyHex_Then_NeitherErrorsAndBothReportAnAccessList()
        {
            using var node = await StartAsync();

            var forNullTo = await CreateAccessListAsync(node, null);
            var forEmptyHexTo = await CreateAccessListAsync(node, "0x");

            Assert.Null(forNullTo.Error);
            Assert.Null(forEmptyHexTo.Error);
            Assert.NotNull(forNullTo.AccessList);
            Assert.NotNull(forEmptyHexTo.AccessList);
        }
    }
}
