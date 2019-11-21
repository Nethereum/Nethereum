using System.Security.Authentication;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
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

        [Fact]
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
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var errorThrowDeployment = new ErrorThrowDeployment();

            var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<ErrorThrowDeployment>().SendRequestAndWaitForReceiptAsync(errorThrowDeployment);
            var contractAddress = transactionReceiptDeployment.ContractAddress;
         
            var contractHandler = web3.Eth.GetContractHandler(contractAddress);
            var error = await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
                await contractHandler.QueryAsync<ThrowItFunction, bool>());
            Assert.Equal("Smart contract error: An error message", error.Message);
        }

        public partial class ErrorThrowDeployment : ErrorThrowDeploymentBase
        {
            public ErrorThrowDeployment() : base(BYTECODE) { }
            public ErrorThrowDeployment(string byteCode) : base(byteCode) { }
        }

        public class ErrorThrowDeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "6080604052348015600f57600080fd5b5060fe8061001e6000396000f3fe608060405260043610603e5763ffffffff7c0100000000000000000000000000000000000000000000000000000000600035041663ab5c4ce581146043575b600080fd5b348015604e57600080fd5b5060556069565b604080519115158252519081900360200190f35b6000604080517f08c379a000000000000000000000000000000000000000000000000000000000815260206004820152601060248201527f416e206572726f72206d65737361676500000000000000000000000000000000604482015290519081900360640190fdfea165627a7a72305820bf00b5d54a30109fad275cdd6ce90448af11d84cbbf82abae1476b0b79f188830029";
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