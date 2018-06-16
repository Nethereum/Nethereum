using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.Deployment
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ContractDeploymentAndCall
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ContractDeploymentAndCall(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        public async void ShouldDeployAContractAndPerformACall()
        {
            //The compiled solidity contract to be deployed
            //contract test { function multiply(uint a) returns(uint d) { return a * 7; } }
            var contractByteCode =
                "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";

            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""}]";

            var senderAddress = AccountFactory.Address;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var receipt = await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(contractByteCode,
                senderAddress, new HexBigInteger(900000), null, null, null);

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");
            //do a function call (not transaction) and get the result
            var callResult = await multiplyFunction.CallAsync<int>(69);
            Assert.Equal(483, callResult);
        }


        [Fact]
        public async void ShouldDeployAContractWithValueAndSendAValue()
        {
            var contractByteCode =
                "0x6060604052600180546c0100000000000000000000000033810204600160a060020a03199091161790556002340460008190556002023414603e576002565b6103258061004c6000396000f3606060405236156100615760e060020a600035046308551a53811461006657806335a063b41461007d5780633fa4f245146100a05780637150d8ae146100ae57806373fac6f0146100c5578063c19d93fb146100e8578063d696069714610101575b610002565b346100025761011f600154600160a060020a031681565b346100025761013b60015433600160a060020a0390811691161461014f57610002565b346100025761013d60005481565b346100025761011f600254600160a060020a031681565b346100025761013b60025433600160a060020a039081169116146101e457610002565b346100025761013d60025460ff60a060020a9091041681565b61013b60025460009060ff60a060020a90910416156102a457610002565b60408051600160a060020a039092168252519081900360200190f35b005b60408051918252519081900360200190f35b60025460009060a060020a900460ff161561016957610002565b6040517f80b62b7017bb13cf105e22749ee2a06a417ffba8c7f57b665057e0f3c2e925d990600090a16040516002805460a160020a60a060020a60ff0219909116179055600154600160a060020a0390811691309091163180156108fc02916000818181858888f1935050505015156101e157610002565b50565b60025460019060a060020a900460ff1681146101ff57610002565b6040517f64ea507aa320f07ae13c28b5e9bf6b4833ab544315f5f2aa67308e21c252d47d90600090a16040516002805460a060020a60ff02191660a160020a179081905560008054600160a060020a03909216926108fc8315029291818181858888f19350505050158061029a5750600154604051600160a060020a039182169130163180156108fc02916000818181858888f19350505050155b156101e157610002565b6000546002023414806102b657610002565b6040517f764326667cab2f2f13cad5f7b7665c704653bd1acc250dcb7b422bce726896b490600090a150506002805460a060020a73ffffffffffffffffffffffffffffffffffffffff199091166c01000000000000000000000000338102041760a060020a60ff02191617905556";

            var abi =
                "[{'constant':true,'inputs':[],'name':'seller','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':false,'inputs':[],'name':'abort','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'value','outputs':[{'name':'','type':'uint256'}],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'buyer','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':false,'inputs':[],'name':'confirmReceived','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'state','outputs':[{'name':'','type':'uint8'}],'payable':false,'type':'function'},{'constant':false,'inputs':[],'name':'confirmPurchase','outputs':[],'payable':true,'type':'function'},{'inputs':[],'type':'constructor'},{'anonymous':false,'inputs':[],'name':'aborted','type':'event'},{'anonymous':false,'inputs':[],'name':'purchaseConfirmed','type':'event'},{'anonymous':false,'inputs':[],'name':'itemReceived','type':'event'}]";

            var senderAddress = AccountFactory.Address;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var transaction =
                await
                    web3.Eth.DeployContract.SendRequestAsync(abi, contractByteCode,
                        senderAddress, new HexBigInteger(900000), new HexBigInteger(10000));

            var pollingService = new TransactionReceiptPollingService(web3.TransactionManager);
            var receipt = await pollingService.PollForReceiptAsync(transaction);

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var valueFuntion = contract.GetFunction("value");

            //do a function call (not transaction) and get the result
            var callResult = await valueFuntion.CallAsync<int>();
            Assert.Equal(5000, callResult);

            var confirmPurchaseFunction = contract.GetFunction("confirmPurchase");
            var tx = await confirmPurchaseFunction.SendTransactionAsync(senderAddress,
                new HexBigInteger(900000), new HexBigInteger(10000));

            receipt = await pollingService.PollForReceiptAsync(tx);

            var stateFunction = contract.GetFunction("state");
            callResult = await stateFunction.CallAsync<int>();
            Assert.Equal(1, callResult);
        }

        /*
          * pragma solidity ^0.4.0;

contract Purchase {
    uint public value;
    address public seller;
    address public buyer;
    enum State { Created, Locked, Inactive }
    State public state;

    function Purchase() payable {
        seller = msg.sender;
        value = msg.value / 2;
        if (2 * value != msg.value) throw;
    }

    modifier require(bool _condition) {
        if (!_condition) throw;
        _;
    }

    modifier onlyBuyer() {
        if (msg.sender != buyer) throw;
        _;
    }

    modifier onlySeller() {
        if (msg.sender != seller) throw;
        _;
    }

    modifier inState(State _state) {
        if (state != _state) throw;
        _;
    }

    event aborted();
    event purchaseConfirmed();
    event itemReceived();

    /// Abort the purchase and reclaim the ether.
    /// Can only be called by the seller before
    /// the contract is locked.
    function abort()
        onlySeller
        inState(State.Created)
    {
        aborted();
        state = State.Inactive;
        if (!seller.send(this.balance))
            throw;
    }

    /// Confirm the purchase as buyer.
    /// Transaction has to include `2 * value` ether.
    /// The ether will be locked until confirmReceived
    /// is called.
    function confirmPurchase()
        inState(State.Created)
        require(msg.value == 2 * value)
        payable
    {
        purchaseConfirmed();
        buyer = msg.sender;
        state = State.Locked;
    }

    /// Confirm that you (the buyer) received the item.
    /// This will release the locked ether.
    function confirmReceived()
        onlyBuyer
        inState(State.Locked)
    {
        itemReceived();
        // It is important to change the state first because
        // otherwise, the contracts called using `send` below
        // can call in again here.
        state = State.Inactive;
        // This actually allows both the buyer and the seller to
        // block the refund.
        if (!buyer.send(value) || !seller.send(this.balance))
            throw;
    }
}*/

        [Fact]
        public async void ShouldDeployAContractWithValueAndSendAValueUsingSignAndSend()
        {
            var contractByteCode =
                "0x6060604052600180546c0100000000000000000000000033810204600160a060020a03199091161790556002340460008190556002023414603e576002565b6103258061004c6000396000f3606060405236156100615760e060020a600035046308551a53811461006657806335a063b41461007d5780633fa4f245146100a05780637150d8ae146100ae57806373fac6f0146100c5578063c19d93fb146100e8578063d696069714610101575b610002565b346100025761011f600154600160a060020a031681565b346100025761013b60015433600160a060020a0390811691161461014f57610002565b346100025761013d60005481565b346100025761011f600254600160a060020a031681565b346100025761013b60025433600160a060020a039081169116146101e457610002565b346100025761013d60025460ff60a060020a9091041681565b61013b60025460009060ff60a060020a90910416156102a457610002565b60408051600160a060020a039092168252519081900360200190f35b005b60408051918252519081900360200190f35b60025460009060a060020a900460ff161561016957610002565b6040517f80b62b7017bb13cf105e22749ee2a06a417ffba8c7f57b665057e0f3c2e925d990600090a16040516002805460a160020a60a060020a60ff0219909116179055600154600160a060020a0390811691309091163180156108fc02916000818181858888f1935050505015156101e157610002565b50565b60025460019060a060020a900460ff1681146101ff57610002565b6040517f64ea507aa320f07ae13c28b5e9bf6b4833ab544315f5f2aa67308e21c252d47d90600090a16040516002805460a060020a60ff02191660a160020a179081905560008054600160a060020a03909216926108fc8315029291818181858888f19350505050158061029a5750600154604051600160a060020a039182169130163180156108fc02916000818181858888f19350505050155b156101e157610002565b6000546002023414806102b657610002565b6040517f764326667cab2f2f13cad5f7b7665c704653bd1acc250dcb7b422bce726896b490600090a150506002805460a060020a73ffffffffffffffffffffffffffffffffffffffff199091166c01000000000000000000000000338102041760a060020a60ff02191617905556";

            var abi =
                "[{'constant':true,'inputs':[],'name':'seller','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':false,'inputs':[],'name':'abort','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'value','outputs':[{'name':'','type':'uint256'}],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'buyer','outputs':[{'name':'','type':'address'}],'payable':false,'type':'function'},{'constant':false,'inputs':[],'name':'confirmReceived','outputs':[],'payable':false,'type':'function'},{'constant':true,'inputs':[],'name':'state','outputs':[{'name':'','type':'uint8'}],'payable':false,'type':'function'},{'constant':false,'inputs':[],'name':'confirmPurchase','outputs':[],'payable':true,'type':'function'},{'inputs':[],'type':'constructor'},{'anonymous':false,'inputs':[],'name':'aborted','type':'event'},{'anonymous':false,'inputs':[],'name':'purchaseConfirmed','type':'event'},{'anonymous':false,'inputs':[],'name':'itemReceived','type':'event'}]";


            var senderAddress = AccountFactory.Address;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var transaction =
                await
                    web3.Eth.DeployContract.SendRequestAsync(abi, contractByteCode,
                        senderAddress, new HexBigInteger(900000), new HexBigInteger(10000));
            var pollingService = new TransactionReceiptPollingService(web3.TransactionManager);
            var receipt = await pollingService.PollForReceiptAsync(transaction);

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var valueFuntion = contract.GetFunction("value");

            //do a function call (not transaction) and get the result
            var callResult = await valueFuntion.CallAsync<int>();
            Assert.Equal(5000, callResult);

            var confirmPurchaseFunction = contract.GetFunction("confirmPurchase");
            var tx = await confirmPurchaseFunction.SendTransactionAsync(senderAddress,
                new HexBigInteger(900000), new HexBigInteger(10000));

            receipt = await pollingService.PollForReceiptAsync(tx);

            var stateFunction = contract.GetFunction("state");
            callResult = await stateFunction.CallAsync<int>();
            Assert.Equal(1, callResult);
        }

        [Fact]
        public async void ShouldDeployUsingMultipleParameters()
        {
            var contractByteCode =
                "0x606060408181528060bd833960a090525160805160009182556001556095908190602890396000f3606060405260e060020a60003504631df4f1448114601c575b6002565b34600257608360043560015460005460408051918402909202808252915173ffffffffffffffffffffffffffffffffffffffff33169184917f841774c8b4d8511a3974d7040b5bc3c603d304c926ad25d168dacd04e25c4bed9181900360200190a3919050565b60408051918252519081900360200190f3";

            var abi =
                @"[{'constant':false,'inputs':[{'name':'a','type':'int256'}],'name':'multiply','outputs':[{'name':'r','type':'int256'}],'payable':false,'type':'function'},{'inputs':[{'name':'multiplier','type':'int256'},{'name':'another','type':'int256'}],'type':'constructor'},{'anonymous':false,'inputs':[{'indexed':true,'name':'a','type':'int256'},{'indexed':true,'name':'sender','type':'address'},{'indexed':false,'name':'result','type':'int256'}],'name':'Multiplied','type':'event'}]";

            var senderAddress = AccountFactory.Address;
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            var receipt =
                await
                    web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(abi, contractByteCode,
                        senderAddress, new HexBigInteger(900000), null, 7, 8);

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);

            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");

            //do a function call (not transaction) and get the result
            var callResult = await multiplyFunction.CallAsync<int>(69);

            Assert.Equal(3864, callResult);
        }
    }
}