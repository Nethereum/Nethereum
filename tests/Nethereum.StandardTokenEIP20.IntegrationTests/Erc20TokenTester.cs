using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.Decoders;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.StandardTokenEIP20.Events.DTO;
using Nethereum.XUnitEthereumClients;
using Xunit;
// ReSharper disable once ConsiderUsingConfigureAwait

namespace Nethereum.StandardTokenEIP20.IntegrationTests
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class Erc20TokenTester
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public Erc20TokenTester(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        private static async Task<TransactionReceipt> GetTransactionReceiptAsync(
            EthApiTransactionsService transactionService, string transactionHash)
        {
            TransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                receipt = await transactionService.GetTransactionReceipt.SendRequestAsync(transactionHash).ConfigureAwait(false);
            }
            return receipt;
        }

        [Fact]
        public async void ShouldGetTheDaiFromMainnet()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var contractHandler = web3.Eth.GetContractHandler("0x89d24A6b4CcB1B6fAA2625fE562bDD9a23260359");
            var stringBytes32Decoder = new StringBytes32Decoder();
            var symbol = await contractHandler.QueryRawAsync<SymbolFunction, StringBytes32Decoder, string>().ConfigureAwait(false);
            var token = await contractHandler.QueryRawAsync<NameFunction, StringBytes32Decoder, string>().ConfigureAwait(false);
            
        }

        [Fact]
        public async void ShouldReturnData()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentHandler =  web3.Eth.GetContractDeploymentHandler<EIP20Deployment>();
            var receipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(new EIP20Deployment()
            {
                DecimalUnits = 18,
                InitialAmount = BigInteger.Parse("10000000000000000000000000"),
                TokenSymbol = "XST",
                TokenName = "XomeStandardToken"
            }).ConfigureAwait(false);

            var contractHandler = web3.Eth.GetContractHandler(receipt.ContractAddress);
            var symbol = await contractHandler.QueryRawAsync<SymbolFunction, StringBytes32Decoder, string>().ConfigureAwait(false);
            var token = await contractHandler.QueryRawAsync<NameFunction, StringBytes32Decoder, string>().ConfigureAwait(false);

            Assert.Equal("XST", symbol);
            Assert.Equal("XomeStandardToken", token);
        }

        [Fact]
        public async void Test()
        {
            var addressOwner = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
     
            ulong totalSupply = 1000000;
            var newAddress = "0x12890d2cce102216644c59daE5baed380d84830e";

            var deploymentContract = new EIP20Deployment()
            {
                InitialAmount = totalSupply,
                TokenName = "TestToken",
                TokenSymbol = "TST"
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(web3, deploymentContract).ConfigureAwait(false);
            
            var transfersEvent = tokenService.GetTransferEvent();

            var totalSupplyDeployed = await tokenService.TotalSupplyQueryAsync().ConfigureAwait(false);
            Assert.Equal(totalSupply, totalSupplyDeployed);

            var tokenName = await tokenService.NameQueryAsync().ConfigureAwait(false);
            Assert.Equal("TestToken", tokenName);

            var tokenSymbol = await tokenService.SymbolQueryAsync().ConfigureAwait(false);
            Assert.Equal("TST", tokenSymbol);

            var ownerBalance = await tokenService.BalanceOfQueryAsync(addressOwner).ConfigureAwait(false);
            Assert.Equal(totalSupply, ownerBalance);

            var transferReceipt =
                await tokenService.TransferRequestAndWaitForReceiptAsync(newAddress, 1000).ConfigureAwait(false);

            ownerBalance = await tokenService.BalanceOfQueryAsync(addressOwner).ConfigureAwait(false);
            Assert.Equal(totalSupply - 1000, ownerBalance);

            var newAddressBalance = await tokenService.BalanceOfQueryAsync(newAddress).ConfigureAwait(false);
            Assert.Equal(1000, newAddressBalance);

            var allTransfersFilter =
                await transfersEvent.CreateFilterAsync(new BlockParameter(transferReceipt.BlockNumber)).ConfigureAwait(false);
            var eventLogsAll = await transfersEvent.GetAllChangesAsync(allTransfersFilter).ConfigureAwait(false);
            Assert.Single(eventLogsAll);
            var transferLog = eventLogsAll.First();
            Assert.Equal(transferLog.Log.TransactionIndex.HexValue, transferReceipt.TransactionIndex.HexValue);
            Assert.Equal(transferLog.Log.BlockNumber.HexValue, transferReceipt.BlockNumber.HexValue);
            Assert.Equal(transferLog.Event.To.ToLower(), newAddress.ToLower());
            Assert.Equal(transferLog.Event.Value, (ulong) 1000);

            var approveTransactionReceipt = await tokenService.ApproveRequestAndWaitForReceiptAsync(newAddress, 1000).ConfigureAwait(false);
            var allowanceAmount = await tokenService.AllowanceQueryAsync(addressOwner, newAddress).ConfigureAwait(false);
            Assert.Equal(1000, allowanceAmount);
        }
    }
}