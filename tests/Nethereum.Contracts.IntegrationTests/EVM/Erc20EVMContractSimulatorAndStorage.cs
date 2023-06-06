using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.DebugNode.Dtos;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.XUnitEthereumClients;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json;
using System.Threading;
using Nethereum.ABI;
using System.Linq;
// ReSharper disable ConsiderUsingConfigureAwait  
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.EVM
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class Erc20EVMContractSimulatorAndStorage
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public Erc20EVMContractSimulatorAndStorage(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }


        [Fact]
        public async void ShouldCalculateBalanceSlot()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var contractAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var senderAddress = "0x0000000000000000000000000000000000000001";
            var simulator = new Nethereum.EVM.Contracts.ERC20.ERC20Simulator(web3, 1, contractAddress);
            var slot = await simulator.CalculateMappingBalanceSlotAsync(senderAddress, 100);
            Assert.Equal(9, slot);
        }

        [Fact]
        public async void ShouldSimulateTransferAndBalanceState()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var contractAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var senderAddress = "0x0000000000000000000000000000000000000001";
            var receiverAddress = "0x0000000000000000000000000000000000000025";
            var simulator = new Nethereum.EVM.Contracts.ERC20.ERC20Simulator(web3, 1, contractAddress);
            var simulationResult = await simulator.SimulateTransferAndBalanceStateAsync(senderAddress, receiverAddress, 100);
            Assert.Equal(simulationResult.BalanceSenderAfter, simulationResult.BalanceSenderBefore - 100);
            Assert.Equal(simulationResult.BalanceSenderStorageAfter, simulationResult.BalanceSenderBefore - 100);
            Assert.Equal(simulationResult.BalanceReceiverAfter, simulationResult.BalanceReceiverBefore + 100);
            Assert.Equal(simulationResult.BalanceReceiverStorageAfter, simulationResult.BalanceReceiverBefore + 100);

        }



        [Fact]
        public async void ShouldBeAbleToGetBalanceFromStorage()
        {
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var contractAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var senderAddress = "0x0000000000000000000000000000000000000001";
            var balanceStorage = await web3.Eth.ERC20.GetContractService(contractAddress).GetBalanceFromStorageAsync(senderAddress, 9);
            var balanceSmartContract = await web3.Eth.ERC20.GetContractService(contractAddress).BalanceOfQueryAsync(senderAddress);
            Assert.Equal(balanceSmartContract, balanceStorage);
        }

      
    }

}
       
