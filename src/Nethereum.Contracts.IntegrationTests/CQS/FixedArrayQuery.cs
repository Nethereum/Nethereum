using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.CQS
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class FixedArrayQuery
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public FixedArrayQuery(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void TestCQS()
        {
            var senderAddress = AccountFactory.Address;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var deploymentMessage = new TestContractDeployment() {FromAddress = senderAddress};
            var deploymentHandler = web3.Eth.GetContractDeploymentHandler<TestContractDeployment>();
            var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractHandler = web3.Eth.GetContractHandler(transactionReceipt.ContractAddress);
            var result = await contractHandler.QueryAsync<ReturnArrayFunction, List<string>>();
            Assert.Equal(10, result.Count);
        }

        public class TestContractDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE = "0x6060604052341561000f57600080fd5b6101f48061001e6000396000f3006060604052600436106100405763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416633cac14c88114610045575b600080fd5b341561005057600080fd5b610058610091565b604051808261014080838360005b8381101561007e578082015183820152602001610066565b5050505090500191505060405180910390f35b61009961019f565b7312890d2cce102216644c59dae5baed380d84830c81527312890d2cce102216644c59dae5baed380d84830160208201527312890d2cce102216644c59dae5baed380d84830260408201527312890d2cce102216644c59dae5baed380d84830360608201527312890d2cce102216644c59dae5baed380d84830460808201527312890d2cce102216644c59dae5baed380d84830560a08201527312890d2cce102216644c59dae5baed380d84830660c08201527312890d2cce102216644c59dae5baed380d84830760e08201527312890d2cce102216644c59dae5baed380d8483086101008201527312890d2cce102216644c59dae5baed380d84830961012082015290565b610140604051908101604052600a815b6000815260001990910190602001816101af57905050905600a165627a7a723058208d1ac3fcf253acee694131c355fd6eada9fec4570b122d449fe950cc09d4a5490029";
            public TestContractDeployment() : base(BYTECODE) { }

            public TestContractDeployment(string byteCode) : base(byteCode) { }
        }

        [Function("returnArray", "address[10]")]
        public class ReturnArrayFunction : FunctionMessage {}

        //*Smart contract
        /*
         contract TestContrac2{
        function returnArray() public view returns(address[10] memory addresses){
            addresses[0] = 0x12890D2cce102216644c59daE5baed380d84830c;
            addresses[1] = 0x12890D2cCe102216644c59DaE5Baed380D848301;
            addresses[2] = 0x12890D2cce102216644c59daE5baed380d848302;
            addresses[3] = 0x12890d2cce102216644c59daE5baed380d848303;
            addresses[4] = 0x12890d2cce102216644c59daE5baed380d848304;
            addresses[5] = 0x12890d2cce102216644c59daE5baed380d848305;
            addresses[6] = 0x12890d2cce102216644c59daE5baed380d848306;
            addresses[7] = 0x12890d2cce102216644c59daE5baed380d848307;
            addresses[8] = 0x12890d2cce102216644c59daE5baed380d848308;
            addresses[9] = 0x12890d2cce102216644c59daE5baed380d848309;
            return addresses;
        }
        }
        */
    }
}