using System.Threading;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Signer.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class TimePreferenceSuggestionStrategy1559Tests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public TimePreferenceSuggestionStrategy1559Tests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }
        
        [Fact]
        public async void ShouldBeAbleToCalculateHistoryAndSend1000sOfTransactions()
        {
            var receiveAddress = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var feeStrategy = new TimePreferenceSuggestionStrategy(web3.Client);
            for (var x = 0; x < 100; x++)
            {
                Thread.Sleep(200);
                var fee = await feeStrategy.SuggestFeesAsync();
                for (int i = 0; i < 1000; i++)
                {
                    var encoded = await web3.TransactionManager.SendTransactionAsync(
                        new TransactionInput()
                        {
                            Type = new HexBigInteger(2),
                            From = web3.TransactionManager.Account.Address,
                            MaxFeePerGas = new HexBigInteger(fee[0].MaxFeePerGas.Value),
                            MaxPriorityFeePerGas = new HexBigInteger(fee[0].MaxPriorityFeePerGas.Value),
                            To = receiveAddress,
                            Value = new HexBigInteger(10)
                        });
                }
            }
        }
    }
}