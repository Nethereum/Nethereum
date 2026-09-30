using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.BlockStorage.Repositories;
using Nethereum.BlockchainProcessing.Services.SmartContracts;
using Nethereum.Util;
using Xunit;

namespace Nethereum.BlockchainProcessing.Token.UnitTests
{
    public class NativeTransfersDoNotBecomeTokenBalancesTests
    {
        private const string TokenContract = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string Sender = "0x1111111111111111111111111111111111111111";
        private const string Receiver = "0x2222222222222222222222222222222222222222";

        private static TokenTransferLog TransferRow(string contract) => new TokenTransferLog
        {
            ContractAddress = contract,
            TokenType = "ERC20",
            FromAddress = Sender,
            ToAddress = Receiver,
            Amount = "1000",
            BlockNumber = 100,
            TransactionHash = "0xabc123",
            LogIndex = 0
        };

        private static async Task<List<TokenBalance>> AggregateAsync(TokenTransferLog row)
        {
            var balances = new InMemoryTokenBalanceRepository();
            var service = new TokenBalanceAggregationService(
                new InMemoryTokenTransferLogRepository(),
                balances,
                new InMemoryNFTInventoryRepository(),
                progressRepository: null);

            await service.ProcessTransferAsync(row);

            return balances.Records;
        }

        [Fact]
        public async Task Given_ATransferFromTheEthTransferEmitter_When_BalancesAreAggregated_Then_NoTokenBalanceRowIsCreated()
        {
            var balances = await AggregateAsync(TransferRow(AddressUtil.SYSTEM_ADDRESS));

            Assert.Empty(balances);
        }

        [Fact]
        public async Task Given_ATransferFromARealTokenContract_When_BalancesAreAggregated_Then_ItIsStillAggregated()
        {
            var balances = await AggregateAsync(TransferRow(TokenContract));

            Assert.NotEmpty(balances);
            Assert.All(balances, b => Assert.True(b.ContractAddress.IsTheSameAddress(TokenContract)));
        }
    }
}
