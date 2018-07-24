using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.CQS
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class MultipleByteArray
    {

        /*
         pragma solidity ^0.4.24;
pragma experimental ABIEncoderV2;

contract Test {
  
    function Foo() public returns (bytes[][]) {
        bytes[][] memory blist = new bytes[][](1);
        blist[0] = new bytes[](1);
        blist[0][0] = new bytes(4);
        return blist;
    }
}
         */
        public class TestDeployment : ContractDeploymentMessage
        {

            public static string BYTECODE = "608060405234801561001057600080fd5b506102c4806100206000396000f3006080604052600436106100405763ffffffff7c0100000000000000000000000000000000000000000000000000000000600035041663bfb4ebcf8114610045575b600080fd5b34801561005157600080fd5b5061005a610070565b604051610067919061022e565b60405180910390f35b604080516001808252818301909252606091829190816020015b606081526020019060019003908161008a575050604080516001808252818301909252919250602082015b60608152602001906001900390816100b55750508151829060009081106100d857fe5b602090810290910101526040805160048082528183019092529081602001602082028038833950508251839150600090811061011057fe5b90602001906020020151600081518110151561012857fe5b60209081029091010152905090565b60006101428261024c565b8084526020840193508360208202850161015b85610246565b60005b8481101561019257838303885261017683835161019e565b925061018182610246565b60209890980197915060010161015e565b50909695505050505050565b60006101a98261024c565b808452602084019350836020820285016101c285610246565b60005b848110156101925783830388526101dd8383516101f9565b92506101e882610246565b6020989098019791506001016101c5565b60006102048261024c565b808452610218816020860160208601610250565b61022181610280565b9093016020019392505050565b6020808252810161023f8184610137565b9392505050565b60200190565b5190565b60005b8381101561026b578181015183820152602001610253565b8381111561027a576000848401525b50505050565b601f01601f1916905600a265627a7a723058204990d69439208797efea4464539382a3abd33dd4bcfea3e376d78fc4b96408576c6578706572696d656e74616cf50037";

            public TestDeployment() : base(BYTECODE) { }

            public TestDeployment(string byteCode) : base(byteCode) { }
        }

        [Function("Foo", "bytes[][]")]
        public class FooFunction : FunctionMessage { }

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public MultipleByteArray(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldReturnMultiDimensionalArray()
        {
            var senderAddress = AccountFactory.Address;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TestDeployment>();
            var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync();
            var contractHandler = web3.Eth.GetContractHandler(transactionReceipt.ContractAddress);
            var result = await contractHandler.QueryAsync<FooFunction, List<List<byte[]>>>();
            Assert.True(result[0][0].Length == 4);
        }

    }
}