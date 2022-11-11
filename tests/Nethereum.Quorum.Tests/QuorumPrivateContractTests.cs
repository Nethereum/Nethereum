using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.RPC.TransactionReceipts;
using Xunit;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Quorum.Enclave;

namespace Nethereum.Quorum.Tests
{
    public class QuorumPrivateContractTests
    {

       /// private string url = "http://13.91.34.xxx";
        private string url = "http://localhost";

        [Fact]
        public async void PrivateRawTransactionTest()
        {
            var account = new QuorumAccount("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

            var web3Quorum = new Web3Quorum(account, $"{url}:9081", $"{url}:22000");
            
            web3Quorum.SetPrivateRequestParameters(new[] { "BULeR8JyUWhiuuCMU/HLA0Q5pzkYT+cHII3ZKBey3Bo=" }, "BULeR8JyUWhiuuCMU/HLA0Q5pzkYT+cHII3ZKBey3Bo=");
            
            var deploymentMessage1 = new StandardTokenDeployment
            {
                TotalSupply = 100000
            };

            var deploymentHandler = web3Quorum.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
            //Deploying
            var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage1).ConfigureAwait(false);

            var contractAddress = transactionReceipt.ContractAddress;

            var totalSupply = await web3Quorum.Eth.GetContractQueryHandler<TotalSupplyFunction>()
                .QueryAsync<BigInteger>(contractAddress).ConfigureAwait(false);

            //var balance = await web3Quorum.Eth.GetContractQueryHandler<BalanceOfFunction>()
            //    .QueryAsync<BigInteger>(contractAddress,
            //        new BalanceOfFunction() { Owner = "0xed9d02e382b34818e88b88a309c7fe71e65f419d" }); //account.Address });
        }


        [Fact]
        public async void UpCheck()
        {
            var quorumEnclave = new QuorumEnclave(url+ ":9081");
            Assert.True(await quorumEnclave.UpCheckAsync().ConfigureAwait(false));
        }


        public class StandardTokenDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE =
                "0x60606040526040516020806106f5833981016040528080519060200190919050505b80600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005081905550806000600050819055505b506106868061006f6000396000f360606040523615610074576000357c010000000000000000000000000000000000000000000000000000000090048063095ea7b31461008157806318160ddd146100b657806323b872dd146100d957806370a0823114610117578063a9059cbb14610143578063dd62ed3e1461017857610074565b61007f5b610002565b565b005b6100a060048080359060200190919080359060200190919050506101ad565b6040518082815260200191505060405180910390f35b6100c36004805050610674565b6040518082815260200191505060405180910390f35b6101016004808035906020019091908035906020019091908035906020019091905050610281565b6040518082815260200191505060405180910390f35b61012d600480803590602001909190505061048d565b6040518082815260200191505060405180910390f35b61016260048080359060200190919080359060200190919050506104cb565b6040518082815260200191505060405180910390f35b610197600480803590602001909190803590602001909190505061060b565b6040518082815260200191505060405180910390f35b600081600260005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008573ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925846040518082815260200191505060405180910390a36001905061027b565b92915050565b600081600160005060008673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561031b575081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505410155b80156103275750600082115b1561047c5781600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff168473ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a381600160005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600260005060008673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060003373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505403925050819055506001905061048656610485565b60009050610486565b5b9392505050565b6000600160005060008373ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000505490506104c6565b919050565b600081600160005060003373ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020600050541015801561050c5750600082115b156105fb5781600160005060003373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008282825054039250508190555081600160005060008573ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000206000828282505401925050819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef846040518082815260200191505060405180910390a36001905061060556610604565b60009050610605565b5b92915050565b6000600260005060008473ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005060008373ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060005054905061066e565b92915050565b60006000600050549050610683565b9056";

            public StandardTokenDeployment() : base(BYTECODE)
            {
            }

            [Parameter("uint256", "totalSupply")]
            public BigInteger TotalSupply { get; set; }
        }

