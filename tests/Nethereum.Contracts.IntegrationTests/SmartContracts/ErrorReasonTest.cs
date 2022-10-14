using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.JsonRpc.Client;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;
using static Nethereum.Contracts.IntegrationTests.SmartContracts.ErrorReasonTest;
// ReSharper disable ConsiderUsingConfigureAwait  
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

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

        //[Fact] //Ignoring as Infura does not support this old block
        //public async void ShouldRetrieveErrorReason()
        //{
        //    var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
        //    //var errorReason =
        //    // await web3.Eth.GetContractTransactionErrorReason.SendRequestAsync("0x9e2831c81b4e3f29a9fb420d50a288d3424b1dd53f7de6bdc423f5429e3be4fc");
        //    // trying to call it will throw an error now

        //    //RpcResponseException error = await Assert.ThrowsAsync<RpcResponseException>(async () =>
        //    await web3.Eth.GetContractTransactionErrorReason.SendRequestAsync("0x9e2831c81b4e3f29a9fb420d50a288d3424b1dd53f7de6bdc423f5429e3be4fc");
        //        //);

        //  //  Assert.Equal("execution reverted: ERC20: transfer amount exceeds balance: eth_call", error.Message);
        //}


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
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new ErrorDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<ErrorDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment).ConfigureAwait(false);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);
                var error = await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
                    await contractHandler.QueryAsync<ThrowItQueryFunction, bool>().ConfigureAwait(false)).ConfigureAwait(false);
                Assert.Equal("An error message", error.RevertMessage);
            }
            else // parity throws Rpc exception : "VM execution error."
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new ErrorDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<ErrorDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment).ConfigureAwait(false);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);
                var error = await Assert.ThrowsAsync<RpcResponseException>(async () =>
                    await contractHandler.QueryAsync<ThrowItQueryFunction, bool>().ConfigureAwait(false)).ConfigureAwait(false);
            }
        }


        [Fact]
        public async void ShouldThrowErrorOnEstimation()
        {
            //Parity does throw an RPC exception if the call is reverted, no info included
            if (_ethereumClientIntegrationFixture.EthereumClient == EthereumClient.Geth)
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new ErrorDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<ErrorDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment).ConfigureAwait(false);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);
                var error = await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
                    await contractHandler.SendRequestAndWaitForReceiptAsync<ThrowItTxnFunction>().ConfigureAwait(false)).ConfigureAwait(false);
                Assert.Equal("An error message", error.RevertMessage);
            }
            else // parity throws Rpc exception : "VM execution error."
            {
                var web3 = _ethereumClientIntegrationFixture.GetWeb3();
                var errorThrowDeployment = new ErrorDeployment();

                var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<ErrorDeployment>()
                    .SendRequestAndWaitForReceiptAsync(errorThrowDeployment).ConfigureAwait(false);
                var contractAddress = transactionReceiptDeployment.ContractAddress;

                var contractHandler = web3.Eth.GetContractHandler(contractAddress);
                var error = await Assert.ThrowsAsync<RpcResponseException>(async () =>
                    await contractHandler.SendRequestAndWaitForReceiptAsync<ThrowItTxnFunction>().ConfigureAwait(false)).ConfigureAwait(false);
            }
        }


        public partial class ErrorDeployment : ErrorDeploymentBase
        {
            public ErrorDeployment() : base(BYTECODE) { }
            public ErrorDeployment(string byteCode) : base(byteCode) { }
        }

        public class ErrorDeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "608060405234801561001057600080fd5b5061010c806100206000396000f3fe6080604052348015600f57600080fd5b506004361060325760003560e01c806351246563146037578063f88b5631146051575b600080fd5b603d6059565b604051901515815260200160405180910390f35b6057609c565b005b60405162461bcd60e51b815260206004820152601060248201526f416e206572726f72206d65737361676560801b60448201526000906064015b60405180910390fd5b60405162461bcd60e51b815260206004820152601060248201526f416e206572726f72206d65737361676560801b6044820152606401609356fea2646970667358221220f26c20ede67912c665386ca928fab378febc6fa989f204d778b967f6bed6f48264736f6c63430008110033";
            public ErrorDeploymentBase() : base(BYTECODE) { }
            public ErrorDeploymentBase(string byteCode) : base(byteCode) { }

        }

        public partial class ThrowItQueryFunction : ThrowItQueryFunctionBase { }

        [Function("throwItQuery", "bool")]
        public class ThrowItQueryFunctionBase : FunctionMessage
        {

        }

        public partial class ThrowItTxnFunction : ThrowItTxnFunctionBase { }

        [Function("throwItTxn")]
        public class ThrowItTxnFunctionBase : FunctionMessage
        {

        }

        public partial class ThrowItQueryOutputDTO : ThrowItQueryOutputDTOBase { }

        [FunctionOutput]
        public class ThrowItQueryOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("bool", "result", 1)]
            public virtual bool Result { get; set; }
        }

    }

}