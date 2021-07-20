using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Signer.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class TransactionSigningTests
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public TransactionSigningTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }
 
        [Fact]
        public async Task<bool> ShouldSignAndSendRawTransaction()
        {
            var receiveAddress = "0x13f022d72158410433cbd66f5dd8bf6d2d129924";
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

           
            
            var feeStrategy = new SimpleFeeSuggestionStrategy(web3.Client);
         
            var fee = await feeStrategy.SuggestFeeAsync();
            var encoded = await web3.TransactionManager.SignTransactionAsync(
                new TransactionInput()
                {
                    Type = new HexBigInteger(2),
                    From = web3.TransactionManager.Account.Address,
                    MaxFeePerGas = new HexBigInteger(fee.MaxFeePerGas.Value),
                    MaxPriorityFeePerGas = new HexBigInteger(fee.MaxPriorityFeePerGas.Value),
                    Nonce = await web3.Eth.TransactionManager.Account.NonceService.GetNextNonceAsync(),
                    To = receiveAddress,
                    Value = new HexBigInteger(10)
                });
            
            Assert.True(TransactionVerificationAndRecovery.VerifyTransaction(encoded));

           
            Assert.Equal(web3.TransactionManager.Account.Address.EnsureHexPrefix().ToLower(),
                TransactionVerificationAndRecovery.GetSenderAddress(encoded).EnsureHexPrefix().ToLower());

            var txId = await web3.Eth.Transactions.SendRawTransaction.SendRequestAsync("0x" + encoded);
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txId);
            while (receipt == null)
            {
                Thread.Sleep(1000);
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txId);
            }

            Assert.Equal(txId, receipt.TransactionHash);
            return true;
        }

      
    }
}