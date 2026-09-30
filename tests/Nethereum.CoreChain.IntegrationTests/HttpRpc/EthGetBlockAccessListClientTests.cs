using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Collection("HttpRpcAmsterdam")]
    public class EthGetBlockAccessListClientTests : IClassFixture<DevChainAmsterdamHttpFixture>
    {
        private readonly DevChainAmsterdamHttpFixture _fixture;
        private static readonly BigInteger OneEther = BigInteger.Parse("1000000000000000000");

        public EthGetBlockAccessListClientTests(DevChainAmsterdamHttpFixture fixture)
        {
            _fixture = fixture;
        }

        private Task<TransactionReceipt> SendEtherAsync() =>
            _fixture.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(
                new TransactionInput
                {
                    From = _fixture.Account.Address,
                    To = DevChainHttpFixture.RecipientAddress,
                    Value = new HexBigInteger(OneEther),
                    Gas = new HexBigInteger(21_000)
                });

        [Fact]
        public async Task Given_ABlockThatMovedEther_When_TheAccessListIsFetchedByTheClient_Then_ItNamesTheAccountsThatChanged()
        {
            var receipt = await SendEtherAsync();

            var accessList = await _fixture.Web3.Eth.Blocks.GetBlockAccessList
                .SendRequestAsync(new BlockParameter(receipt.BlockNumber));

            Assert.NotNull(accessList);
            Assert.NotEmpty(accessList);
            Assert.Contains(accessList, a => a.Address.IsTheSameAddress(DevChainHttpFixture.RecipientAddress));
            Assert.Contains(accessList,
                a => a.Address.IsTheSameAddress(DevChainHttpFixture.RecipientAddress) && a.BalanceChanges.Any());
        }

        [Fact]
        public async Task Given_ABlockWithNoStoredAccessList_When_Fetched_Then_TheClientSurfacesTheNodesRefusalRatherThanAnEmptyList()
        {
            var refusal = await Assert.ThrowsAsync<RpcResponseException>(() =>
                _fixture.Web3.Eth.Blocks.GetBlockAccessList
                    .SendRequestAsync(new BlockParameter(new HexBigInteger(0))));

            Assert.Contains("access", refusal.Message, System.StringComparison.OrdinalIgnoreCase);
        }

    }
}
