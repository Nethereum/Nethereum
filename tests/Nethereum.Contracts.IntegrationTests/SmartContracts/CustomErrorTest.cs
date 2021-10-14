using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.XUnitEthereumClients;
using Newtonsoft.Json.Linq;
using System.Numerics;
using Xunit;
// ReSharper disable ConsiderUsingConfigureAwait  
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
{
    /*
      pragma solidity ^0.8.4;

error InsufficientBalance(uint256 available, uint256 required);

contract TestToken {
    mapping(address => uint) balance;
    function transfer(address to, uint256 amount) public {
        if (amount > balance[msg.sender])
            // Error call using named parameters. Equivalent to
            // revert InsufficientBalance(balance[msg.sender], amount);
            revert InsufficientBalance({
                available: balance[msg.sender],
                required: amount
            });
        balance[msg.sender] -= amount;
        balance[to] += amount;
    }
    // ...
}
     */

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class CustomErrorTest
    {
        public partial class TestTokenDeployment: ContractDeploymentMessage
        {
            public static string BYTECODE = "608060405234801561001057600080fd5b5061019b806100206000396000f3fe608060405234801561001057600080fd5b506004361061002b5760003560e01c8063a9059cbb14610030575b600080fd5b61004361003e3660046100ea565b610045565b005b3360009081526020819052604090205481111561009557336000908152602081905260409081902054905163cf47918160e01b815260048101919091526024810182905260440160405180910390fd5b33600090815260208190526040812080548392906100b4908490610138565b90915550506001600160a01b038216600090815260208190526040812080548392906100e1908490610120565b90915550505050565b600080604083850312156100fc578182fd5b82356001600160a01b0381168114610112578283fd5b946020939093013593505050565b600082198211156101335761013361014f565b500190565b60008282101561014a5761014a61014f565b500390565b634e487b7160e01b600052601160045260246000fdfea2646970667358221220036d01bbac8615b9779f8355c03bd4da1057c57188f047db3a3190e81f894f7964736f6c63430008040033";

            public TestTokenDeployment() : base(BYTECODE) { }
            public TestTokenDeployment(string byteCode) : base(byteCode) { }
        }

        public partial class TransferFunction : TransferFunctionBase { }

        [Function("transfer")]
        public class TransferFunctionBase : FunctionMessage
        {
            [Parameter("address", "to", 1)]
            public virtual string To { get; set; }
            [Parameter("uint256", "amount", 2)]
            public virtual BigInteger Amount { get; set; }
        }

        [Error("InsufficientBalance")]
        public class InsufficientBalance
        {
            [Parameter("uint256", "available", 1)]
            public virtual BigInteger Available { get; set; }

            [Parameter("uint256", "required", 1)]
            public virtual BigInteger Required { get; set; }
        }

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public CustomErrorTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact] //estimates are done when making a transaction
        public async void ShouldRetrieveErrorReasonMakingAnEstimateForTransaction()
        {
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new TestTokenDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<TestTokenDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);

                var error = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(async () =>
                   await contractHandler.EstimateGasAsync(new TransferFunction() { Amount = 100, To = EthereumClientIntegrationFixture.AccountAddress }));
                
                Assert.True(error.IsCustomErrorFor<InsufficientBalance>());
                var insufficientBalance = error.DecodeError<InsufficientBalance>();
                Assert.Equal(100, insufficientBalance.Required);
                Assert.Equal(0, insufficientBalance.Available);

            }
        }

        [Fact]
        public async void ShouldRetrieveErrorReasonMakingAQuery()
        {
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new TestTokenDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<TestTokenDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);


                var error = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(async () =>
                    //random return value as it is going to error
                   await contractHandler.QueryAsync<TransferFunction, int>(new TransferFunction() { Amount = 100, To = EthereumClientIntegrationFixture.AccountAddress }));

                Assert.True(error.IsCustomErrorFor<InsufficientBalance>());
                var insufficientBalance = error.DecodeError<InsufficientBalance>();
                Assert.Equal(100, insufficientBalance.Required);
                Assert.Equal(0, insufficientBalance.Available);

            }
        }

        [Fact]
        public async void ShouldFindError()
        {
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new TestTokenDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<TestTokenDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
                var contractAddress = transactionReceiptDeployment.ContractAddress;
                var contractHandler = web3.Eth.GetContractHandler(contractAddress);

                var customErrorException = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(async () =>
                   //random return value as it is going to error
                   await contractHandler.QueryAsync<TransferFunction, int>(new TransferFunction() { Amount = 100, To = EthereumClientIntegrationFixture.AccountAddress }));

                var contract = web3.Eth.GetContract("[{'inputs':[{'internalType':'uint256','name':'available','type':'uint256'},{'internalType':'uint256','name':'required','type':'uint256'}],'name':'InsufficientBalance','type':'error'},{'inputs':[{'internalType':'address','name':'to','type':'address'},{'internalType':'uint256','name':'amount','type':'uint256'}],'name':'transfer','outputs':[],'stateMutability':'nonpayable','type':'function'}]", contractAddress);
                var error = contract.FindError(customErrorException.ExceptionEncodedData);
                Assert.NotNull(error);
                var errorJObject = error.DecodeExceptionEncodedDataToDefault(customErrorException.ExceptionEncodedData).ConvertToJObject();
                var expectedJson = JToken.Parse(@"{'available': '0','required': '100'}");
                Assert.True(JObject.DeepEquals(expectedJson, errorJObject));
            }
        }

        [Fact]
        public async void ShouldRetrieveErrorReasonMakingACall()
        {
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new TestTokenDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<TestTokenDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contract = web3.Eth.GetContract("[{'inputs':[{'internalType':'uint256','name':'available','type':'uint256'},{'internalType':'uint256','name':'required','type':'uint256'}],'name':'InsufficientBalance','type':'error'},{'inputs':[{'internalType':'address','name':'to','type':'address'},{'internalType':'uint256','name':'amount','type':'uint256'}],'name':'transfer','outputs':[],'stateMutability':'nonpayable','type':'function'}]", contractAddress);
                var function = contract.GetFunction("transfer");

                var error = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(async () =>
                   //random return value as it is going to error
                   await function.CallAsync<int>(EthereumClientIntegrationFixture.AccountAddress,100));

                Assert.True(error.IsCustomErrorFor<InsufficientBalance>());
                var insufficientBalance = error.DecodeError<InsufficientBalance>();
                Assert.Equal(100, insufficientBalance.Required);
                Assert.Equal(0, insufficientBalance.Available);

            }
        }

        [Fact]
        public async void ShouldRetrieveErrorReasonMakingAnEstimateCall()
        {
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new TestTokenDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<TestTokenDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contract = web3.Eth.GetContract("[{'inputs':[{'internalType':'uint256','name':'available','type':'uint256'},{'internalType':'uint256','name':'required','type':'uint256'}],'name':'InsufficientBalance','type':'error'},{'inputs':[{'internalType':'address','name':'to','type':'address'},{'internalType':'uint256','name':'amount','type':'uint256'}],'name':'transfer','outputs':[],'stateMutability':'nonpayable','type':'function'}]", contractAddress);
                var function = contract.GetFunction("transfer");

                var error = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(async () =>
                   //random return value as it is going to error
                   await function.EstimateGasAsync(EthereumClientIntegrationFixture.AccountAddress, 100));

                Assert.True(error.IsCustomErrorFor<InsufficientBalance>());
                var insufficientBalance = error.DecodeError<InsufficientBalance>();
                Assert.Equal(100, insufficientBalance.Required);
                Assert.Equal(0, insufficientBalance.Available);
            }
        }
    }
}