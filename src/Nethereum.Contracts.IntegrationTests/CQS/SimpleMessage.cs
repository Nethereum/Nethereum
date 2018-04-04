using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.CQS
{
    public class SimpleMessage
    {

        [Fact]
        public async void Test()
        {
            var senderAddress = AccountFactory.Address;
            var web3 = Web3Factory.GetWeb3();

            var deploymentMessage = new TestContractDeployment() {FromAddress = senderAddress};
           

            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TestContractDeployment>();
            var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);

            var contractAddress = transactionReceipt.ContractAddress;
            var contractHandler = web3.Eth.GetContractHandler(contractAddress);

            var returnAddress = await contractHandler.QueryAsync<ReturnSenderFunction, string>(new ReturnSenderFunction(){FromAddress = senderAddress});

           Assert.Equal(senderAddress, returnAddress);
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
            public static string BYTECODE = "0x6060604052341561000f57600080fd5b60ac8061001d6000396000f300606060405260043610603e5763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416635170a9d081146043575b600080fd5b3415604d57600080fd5b6053607c565b60405173ffffffffffffffffffffffffffffffffffffffff909116815260200160405180910390f35b33905600a165627a7a72305820ad71c73577f8423259abb92d0e9aad1a0e98ef0c93a1a1aeee4c4407c9b85c320029";
            public TestContractDeployment() : base(BYTECODE) {}
            public TestContractDeployment(string byteCode) : base(byteCode) { }
        }

        [Function("returnSender", "address")]
        public class ReturnSenderFunction : ContractMessage
        {

        }
    }
}
 