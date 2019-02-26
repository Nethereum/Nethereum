using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.FiltersEvents
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class AnonymousEventFilterWith2Topics
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public AnonymousEventFilterWith2Topics(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async Task TestEvent()
        {
            var bytecode =
                "608060405234801561001057600080fd5b5033600160006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff1602179055506101d8806100616000396000f3fe608060405234801561001057600080fd5b5060043610610048576000357c01000000000000000000000000000000000000000000000000000000009004806329b856881461004d575b600080fd5b6100836004803603604081101561006357600080fd5b810190808035906020019092919080359060200190929190505050610085565b005b6000606060405190810160405280848152602001838152602001600160009054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681525090806001815401808255809150509060018203906000526020600020906003020160009091929091909150600082015181600001556020820151816001015560408201518160020160006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff160217905550505050808233604051808273ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390a2505056fea165627a7a7230582091585526610b2382b1d3830ee346d3e852dab6dc3f9cefb8fe4b450988e126d10029";
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""id"",""type"":""uint256""},{""name"":""price"",""type"":""uint256""}],""name"":""newItem"",""outputs"":[],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[],""payable"":false,""stateMutability"":""nonpayable"",""type"":""constructor""},{""anonymous"":true,""inputs"":[{""indexed"":true,""name"":""itemId"",""type"":""uint256""},{""indexed"":true,""name"":""price"",""type"":""uint256""},{""indexed"":false,""name"":""result"",""type"":""address""}],""name"":""ItemCreated"",""type"":""event""}]";
            
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var addressFrom = AccountFactory.Address;
            var deploymentTransactionReceipt = await DoTransactionAndWaitForReceiptAsync(web3,
                () => web3.Eth.DeployContract.SendRequestAsync(abi, bytecode, addressFrom, new HexBigInteger(900000)));

            var code = await web3.Eth.GetCode.SendRequestAsync(deploymentTransactionReceipt.ContractAddress);

            if (string.IsNullOrEmpty(code))
            {
                throw new Exception(
                    "Code was not deployed correctly, verify bytecode or enough gas was uto deploy the contract");
            }
            
            var contract = web3.Eth.GetContract(abi, deploymentTransactionReceipt.ContractAddress);
            
            var itemCreatedEvent = contract.GetEvent("ItemCreated");
            
            var filter1_ = await itemCreatedEvent.CreateFilterAsync(1);
            var filter_22 = await itemCreatedEvent.CreateFilterAsync<object, int>(null, 22);
            var filter1_22 = await itemCreatedEvent.CreateFilterAsync(1, 22);
            var filter1_11 = await itemCreatedEvent.CreateFilterAsync(1, 11);
            
            var newItemFunction = contract.GetFunction("newItem");
            
            var gas1_11 = await newItemFunction.EstimateGasAsync(1, 11);
            var newItem1_11TransactionReceipt = await DoTransactionAndWaitForReceiptAsync(web3,
                () => newItemFunction.SendTransactionAsync(addressFrom, gas1_11, null, 1, 11));
            var gas2_22 = await newItemFunction.EstimateGasAsync(2, 22);
            var newItem2_22TransactionReceipt = await DoTransactionAndWaitForReceiptAsync(web3,
                () => newItemFunction.SendTransactionAsync(addressFrom, gas2_22, null, 2, 22));
            
            var logs1_Result = await itemCreatedEvent.GetFilterChanges<ItemCreatedEvent>(filter1_);
            Assert.Single(logs1_Result);
            Assert.Equal(1, logs1_Result[0].Event.ItemId);
            Assert.Equal(11, logs1_Result[0].Event.Price);

            var logs_22Result = await itemCreatedEvent.GetFilterChanges<ItemCreatedEvent>(filter_22);
            Assert.Single(logs_22Result);
            Assert.Equal(2, logs_22Result[0].Event.ItemId);
            Assert.Equal(22, logs_22Result[0].Event.Price);

            var logs1_22Result = await itemCreatedEvent.GetFilterChanges<ItemCreatedEvent>(filter1_22);
            Assert.Empty(logs1_22Result);

            var logs1_11Result = await itemCreatedEvent.GetFilterChanges<ItemCreatedEvent>(filter1_11);
            Assert.Single(logs1_11Result);
            Assert.Equal(1, logs1_11Result[0].Event.ItemId);
            Assert.Equal(11, logs1_11Result[0].Event.Price);
        }

        private async Task<TransactionReceipt> DoTransactionAndWaitForReceiptAsync(Web3.Web3 web3, Func<Task<string>> transactionFunc)
        {
            var transactionHash = await transactionFunc();

            TransactionReceipt receipt = null;

            while (receipt == null)
            {
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
                
                if (receipt != null)
                {
                    break;
                }

                await Task.Delay(100);
            }

            return receipt;
        }
        
        [Event("ItemCreated", true)]
        public class ItemCreatedEvent
        {
            [Parameter("uint256", "itemId", 1, true)]
            public BigInteger ItemId { get; set; }
            
            [Parameter("uint256", "price", 2, true)]
            public BigInteger Price { get; set; }

            [Parameter("address", "result", 3, false)]
            public string Result { get; set; }
        }
        
/* Contract 
contract TestAnonymousEventContract {
    struct Item {
        uint id;
        uint price;
        address manager;
    }

    Item[] items;
    address manager;

    constructor() public {
        manager = msg.sender;
    }
    
    event ItemCreated(uint indexed itemId, uint indexed price, address result) anonymous;

    function newItem(uint id, uint price) public {
        items.push(Item(id, price, manager));
        emit ItemCreated(id, price, msg.sender);
    }
}
*/
    }
}