        [Function("transfer", "bool")]
        public class TransferFunction : FunctionMessage
        {
            [Parameter("address", "_to", 1)]
            public string To { get; set; }

            [Parameter("uint256", "_value", 2)]
            public BigInteger TokenAmount { get; set; }
        }

        [Function("balanceOf", "uint256")]
        public class BalanceOfFunction : FunctionMessage
        {
            [Parameter("address", "_owner", 1)]
            public string Owner { get; set; }
        }

        [Function(name: "totalSupply", returnType: "uint256")]
        public class TotalSupplyFunction : FunctionMessage
        {

        }

        [Fact]
        public async void ShouldBeAbleToConnectTo7NodesPrivate()
        {
            var ipAddress = DefaultSettings.QuorumIPAddress;
            var node1Port = "22000";
            var node2Port = "22001";
            var node7Port = "22006";
            var urlNode1 = ipAddress + ":" + node1Port;
            var urlNode2 = ipAddress + ":" + node2Port;
            var urlNode7 = ipAddress + ":" + node7Port;

            var address = "0x1932c48b2bf8102ba33b4a6b545c32236e342f34";
            var abi = "[{ 'constant':true,'inputs':[],'name':'storedData','outputs':[{'name':'','type':'uint256'}],'payable':false,'type':'function'},{'constant':false,'inputs':[{'name':'x','type':'uint256'}],'name':'set','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'get','outputs':[{'name':'retVal','type':'uint256'}],'payable':false,'type':'function'},{'inputs':[{'name':'initVal','type':'uint256'}],'type':'constructor'}]";
           

            var web3Node1 = new Web3Quorum(urlNode1);
            var transactionService = new TransactionReceiptPollingService(web3Node1.TransactionManager);
            var account = await web3Node1.Eth.CoinBase.SendRequestAsync().ConfigureAwait(false);
            var contract = web3Node1.Eth.GetContract(abi, address);
            var functionSet = contract.GetFunction("set");

            //set the private for
            var privateFor = new List<string>(new[] { "ROAZBWtSacxXQrOe3FGAqJDyJjFePR5ce4TSIzmJ0Bc=" });
            web3Node1.SetPrivateRequestParameters(privateFor);
            //send transaction
            var txnHash = await transactionService.SendRequestAndWaitForReceiptAsync(() => functionSet.SendTransactionAsync(account, 4)).ConfigureAwait(false);

            var node1Value = await GetValue(abi, address, urlNode1).ConfigureAwait(false);
            Assert.Equal(4, node1Value);

            var node2Value = await GetValue(abi, address, urlNode2).ConfigureAwait(false);
            Assert.Equal(0, node2Value);

            var node7Value = await GetValue(abi, address, urlNode7).ConfigureAwait(false);
            Assert.Equal(4, node7Value);

            txnHash = await transactionService.SendRequestAndWaitForReceiptAsync(() => functionSet.SendTransactionAsync(account, 42)).ConfigureAwait(false);

            //node1
            node1Value = await GetValue(abi, address, urlNode1).ConfigureAwait(false);
            Assert.Equal(42, node1Value);

            node2Value = await GetValue(abi, address, urlNode2).ConfigureAwait(false);
            Assert.Equal(0, node2Value);

            node7Value = await GetValue(abi, address, urlNode7).ConfigureAwait(false);
            Assert.Equal(42, node7Value);

            //private.set(4,{from:eth.coinbase,privateFor:["ROAZBWtSacxXQrOe3FGAqJDyJjFePR5ce4TSIzmJ0Bc="]});
        }

        private static async Task<int> GetValue(string abi, string address, string nodeUrl)
        {
            //normal geth is ok
            var web3 = new Web3.Web3(nodeUrl);
            var contract = web3.Eth.GetContract(abi, address);
            var functionGet = contract.GetFunction("get");
            return await functionGet.CallAsync<int>().ConfigureAwait(false);
        }
    }
}
