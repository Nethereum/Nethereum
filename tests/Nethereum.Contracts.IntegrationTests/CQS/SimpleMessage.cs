using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting
// ReSharper disable ConsiderUsingConfigureAwait

namespace Nethereum.Contracts.IntegrationTests.CQS
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class SimpleMessage
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public SimpleMessage(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void TestCQS()
        {
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new TestContractDeployment() {FromAddress = senderAddress};

            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TestContractDeployment>();
            var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);

            var contractHandler = web3.Eth.GetContractHandler(transactionReceipt.ContractAddress);

            var returnSenderMessage = new ReturnSenderFunction() {FromAddress = senderAddress};

            var returnAddress = await contractHandler.QueryAsync<ReturnSenderFunction, string>(returnSenderMessage);

            Assert.Equal(senderAddress.ToLower(), returnAddress.ToLower());
        }

        [Fact]
        public async void TestOriginal()
        {
            var byteCode =
                "0x6060604052341561000f57600080fd5b60ac8061001d6000396000f300606060405260043610603e5763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416635170a9d081146043575b600080fd5b3415604d57600080fd5b6053607c565b60405173ffffffffffffffffffffffffffffffffffffffff909116815260200160405180910390f35b33905600a165627a7a72305820ad71c73577f8423259abb92d0e9aad1a0e98ef0c93a1a1aeee4c4407c9b85c320029";
            var abi =
                @"[ { ""constant"": true, ""inputs"": [], ""name"": ""returnSender"", ""outputs"": [ { ""name"": """", ""type"": ""address"", ""value"": ""0x108b08336f8890a3f5d091b1f696c67b13b19c4d"" } ], ""payable"": false, ""stateMutability"": ""view"", ""type"": ""function"" } ]";
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(abi, byteCode, senderAddress,
                    new HexBigInteger(900000));

            var contractAddress = receipt.ContractAddress;
            var contract = web3.Eth.GetContract(abi, contractAddress);
            var function = contract.GetFunction("returnSender");
            var returnAddress = await function.CallAsync<string>(senderAddress, new HexBigInteger(900000), null,
                BlockParameter.CreateLatest());
            Assert.Equal(senderAddress.ToLower(), returnAddress.ToLower());
        }

        [Fact]
        public async void TestOriginalStringSignature()
        {
            var byteCode =
                "0x6060604052341561000f57600080fd5b60ac8061001d6000396000f300606060405260043610603e5763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416635170a9d081146043575b600080fd5b3415604d57600080fd5b6053607c565b60405173ffffffffffffffffffffffffffffffffffffffff909116815260200160405180910390f35b33905600a165627a7a72305820ad71c73577f8423259abb92d0e9aad1a0e98ef0c93a1a1aeee4c4407c9b85c320029";
            var abi =
                @"function returnSender() public view returns (address)";
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(abi, byteCode, senderAddress,
                    new HexBigInteger(900000));

            var contractAddress = receipt.ContractAddress;
            var contract = web3.Eth.GetContract(abi, contractAddress);
            var function = contract.GetFunction("returnSender");
            var returnAddress = await function.CallAsync<string>(senderAddress, new HexBigInteger(900000), null,
                BlockParameter.CreateLatest());
            Assert.Equal(senderAddress.ToLower(), returnAddress.ToLower());
        }

        //Smart contract
        /*pragma solidity ^0.4.4;
        contract TestContract{
            function returnSender() public view returns (address) {
                return msg.sender;
            }
        }*/

        public class TestContractDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "0x6060604052341561000f57600080fd5b60ac8061001d6000396000f300606060405260043610603e5763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416635170a9d081146043575b600080fd5b3415604d57600080fd5b6053607c565b60405173ffffffffffffffffffffffffffffffffffffffff909116815260200160405180910390f35b33905600a165627a7a72305820ad71c73577f8423259abb92d0e9aad1a0e98ef0c93a1a1aeee4c4407c9b85c320029";

            public TestContractDeployment() : base(BYTECODE)
            {
            }

            public TestContractDeployment(string byteCode) : base(byteCode)
            {
            }
        }

        [Function("returnSender", "address")]
        public class ReturnSenderFunction : FunctionMessage
        {
        }
    }
}