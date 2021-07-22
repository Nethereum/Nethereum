using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.XUnitEthereumClients;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting
// ReSharper disable ConsiderUsingConfigureAwait
namespace Nethereum.Contracts.IntegrationTests.CQS
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class FixedMultipleArrayQuery
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public FixedMultipleArrayQuery(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
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
            var result = await contractHandler.QueryAsync<ReturnArrayFunction, List<List<int>>>();
            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Count);
        }

        [Function("returnArray", "int256[2][2]")]
        public class ReturnArrayFunction : FunctionMessage
        {
        }

        public class TestContractDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "0x6060604052341561000f57600080fd5b6101618061001e6000396000f3006060604052600436106100405763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416633cac14c88114610045575b600080fd5b341561005057600080fd5b6100586100b2565b6040516000826002835b818410156100a25782846020020151604080838360005b83811015610091578082015183820152602001610079565b505050509050019260010192610062565b9250505091505060405180910390f35b6100ba6100e2565b6001815152600281516001602002015260018181602002015152600260208201516020015290565b60806040519081016040526002815b6100f961010f565b8152602001906001900390816100f15790505090565b604080519081016040526002815b600081526020019060019003908161011d57905050905600a165627a7a72305820d6fbdcd20aa2df88d4cb7700f4abe8b955740ec5ba3c4101ba0e8819677de5810029";

            public TestContractDeployment() : base(BYTECODE)
            {
            }

            public TestContractDeployment(string byteCode) : base(byteCode)
            {
            }
        }

        /*contract
         * contract TestContract3{
            function returnArray() public view returns(int[2][2] memory array){
                array[0][0] = 1;
                array[0][1] = 2;
                array[1][0] = 1;
                array[1][1] = 2;
                return array;
                }
            }
        */
    }
}