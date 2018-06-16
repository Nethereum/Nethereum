using System;
using System.Numerics;
using System.Threading.Tasks;
using Common.Logging;
using Common.Logging.Simple;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;
using Nethereum.JsonRpc.Client;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Accounts.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ABIIntegerTests
    {

        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ABIIntegerTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        public class TestDeployment : ContractDeploymentMessage
        {

            public static string BYTECODE = "608060405234801561001057600080fd5b5061014b806100206000396000f3006080604052600436106100615763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166335278d128114610066578063840c54d61461008d578063a0f82817146100a2578063cbf412f3146100b7575b600080fd5b34801561007257600080fd5b5061007b6100cc565b60408051918252519081900360200190f35b34801561009957600080fd5b5061007b6100d1565b3480156100ae57600080fd5b5061007b6100f5565b3480156100c357600080fd5b5061007b6100fb565b600090565b7f800000000000000000000000000000000000000000000000000000000000000090565b60001990565b7f7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff905600a165627a7a723058207167959dd6e9f6ea72dd95ff860a189750a069cde07d6f8dff9be8caafdc90be0029";

            public TestDeployment() : base(BYTECODE) { }

            public TestDeployment(string byteCode) : base(byteCode) { }


        }

        [Function("Max", "uint256")]
        public class MaxFunction : ContractMessage
        {

        }

        [Function("MaxInt256", "int256")]
        public class MaxInt256Function : ContractMessage
        {

        }

        [Function("MinInt256", "int256")]
        public class MinInt256Function : ContractMessage
        {

        }

        [Fact]
        public async Task MinInt256()
        {
            var capturingLoggerAdapter = new CapturingLoggerFactoryAdapter();
            LogManager.Adapter = capturingLoggerAdapter;

            var web3 = GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestDeployment>()
                .SendRequestAndWaitForReceiptAsync();
            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);
            var result = await contractHandler.QueryAsync<MinInt256Function, BigInteger>();
            Assert.Equal(result, BigInteger.Parse("-57896044618658097711785492504343953926634992332820282019728792003956564819968"));
            Assert.Equal("RPC Response: 0x8000000000000000000000000000000000000000000000000000000000000000", 
                capturingLoggerAdapter.LastEvent.MessageObject.ToString());
        }

        [Fact]
        public async Task MaxInt256()
        {
            var capturingLoggerAdapter = new CapturingLoggerFactoryAdapter();
            LogManager.Adapter = capturingLoggerAdapter;

            var web3 = GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestDeployment>()
                .SendRequestAndWaitForReceiptAsync();
            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);
            var result = await contractHandler.QueryAsync<MaxInt256Function, BigInteger>();
            Assert.Equal(result, BigInteger.Parse("57896044618658097711785492504343953926634992332820282019728792003956564819967"));
            Assert.Equal("RPC Response: 0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                capturingLoggerAdapter.LastEvent.MessageObject.ToString());
        }

        [Fact]
        public async Task UMaxInt256()
        {
            var capturingLoggerAdapter = new CapturingLoggerFactoryAdapter();
            LogManager.Adapter = capturingLoggerAdapter;

            var web3 = GetWeb3();
            var deploymentReceipt = await web3.Eth.GetContractDeploymentHandler<TestDeployment>()
                .SendRequestAndWaitForReceiptAsync();
            var contractHandler = web3.Eth.GetContractHandler(deploymentReceipt.ContractAddress);
            var result = await contractHandler.QueryAsync<MaxFunction, BigInteger>();
            
            Assert.Equal("RPC Response: 0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                capturingLoggerAdapter.LastEvent.MessageObject.ToString());
            
            Assert.Equal(result, BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935"));
        }

        public Web3.Web3 GetWeb3()
        {
            var web3 = new Web3.Web3(_ethereumClientIntegrationFixture.GetWeb3().TransactionManager.Account,
                new RpcClient(new Uri("http://localhost:8545"), null, null, null, LogManager.GetLogger<ILog>()));
            return web3;
        }
       
    }
}