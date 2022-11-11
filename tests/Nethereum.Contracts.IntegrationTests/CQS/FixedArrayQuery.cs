using System.Collections.Generic;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.XUnitEthereumClients;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting
// ReSharper disable ConsiderUsingConfigureAwait

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
        public async void MultiDimensionShouldUseSolidityNestedNotationToDescribeReturnButDecodeToCSharp()
        {
            var senderAddress = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var contractAddress = "0x5EF1009b9FCD4fec3094a5564047e190D72Bd511";
            var contractHandler = web3.Eth.GetContractHandler(contractAddress);

            var getPairsByIndexRangeFunction = new GetPairsByIndexRangeFunction();
            getPairsByIndexRangeFunction.UniswapFactory = "0x5C69bEe701ef814a2B6a3EDD4B1652CB9cc5aA6f";
            getPairsByIndexRangeFunction.Start = 0;
            getPairsByIndexRangeFunction.Stop = 10;

            var getPairsByIndexRangeFunctionReturn = await contractHandler.QueryAsync<GetPairsByIndexRangeFunction, List<List<string>>>(getPairsByIndexRangeFunction);

            Assert.Equal(10, getPairsByIndexRangeFunctionReturn.Count);
            Assert.Equal(3, getPairsByIndexRangeFunctionReturn[0].Count);
        }


        public partial class GetPairsByIndexRangeFunction : GetPairsByIndexRangeFunctionBase { }

        [Function("getPairsByIndexRange", "address[3][]")]
        public class GetPairsByIndexRangeFunctionBase : FunctionMessage
        {
            [Parameter("address", "_uniswapFactory", 1)]
            public virtual string UniswapFactory { get; set; }
            [Parameter("uint256", "_start", 2)]
            public virtual BigInteger Start { get; set; }
            [Parameter("uint256", "_stop", 3)]
            public virtual BigInteger Stop { get; set; }
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
            var result = await contractHandler.QueryAsync<ReturnArrayFunction, List<string>>();
            Assert.Equal(10, result.Count);
        }

        public class TestContractDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "0x6060604052341561000f57600080fd5b6101f48061001e6000396000f3006060604052600436106100405763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416633cac14c88114610045575b600080fd5b341561005057600080fd5b610058610091565b604051808261014080838360005b8381101561007e578082015183820152602001610066565b5050505090500191505060405180910390f35b61009961019f565b7312890d2cce102216644c59dae5baed380d84830c81527312890d2cce102216644c59dae5baed380d84830160208201527312890d2cce102216644c59dae5baed380d84830260408201527312890d2cce102216644c59dae5baed380d84830360608201527312890d2cce102216644c59dae5baed380d84830460808201527312890d2cce102216644c59dae5baed380d84830560a08201527312890d2cce102216644c59dae5baed380d84830660c08201527312890d2cce102216644c59dae5baed380d84830760e08201527312890d2cce102216644c59dae5baed380d8483086101008201527312890d2cce102216644c59dae5baed380d84830961012082015290565b610140604051908101604052600a815b6000815260001990910190602001816101af57905050905600a165627a7a723058208d1ac3fcf253acee694131c355fd6eada9fec4570b122d449fe950cc09d4a5490029";

            public TestContractDeployment() : base(BYTECODE)
            {
            }

            public TestContractDeployment(string byteCode) : base(byteCode)
            {
            }
        }

        [Function("returnArray", "address[10]")]
        public class ReturnArrayFunction : FunctionMessage
        {
        }

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