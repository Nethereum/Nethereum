using System.Security.Authentication;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.JsonRpc.Client;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.SmartContracts
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ErrorReasonTest
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ErrorReasonTest(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        //[Fact] Ignoring as Infura does not support this old block
        public async void ShouldRetrieveErrorReason()
        {
            var web3 = new Web3.Web3("https://ropsten.infura.io/v3/7238211010344719ad14a89db874158c");
            var errorReason = await web3.Eth.GetContractTransactionErrorReason.SendRequestAsync("0x2b90deeeb87db3b2b83a91afc48dcc56ce46d1ad84cab1c8864d980dbdec47ec");
            Assert.Equal("SafeMath: subtraction overflow", errorReason);
        }


        //Solidity 
        /*
          pragma solidity ^0.5.0;

        contract Error { 
                function throwIt() view public returns (bool result) {
                    require(false, "An error message");
                return false;
                }
        }
         */

        [Fact]
        public async void ShouldThrowErrorDecodingCall()
        {
            //Parity does throw an RPC exception if the call is reverted, no info included
            if (_ethereumClientIntegrationFixture.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new ErrorThrowDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<ErrorThrowDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);
                var error = await Assert.ThrowsAsync<RpcResponseException>(async () =>
                    await contractHandler.QueryAsync<ThrowItFunction, bool>());
                Assert.Equal("execution reverted: An error message", error.Message);
            }
            else // parity throws Rpc exception : "VM execution error."
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new ErrorThrowDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<ErrorThrowDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);
                var error = await Assert.ThrowsAsync<RpcResponseException>(async () =>
                    await contractHandler.QueryAsync<ThrowItFunction, bool>());
            }
        }


        [Fact]

        public async void ShouldThrowErrorOnEstimation()
        {
            //Parity does throw an RPC exception if the call is reverted, no info included
            if (_ethereumClientIntegrationFixture.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new ErrorThrowDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<ErrorThrowDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);
                var error = await Assert.ThrowsAsync<RpcResponseException>(async () =>
                    await contractHandler.SendRequestAndWaitForReceiptAsync<ThrowItFunction>());
                Assert.Equal("execution reverted: An error message", error.Message);
            }
            else // parity throws Rpc exception : "VM execution error."
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new ErrorThrowDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<ErrorThrowDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);
                var error = await Assert.ThrowsAsync<RpcResponseException>(async () =>
                    await contractHandler.SendRequestAndWaitForReceiptAsync<ThrowItFunction>());
            }
        }



        public partial class ErrorThrowDeployment : ErrorThrowDeploymentBase
        {
            public ErrorThrowDeployment() : base(BYTECODE) { }
            public ErrorThrowDeployment(string byteCode) : base(byteCode) { }
        }

        public class ErrorThrowDeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "6080604052348015600f57600080fd5b5060bf8061001e6000396000f3fe6080604052348015600f57600080fd5b506004361060285760003560e01c8063ab5c4ce514602d575b600080fd5b60336047565b604080519115158252519081900360200190f35b60006040805162461bcd60e51b815260206004820152601060248201526f416e206572726f72206d65737361676560801b604482015290519081900360640190fdfea2646970667358221220b8416f2f906bcee46dace86def6ec94c1f60821a9a5badffc0c374e542112d2564736f6c63430006010033";
            public ErrorThrowDeploymentBase() : base(BYTECODE) { }
            public ErrorThrowDeploymentBase(string byteCode) : base(byteCode) { }

        }

        public partial class ThrowItFunction : ThrowItFunctionBase { }

        [Function("throwIt", "bool")]
        public class ThrowItFunctionBase : FunctionMessage
        {

        }

        public partial class ThrowItOutputDTO : ThrowItOutputDTOBase { }

        [FunctionOutput]
        public class ThrowItOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("bool", "result", 1)]
            public virtual bool Result { get; set; }
        }
    }
}