using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.XUnitEthereumClients;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ReceiptStatusTests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ReceiptStatusTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        //note ensure geth  "byzantiumBlock": 0 is set in the genesis.json file to enable status
        [Fact]
        public async Task ShouldReportNoErrorsWhenValid()
        {
            var abi =
                @"[{'constant':false,'inputs':[{'name':'val','type':'int256'}],'name':'multiply','outputs':[{'name':'','type':'int256'}],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[{'name':'multiplier','type':'int256'}],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'from','type':'address'},{'indexed':false,'name':'val','type':'int256'},{'indexed':false,'name':'result','type':'int256'}],'name':'Multiplied','type':'event'}]";

            var smartContractByteCode =
                "6060604052341561000f57600080fd5b604051602080610149833981016040528080516000555050610113806100366000396000f300606060405260043610603e5763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416631df4f14481146043575b600080fd5b3415604d57600080fd5b60566004356068565b60405190815260200160405180910390f35b6000805482027fd01bc414178a5d1578a8b9611adebfeda577e53e89287df879d5ab2c29dfa56a338483604051808473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001838152602001828152602001935050505060405180910390a1929150505600a165627a7a723058201bd2fbd3fb58686ed61df3e636dc4cc7c95b864aa1654bc02b0136e6eca9e9ef0029";

            var accountAddresss = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var multiplier = 2;

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                    abi,
                    smartContractByteCode,
                    accountAddresss,
                    new HexBigInteger(900000),
                    null,
                    multiplier);

            var contractAddress = receipt.ContractAddress;

            var contract = web3.Eth.GetContract(abi, contractAddress);
            var multiplyFunction = contract.GetFunction("multiply");

            //correct gas estimation with a parameter
            var estimatedGas = await multiplyFunction.EstimateGasAsync(7);

            var receipt1 = await multiplyFunction.SendTransactionAndWaitForReceiptAsync(accountAddresss,
                new HexBigInteger(estimatedGas.Value), null, null, 5);

            Assert.Equal(1, receipt1.Status.Value);

            Assert.False(receipt1.HasErrors());
        }


        [Fact]
        public async Task ShouldReportErrorsWhenInValid()
        {
            var abi =
                @"[{'constant':false,'inputs':[{'name':'val','type':'int256'}],'name':'multiply','outputs':[{'name':'','type':'int256'}],'payable':false,'stateMutability':'nonpayable','type':'function'},{'inputs':[{'name':'multiplier','type':'int256'}],'payable':false,'stateMutability':'nonpayable','type':'constructor'},{'anonymous':false,'inputs':[{'indexed':false,'name':'from','type':'address'},{'indexed':false,'name':'val','type':'int256'},{'indexed':false,'name':'result','type':'int256'}],'name':'Multiplied','type':'event'}]";

            var smartContractByteCode =
                "6060604052341561000f57600080fd5b604051602080610149833981016040528080516000555050610113806100366000396000f300606060405260043610603e5763ffffffff7c01000000000000000000000000000000000000000000000000000000006000350416631df4f14481146043575b600080fd5b3415604d57600080fd5b60566004356068565b60405190815260200160405180910390f35b6000805482027fd01bc414178a5d1578a8b9611adebfeda577e53e89287df879d5ab2c29dfa56a338483604051808473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001838152602001828152602001935050505060405180910390a1929150505600a165627a7a723058201bd2fbd3fb58686ed61df3e636dc4cc7c95b864aa1654bc02b0136e6eca9e9ef0029";

            var accountAddresss = EthereumClientIntegrationFixture.AccountAddress;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();


            var multiplier = 2;

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                    abi,
                    smartContractByteCode,
                    accountAddresss,
                    new HexBigInteger(900000),
                    null,
                    multiplier);

            var contractAddress = receipt.ContractAddress;

            var contract = web3.Eth.GetContract(abi, contractAddress);
            var multiplyFunction = contract.GetFunction("multiply");

            //incorrect gas estimation without a parameter
            //it will ran out of gas
            var estimatedGas = await multiplyFunction.EstimateGasAsync();

            var receipt1 = await multiplyFunction.SendTransactionAndWaitForReceiptAsync(accountAddresss,
                new HexBigInteger(estimatedGas.Value), null, null, 5);

            Assert.Equal(0, receipt1.Status.Value);

            Assert.True(receipt1.HasErrors());
        }
    }
}