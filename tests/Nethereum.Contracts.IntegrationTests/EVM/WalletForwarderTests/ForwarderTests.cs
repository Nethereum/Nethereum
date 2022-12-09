using System.Numerics;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using Xunit;
using Nethereum.WalletForwarder.Contracts.Forwarder;
using Nethereum.WalletForwarder.Contracts.Forwarder.ContractDefinition;
using Nethereum.WalletForwarder.Contracts.ForwarderFactory;
using Nethereum.WalletForwarder.Contracts.ForwarderFactory.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Contracts;
using Nethereum.ABI.Encoders;
using Nethereum.ABI.Decoders;
using System.Globalization;
using System.Collections.Generic;
using Nethereum.WalletForwarder.Contracts.ERC20Token;
using Nethereum.WalletForwarder.Contracts.ERC20Token.ContractDefinition;
using System.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Nethereum.EVM;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.EVM.BlockchainState;

namespace Nethereum.Contracts.IntegrationTests.EVM.WalletForwarderTests
{
    //These tests are from https://github.com/Nethereum/Nethereum.WalletForwarder
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class ForwarderTests

    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public ForwarderTests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Fact]
        ///This demonstrates creating a contract using create2, accessing that contract using the saved state
        ///transferring ether to that contract and using payable default transfer to a destination address and validating the info from logs
        /// and saved balances of the execution
        public async void ShouldDeployForwarder_CloneItUsingFactory_TransferEther()
        {
            var destinationAddress = "0x6C547791C3573c2093d81b919350DB1094707011";
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            var defaultForwarderDeploymentReceipt = await ForwarderService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderDeployment());
            var defaultForwaderContractAddress = defaultForwarderDeploymentReceipt.ContractAddress;
            var defaultForwarderService = new ForwarderService(web3, defaultForwaderContractAddress);
            await defaultForwarderService.ChangeDestinationRequestAndWaitForReceiptAsync(destinationAddress);
            var destinationInContract = await defaultForwarderService.DestinationQueryAsync();
            Assert.True(destinationInContract.IsTheSameAddress(destinationAddress));

            var factoryDeploymentReceipt = await ForwarderFactoryService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderFactoryDeployment());
            var factoryAddress = factoryDeploymentReceipt.ContractAddress;
            var factoryService = new ForwarderFactoryService(web3, factoryDeploymentReceipt.ContractAddress);

            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var factoryServiceCode = await web3.Eth.GetCode.SendRequestAsync(factoryAddress); // runtime code;

            //New invovice 
            var salt = BigInteger.Parse("12");
            var saltHex = new IntTypeEncoder().Encode(salt).ToHex();

            var contractCalculatedAddress = CalculateCreate2AddressMinimalProxy(factoryAddress, saltHex, defaultForwaderContractAddress);
            var cloneForwarderFunction = new CloneForwarderFunction();
            cloneForwarderFunction.Forwarder = defaultForwaderContractAddress;
            cloneForwarderFunction.Salt = salt;

            var callInput = cloneForwarderFunction.CreateCallInput(factoryAddress);
            callInput.From = EthereumClientIntegrationFixture.AccountAddress;
            callInput.ChainId = new HexBigInteger(EthereumClientIntegrationFixture.ChainId);

            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
            var executionStateService = new ExecutionStateService(nodeDataService);
            var programContext = new ProgramContext(callInput, executionStateService);
            var program = new Program(factoryServiceCode.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();
            await evmSimulator.ExecuteAsync(program, 0, 0, false);

            //var txnReceipt = await factoryService.CloneForwarderRequestAndWaitForReceiptAsync(defaultForwaderContractAddress, salt);
            //var clonedAddress = txnReceipt.DecodeAllEvents<ForwarderClonedEventDTO>()[0].Event.ClonedAdress;
            //Assert.True(clonedAdress.IsTheSameAddress(contractCalculatedAddress));

            var logs = program.ProgramResult.Logs;
            var clonedAddress = logs.DecodeAllEvents<ForwarderClonedEventDTO>()[0].Event.ClonedAdress;
            Assert.True(clonedAddress.IsTheSameAddress(contractCalculatedAddress));
            

            var destinationCallInput = new DestinationFunction()
            { 
                FromAddress = EthereumClientIntegrationFixture.AccountAddress,
            }.CreateCallInput(contractCalculatedAddress);
            destinationCallInput.ChainId = new HexBigInteger(EthereumClientIntegrationFixture.ChainId);
            programContext = new ProgramContext(destinationCallInput, executionStateService);
            var newCode = await executionStateService.GetCodeAsync(contractCalculatedAddress);
            program = new Program(newCode, programContext);
            await evmSimulator.ExecuteAsync(program, 0, 0, false);

            var destinationInContractCloned = new DestinationOutputDTO().DecodeOutput(program.ProgramResult.Result.ToHex()).ReturnValue1;

            //var clonedForwarderService = new ForwarderService(web3, contractCalculatedAddress);
            //var destinationInContractCloned = await clonedForwarderService.DestinationQueryAsync();
            Assert.True(destinationInContractCloned.IsTheSameAddress(destinationAddress));

            var callInputTransferEther = new CallInput()
            {
                From = EthereumClientIntegrationFixture.AccountAddress,
                Value = new HexBigInteger(5000),
                To = contractCalculatedAddress,
                ChainId = new HexBigInteger(EthereumClientIntegrationFixture.ChainId)
        };

            programContext = new ProgramContext(callInputTransferEther, executionStateService);
            newCode = await executionStateService.GetCodeAsync(contractCalculatedAddress);
            program = new Program(newCode, programContext);
            await evmSimulator.ExecuteAsync(program);

            //amounts are transferred to the destination address and an event is set wit the forwarded amount
            logs = program.ProgramResult.Logs;
            var amountSend = logs.DecodeAllEvents<ForwarderDepositedEventDTO>()[0].Event.Value;
            Assert.Equal(5000, amountSend);
            Assert.Equal(5000, executionStateService.CreateOrGetAccountExecutionState(destinationAddress).Balance.ExecutionBalance);
            Assert.Equal(0, executionStateService.CreateOrGetAccountExecutionState(contractCalculatedAddress).Balance.ExecutionBalance);
           

        }


        public static string CalculateCreate2AddressMinimalProxy(string address, string saltHex, string deploymentAddress)
        {
            if (string.IsNullOrEmpty(deploymentAddress))
            {
                throw new System.ArgumentException($"'{nameof(deploymentAddress)}' cannot be null or empty.", nameof(deploymentAddress));
            }

            var bytecode = "3d602d80600a3d3981f3363d3d373d3d3d363d73" + deploymentAddress.RemoveHexPrefix() + "5af43d82803e903d91602b57fd5bf3";
            return ContractUtils.CalculateCreate2Address(address, saltHex, bytecode);
        }



        /*
         / https://eips.ethereum.org/EIPS/eip-20
// SPDX-License-Identifier: MIT
pragma solidity >=0.5.0 <0.8.0;


interface IERC20 {

    /// @param _owner The address from which the balance will be retrieved
    /// @return balance the balance
    function balanceOf(address _owner) external view returns (uint256 balance);

    /// @notice send `_value` token to `_to` from `msg.sender`
    /// @param _to The address of the recipient
    /// @param _value The amount of token to be transferred
    /// @return success Whether the transfer was successful or not
    function transfer(address _to, uint256 _value)  external returns (bool success);

    /// @notice send `_value` token to `_to` from `_from` on the condition it is approved by `_from`
    /// @param _from The address of the sender
    /// @param _to The address of the recipient
    /// @param _value The amount of token to be transferred
    /// @return success Whether the transfer was successful or not
    function transferFrom(address _from, address _to, uint256 _value) external returns (bool success);

    /// @notice `msg.sender` approves `_addr` to spend `_value` tokens
    /// @param _spender The address of the account able to transfer the tokens
    /// @param _value The amount of wei to be approved for transfer
    /// @return success Whether the approval was successful or not
    function approve(address _spender  , uint256 _value) external returns (bool success);

    /// @param _owner The address of the account owning tokens
    /// @param _spender The address of the account able to transfer the tokens
    /// @return remaining Amount of remaining tokens allowed to spent
    function allowance(address _owner, address _spender) external view returns (uint256 remaining);

    event Transfer(address indexed _from, address indexed _to, uint256 _value);
    event Approval(address indexed _owner, address indexed _spender, uint256 _value);
}

contract ERC20Token is IERC20 {
    uint256 constant private MAX_UINT256 = 2**256 - 1;
    mapping (address => uint256) public balances;
    mapping (address => mapping (address => uint256)) public allowed;
    uint256 public totalSupply;
  
    //NOTE:
    ///The following variables are OPTIONAL vanities. One does not have to include them.
    ///They allow one to customise the token contract & in no way influences the core functionality.
    ///Some wallets/interfaces might not even bother to look at this information.
    
        string public name;                   //fancy name: eg Simon Bucks
    uint8 public decimals;                //How many decimals to show.
    string public symbol;                 //An identifier: eg SBX

    constructor(uint256 _initialAmount, string memory _tokenName, uint8 _decimalUnits, string memory _tokenSymbol)
        {
            balances[msg.sender] = _initialAmount;               // Give the creator all initial tokens
            totalSupply = _initialAmount;                        // Update total supply
            name = _tokenName;                                   // Set the name for display purposes
            decimals = _decimalUnits;                            // Amount of decimals for display purposes
            symbol = _tokenSymbol;                               // Set the symbol for display purposes
        }

        function transfer(address _to, uint256 _value) public override returns(bool success)
        {
            require(balances[msg.sender] >= _value, "token balance is lower than the value requested");
            balances[msg.sender] -= _value;
            balances[_to] += _value;
            emit Transfer(msg.sender, _to, _value); //solhint-disable-line indent, no-unused-vars
            return true;
        }

        function transferFrom(address _from, address _to, uint256 _value) public override returns(bool success)
        {
            uint256 allowance = allowed[_from][msg.sender];
            require(balances[_from] >= _value && allowance >= _value, "token balance or allowance is lower than amount requested");
            balances[_to] += _value;
            balances[_from] -= _value;
            if (allowance < MAX_UINT256)
            {
                allowed[_from][msg.sender] -= _value;
            }
            emit Transfer(_from, _to, _value); //solhint-disable-line indent, no-unused-vars
            return true;
        }

        function balanceOf(address _owner) public override view returns(uint256 balance)
        {
            return balances[_owner];
        }

        function approve(address _spender, uint256 _value) public override returns(bool success)
        {
            allowed[msg.sender][_spender] = _value;
            emit Approval(msg.sender, _spender, _value); //solhint-disable-line indent, no-unused-vars
            return true;
        }

        function allowance(address _owner, address _spender) public override view returns(uint256 remaining)
        {
            return allowed[_owner][_spender];
        }
    }
    
     //Contract that will forward any incoming Ether to the creator of the contract
    
    contract Forwarder
    {
        // Address to which any funds sent to this contract will be forwarded
        address payable public destination;
  bool inititalised = false;

    event ForwarderDeposited(address from, uint value, bytes data);
  event TokensFlushed(address forwarderAddress, uint value, address tokenContractAddress);

    
     //Create the contract, and sets the destination address to that of the creator
     // set initialised true for the default forwarder on normal contract deployment
     
    constructor() {
        destination = msg.sender;
        inititalised = true;
    }


    modifier onlyDestination
    {
        if (msg.sender != destination)
        {
            revert("Only destination");
        }
        _;
    }
    //if forwarder is deployed.. forward the payment straight away
    receive() external payable
    {
        destination.transfer(msg.value);
        emit ForwarderDeposited(msg.sender, msg.value, msg.data);
    }

    //init on create2
    function init(address payable newDestination) public {
      if(!inititalised){
          destination = newDestination;
          inititalised = true;
      }
  }
  
  function changeDestination(address payable newDestination) public onlyDestination
{
    destination = newDestination;
}

//flush the tokens
function flushTokens(address tokenContractAddress) public
{
    IERC20 instance = IERC20(tokenContractAddress);
    uint256 forwarderBalance = instance.balanceOf(address(this));
    if (forwarderBalance == 0)
    {
        revert();
    }
    if (!instance.transfer(destination, forwarderBalance))
    {
        revert();
    }
    emit TokensFlushed(address(this), forwarderBalance, tokenContractAddress);
}

function flush() payable public
{
    address payable thisContract = address(this);
    destination.transfer(thisContract.balance);
}

//simple withdraw instead of flush
function withdraw() payable external onlyDestination{
      address payable thisContract = address(this);
msg.sender.transfer(thisContract.balance);
  }
}

contract ForwarderFactory
{

  event ForwarderCloned(address clonedAdress);

    function cloneForwarder(address payable forwarder, uint256 salt)
      public returns (Forwarder clonedForwarder)
{
    address payable clonedAddress = createClone(forwarder, salt);
    Forwarder parentForwarder = Forwarder(forwarder);
    clonedForwarder = Forwarder(clonedAddress);
    clonedForwarder.init(parentForwarder.destination());
    emit ForwarderCloned(clonedAddress);
}

function createClone(address target, uint256 salt) private returns (address payable result)
{
    bytes20 targetBytes = bytes20(target);
    assembly {
        let clone := mload(0x40)
      mstore(clone, 0x3d602d80600a3d3981f3363d3d373d3d3d363d73000000000000000000000000)
      mstore(add(clone, 0x14), targetBytes)
      mstore(add(clone, 0x28), 0x5af43d82803e903d91602b57fd5bf30000000000000000000000000000000000)
      result:= create2(0, clone, 0x37, salt)
    }
  }

  function flushTokens(address payable[]  memory forwarders, address tokenAddres) public
{
    for (uint index = 0; index < forwarders.length; index++)
    {
        Forwarder forwarder = Forwarder(forwarders[index]);
        forwarder.flushTokens(tokenAddres);
    }
}

function flushEther(address payable[]  memory forwarders) public
{
    for (uint index = 0; index < forwarders.length; index++)
    {
        Forwarder forwarder = Forwarder(forwarders[index]);
        forwarder.flush();
    }
}

} 
         
         
        [Fact]
        public async void ShouldDeployForwarder_TransferEther_CloneItUsingFactory_FlushEther()
        {
            var destinationAddress = "0x6C547791C3573c2093d81b919350DB1094707011";
            //Using ropsten infura 
            //var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Ropsten);
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            //Getting the current Ether balance of the destination, we are going to transfer 0.001 ether
            var balanceDestination = await web3.Eth.GetBalance.SendRequestAsync(destinationAddress);
            var balanceDestinationEther = Web3.Web3.Convert.FromWei(balanceDestination);

            //Deploying first the default forwarder (template for all clones)
            var defaultForwarderDeploymentReceipt = await ForwarderService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderDeployment());
            var defaultForwaderContractAddress = defaultForwarderDeploymentReceipt.ContractAddress;
            var defaultForwarderService = new ForwarderService(web3, defaultForwaderContractAddress);
            //initialiasing with the destination address
            await defaultForwarderService.ChangeDestinationRequestAndWaitForReceiptAsync(destinationAddress);
            var destinationInContract = await defaultForwarderService.DestinationQueryAsync();
            //validate the destination address has been set correctly
            Assert.True(destinationInContract.IsTheSameAddress(destinationAddress));

            //Deploying the factory
            var factoryDeploymentReceipt = await ForwarderFactoryService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderFactoryDeployment());
            var factoryAddress = factoryDeploymentReceipt.ContractAddress;
            var factoryService = new ForwarderFactoryService(web3, factoryDeploymentReceipt.ContractAddress);

            //Lets create new invovice to be paid
            var salt = BigInteger.Parse("12"); //12 our invoice number
            var saltHex = new IntTypeEncoder().Encode(salt).ToHex();

            //Calculate the new contract address
            var contractCalculatedAddress = CalculateCreate2AddressMinimalProxy(factoryAddress, saltHex, defaultForwaderContractAddress);

            //Let's tranfer some ether, with some extra gas to allow forwarding if the smart contract is deployed (UX problem)
            var transferEtherReceipt = await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(contractCalculatedAddress, (decimal)0.001, null, 4500000);


            //Check the balance of the adress we sent.. we have not deployed the smart contract so it should be still the same
            var balanceContract = await web3.Eth.GetBalance.SendRequestAsync(contractCalculatedAddress);
            //Assert.Equal((decimal)0.001, Web3.Web3.Convert.FromWei(balanceContract.Value));

            //Create the clone with the salt to match the address
            var txnReceipt = await factoryService.CloneForwarderRequestAndWaitForReceiptAsync(defaultForwaderContractAddress, salt);
            var clonedAdress = txnReceipt.DecodeAllEvents<ForwarderClonedEventDTO>()[0].Event.ClonedAdress;
            Assert.True(clonedAdress.IsTheSameAddress(contractCalculatedAddress));


            //we should still have the same balance
            balanceContract = await web3.Eth.GetBalance.SendRequestAsync(contractCalculatedAddress);
            Assert.Equal((decimal)0.001, Web3.Web3.Convert.FromWei(balanceContract.Value));

            //create a service to for cloned forwarder
            var clonedForwarderService = new ForwarderService(web3, contractCalculatedAddress);
            var destinationInContractCloned = await clonedForwarderService.DestinationQueryAsync();
            //validate the destination address is the same
            Assert.True(destinationInContractCloned.IsTheSameAddress(destinationAddress));

            //Using flush directly in the cloned contract
            //call flush to get all the ether transferred to destination address 
            var flushReceipt = await clonedForwarderService.FlushRequestAndWaitForReceiptAsync();
            balanceContract = await web3.Eth.GetBalance.SendRequestAsync(contractCalculatedAddress);
            //validate balances...
            var newbalanceDestination = await web3.Eth.GetBalance.SendRequestAsync(destinationAddress);
            Assert.Equal((decimal)0.001 + balanceDestinationEther, Web3.Web3.Convert.FromWei(newbalanceDestination));
        }



        [Fact]
        public async void ShouldDeployForwarder_TransferEther_CloneItUsingFactory_FlushEther2ClonesUsingFactory()
        {
            var destinationAddress = "0x6C547791C3573c2093d81b919350DB1094707011";
            //Using ropsten infura 
            //var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Ropsten);
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            //Getting the current Ether balance of the destination, we are going to transfer 0.001 ether
            var balanceDestination = await web3.Eth.GetBalance.SendRequestAsync(destinationAddress);
            var balanceDestinationEther = Web3.Web3.Convert.FromWei(balanceDestination);

            //Deploying first the default forwarder (template for all clones)
            var defaultForwarderDeploymentReceipt = await ForwarderService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderDeployment());
            var defaultForwaderContractAddress = defaultForwarderDeploymentReceipt.ContractAddress;
            var defaultForwarderService = new ForwarderService(web3, defaultForwaderContractAddress);
            //initialiasing with the destination address
            await defaultForwarderService.ChangeDestinationRequestAndWaitForReceiptAsync(destinationAddress);
            var destinationInContract = await defaultForwarderService.DestinationQueryAsync();
            //validate the destination address has been set correctly
            Assert.True(destinationInContract.IsTheSameAddress(destinationAddress));

            //Deploying the factory
            var factoryDeploymentReceipt = await ForwarderFactoryService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderFactoryDeployment());
            var factoryAddress = factoryDeploymentReceipt.ContractAddress;
            var factoryService = new ForwarderFactoryService(web3, factoryDeploymentReceipt.ContractAddress);

            //Lets create new contract to be paid
            var salt = BigInteger.Parse("12"); //salt id
            var saltHex = new IntTypeEncoder().Encode(salt).ToHex();

            //Calculate the new contract address
            var contractCalculatedAddress = CalculateCreate2AddressMinimalProxy(factoryAddress, saltHex, defaultForwaderContractAddress);

            //Let's tranfer some ether, with some extra gas to allow forwarding if the smart contract is deployed (UX problem)
            var transferEtherReceipt = await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(contractCalculatedAddress, (decimal)0.001, null, 4500000);


            //Lets create new contract to be paid
            var salt2 = BigInteger.Parse("13"); //salt id
            var saltHex2 = new IntTypeEncoder().Encode(salt2).ToHex();

            //Calculate the new contract address
            var contractCalculatedAddress2 = CalculateCreate2AddressMinimalProxy(factoryAddress, saltHex2, defaultForwaderContractAddress);

            //Let's tranfer some ether, with some extra gas to allow forwarding if the smart contract is deployed (UX problem)
            var transferEtherReceipt2 = await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(contractCalculatedAddress2, (decimal)0.001, null, 4500000);



            //Create the clone with the salt to match the address
            var txnReceipt = await factoryService.CloneForwarderRequestAndWaitForReceiptAsync(defaultForwaderContractAddress, salt);
            var clonedAdress = txnReceipt.DecodeAllEvents<ForwarderClonedEventDTO>()[0].Event.ClonedAdress;
            Assert.True(clonedAdress.IsTheSameAddress(contractCalculatedAddress));

            //Create the clone2 with the salt to match the address
            var txnReceipt2 = await factoryService.CloneForwarderRequestAndWaitForReceiptAsync(defaultForwaderContractAddress, salt2);
            var clonedAdress2 = txnReceipt2.DecodeAllEvents<ForwarderClonedEventDTO>()[0].Event.ClonedAdress;
            Assert.True(clonedAdress2.IsTheSameAddress(contractCalculatedAddress2));


            //Flushing from the factory
            var flushAllReceipt = await factoryService.FlushEtherRequestAndWaitForReceiptAsync(new List<string> { contractCalculatedAddress, contractCalculatedAddress2 });

            //////validate balances... for two forwarders of 0.001 + 0.001
            var newbalanceDestination = await web3.Eth.GetBalance.SendRequestAsync(destinationAddress);
            Assert.Equal((decimal)0.001 + (decimal)0.001 + balanceDestinationEther, Web3.Web3.Convert.FromWei(newbalanceDestination));
        }



        [Fact]
        public async void ShouldDeployForwarder_TransferEther_CloneItUsingFactory_FlushEtherManyClonesUsingFactory()
        {
            var destinationAddress = "0x6C547791C3573c2093d81b919350DB1094707011";
            //Using ropsten infura 
            //var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Ropsten);
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            //Getting the current Ether balance of the destination, we are going to transfer 0.001 ether
            var balanceDestination = await web3.Eth.GetBalance.SendRequestAsync(destinationAddress);
            var balanceDestinationEther = Web3.Web3.Convert.FromWei(balanceDestination);

            //Deploying first the default forwarder (template for all clones)
            var defaultForwarderDeploymentReceipt = await ForwarderService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderDeployment());
            var defaultForwaderContractAddress = defaultForwarderDeploymentReceipt.ContractAddress;
            var defaultForwarderService = new ForwarderService(web3, defaultForwaderContractAddress);
            //initialiasing with the destination address
            await defaultForwarderService.ChangeDestinationRequestAndWaitForReceiptAsync(destinationAddress);
            var destinationInContract = await defaultForwarderService.DestinationQueryAsync();
            //validate the destination address has been set correctly
            Assert.True(destinationInContract.IsTheSameAddress(destinationAddress));

            //Deploying the factory
            var factoryDeploymentReceipt = await ForwarderFactoryService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderFactoryDeployment());
            var factoryAddress = factoryDeploymentReceipt.ContractAddress;
            var factoryService = new ForwarderFactoryService(web3, factoryDeploymentReceipt.ContractAddress);
            var addresses = await SendEtherAndCreateClones(50, web3, factoryService, 0.001M, factoryAddress, defaultForwaderContractAddress);


            //Flushing from the factory
            var flushAllReceipt = await factoryService.FlushEtherRequestAndWaitForReceiptAsync(addresses);
            //check here the cost ^^^
            var totalEtherTransfered = 0.001M * addresses.Count;

            var newbalanceDestination = await web3.Eth.GetBalance.SendRequestAsync(destinationAddress);
            Assert.Equal(totalEtherTransfered + balanceDestinationEther, Web3.Web3.Convert.FromWei(newbalanceDestination));
        }

        private async Task<List<string>> SendEtherAndCreateClones(int numberOfClones, Web3.Web3 web3, ForwarderFactoryService factoryService, decimal amount, string factoryAddress, string defaultForwaderContractAddress)
        {

            var numProcs = Environment.ProcessorCount;
            var concurrencyLevel = numProcs * 2;
            var concurrentDictionary = new ConcurrentDictionary<int, string>(concurrencyLevel, numberOfClones * 2);
            var taskItems = new List<int>();
            for (var i = 0; i < numberOfClones; i++)
                taskItems.Add(i);

            Parallel.ForEach(taskItems, (item, state) =>
            {
                var id = item.ToString();
                var address = SendEtherAndCreateClone(web3, factoryService, amount, id, factoryAddress, defaultForwaderContractAddress).Result;
                concurrentDictionary.TryAdd(item, address);
            });

            return concurrentDictionary.Values.ToList();
        }


        private async Task<string> SendEtherAndCreateClone(Web3.Web3 web3, ForwarderFactoryService factoryService, decimal amount, string saltNumber, string factoryAddress, string defaultForwaderContractAddress)
        {
            //Lets create new contract to be paid
            var salt = BigInteger.Parse(saltNumber); //salt id
            var saltHex = new IntTypeEncoder().Encode(salt).ToHex();

            //Calculate the new contract address
            var contractCalculatedAddress = CalculateCreate2AddressMinimalProxy(factoryAddress, saltHex, defaultForwaderContractAddress);

            //Let's tranfer some ether, with some extra gas to allow forwarding if the smart contract is deployed (UX problem)
            var transferEtherReceipt = await web3.Eth.GetEtherTransferService().TransferEtherAndWaitForReceiptAsync(contractCalculatedAddress, amount, null, 4500000);

            var txnReceipt = await factoryService.CloneForwarderRequestAndWaitForReceiptAsync(defaultForwaderContractAddress, salt);
            var clonedAdress = txnReceipt.DecodeAllEvents<ForwarderClonedEventDTO>()[0].Event.ClonedAdress;
            Assert.True(clonedAdress.IsTheSameAddress(contractCalculatedAddress));
            return contractCalculatedAddress;
        }

        [Fact]
        public async void ShouldDeployForwarder_TransferToken_CloneItUsingFactory_FlushToken()
        {
            var destinationAddress = "0x6C547791C3573c2093d81b919350DB1094707011";
            //Using ropsten infura 
            //var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Ropsten);
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            //Deploy our custom token
            var tokenDeploymentReceipt = await ERC20TokenService.DeployContractAndWaitForReceiptAsync(web3,
                new ERC20TokenDeployment() { DecimalUnits = 18, TokenName = "TST", TokenSymbol = "TST", InitialAmount = Web3.Web3.Convert.ToWei(10000) });
            var tokenService = new ERC20TokenService(web3, tokenDeploymentReceipt.ContractAddress);

            //Deploying first the default forwarder (template for all clones)
            var defaultForwarderDeploymentReceipt = await ForwarderService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderDeployment());
            var defaultForwaderContractAddress = defaultForwarderDeploymentReceipt.ContractAddress;
            var defaultForwarderService = new ForwarderService(web3, defaultForwaderContractAddress);
            //initialiasing with the destination address
            await defaultForwarderService.ChangeDestinationRequestAndWaitForReceiptAsync(destinationAddress);
            var destinationInContract = await defaultForwarderService.DestinationQueryAsync();
            //validate the destination address has been set correctly
            Assert.True(destinationInContract.IsTheSameAddress(destinationAddress));

            //Deploying the factory
            var factoryDeploymentReceipt = await ForwarderFactoryService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderFactoryDeployment());
            var factoryAddress = factoryDeploymentReceipt.ContractAddress;
            var factoryService = new ForwarderFactoryService(web3, factoryDeploymentReceipt.ContractAddress);

            //Lets create new invovice to be paid
            var salt = BigInteger.Parse("12"); //12 our invoice number
            var saltHex = new IntTypeEncoder().Encode(salt).ToHex();

            //Calculate the new contract address
            var contractCalculatedAddress = CalculateCreate2AddressMinimalProxy(factoryAddress, saltHex, defaultForwaderContractAddress);



            var transferRecipt = await tokenService.TransferRequestAndWaitForReceiptAsync(contractCalculatedAddress, Web3.Web3.Convert.ToWei(0.001));
            //Check the balance of the adress we sent.. we have not deployed the smart contract so it should be still the same
            var balanceContract = await tokenService.BalanceOfQueryAsync(contractCalculatedAddress);
            Assert.Equal((decimal)0.001, Web3.Web3.Convert.FromWei(balanceContract));

            //Create the clone with the salt to match the address
            var txnReceipt = await factoryService.CloneForwarderRequestAndWaitForReceiptAsync(defaultForwaderContractAddress, salt);
            var clonedAdress = txnReceipt.DecodeAllEvents<ForwarderClonedEventDTO>()[0].Event.ClonedAdress;
            Assert.True(clonedAdress.IsTheSameAddress(contractCalculatedAddress));

            //create a service to for cloned forwarder
            var clonedForwarderService = new ForwarderService(web3, contractCalculatedAddress);
            var destinationInContractCloned = await clonedForwarderService.DestinationQueryAsync();
            //validate the destination address is the same
            Assert.True(destinationInContractCloned.IsTheSameAddress(destinationAddress));

            //Using flush directly in the cloned contract
            //call flush to get all the ether transferred to destination address 
            var flushReceipt = await clonedForwarderService.FlushTokensRequestAndWaitForReceiptAsync(tokenService.ContractHandler.ContractAddress);

            //validate balances...
            var newbalanceDestination = await tokenService.BalanceOfQueryAsync(destinationAddress);
            Assert.Equal((decimal)0.001, Web3.Web3.Convert.FromWei(newbalanceDestination));
        }


        [Fact]
        public async void ShouldDeployForwarder_TransferToken_CloneItUsingFactory_FlushTokensUsinFactory()
        {
            var destinationAddress = "0x6C547791C3573c2093d81b919350DB1094707011";
            //Using ropsten infura 
            //var web3 = _ethereumClientIntegrationFixture.GetInfuraWeb3(InfuraNetwork.Ropsten);
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();

            //Deploy our custom token
            var tokenDeploymentReceipt = await ERC20TokenService.DeployContractAndWaitForReceiptAsync(web3,
                new ERC20TokenDeployment() { DecimalUnits = 18, TokenName = "TST", TokenSymbol = "TST", InitialAmount = Web3.Web3.Convert.ToWei(10000) });
            var tokenService = new ERC20TokenService(web3, tokenDeploymentReceipt.ContractAddress);

            //Deploying first the default forwarder (template for all clones)
            var defaultForwarderDeploymentReceipt = await ForwarderService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderDeployment());
            var defaultForwaderContractAddress = defaultForwarderDeploymentReceipt.ContractAddress;
            var defaultForwarderService = new ForwarderService(web3, defaultForwaderContractAddress);
            //initialiasing with the destination address
            await defaultForwarderService.ChangeDestinationRequestAndWaitForReceiptAsync(destinationAddress);
            var destinationInContract = await defaultForwarderService.DestinationQueryAsync();
            //validate the destination address has been set correctly
            Assert.True(destinationInContract.IsTheSameAddress(destinationAddress));

            //Deploying the factory
            var factoryDeploymentReceipt = await ForwarderFactoryService.DeployContractAndWaitForReceiptAsync(web3, new ForwarderFactoryDeployment());
            var factoryAddress = factoryDeploymentReceipt.ContractAddress;
            var factoryService = new ForwarderFactoryService(web3, factoryDeploymentReceipt.ContractAddress);

            //Lets create new salt
            var salt = BigInteger.Parse("12"); //12
            var saltHex = new IntTypeEncoder().Encode(salt).ToHex();

            //Calculate the new contract address
            var contractCalculatedAddress = CalculateCreate2AddressMinimalProxy(factoryAddress, saltHex, defaultForwaderContractAddress);



            //Lets create new salt for another 
            var salt2 = BigInteger.Parse("13"); //13
            var saltHex2 = new IntTypeEncoder().Encode(salt2).ToHex();

            //Calculate the new contract address
            var contractCalculatedAddress2 = CalculateCreate2AddressMinimalProxy(factoryAddress, saltHex2, defaultForwaderContractAddress);


            var transferRecipt = await tokenService.TransferRequestAndWaitForReceiptAsync(contractCalculatedAddress, Web3.Web3.Convert.ToWei(0.001));
            //Check the balance of the adress we sent.. we have not deployed the smart contract so it should be still the same
            var balanceContract = await tokenService.BalanceOfQueryAsync(contractCalculatedAddress);
            Assert.Equal((decimal)0.001, Web3.Web3.Convert.FromWei(balanceContract));

            var transferReceipt2 = await tokenService.TransferRequestAndWaitForReceiptAsync(contractCalculatedAddress2, Web3.Web3.Convert.ToWei(0.001));

            //Create the clone with the salt to match the address
            var txnReceipt = await factoryService.CloneForwarderRequestAndWaitForReceiptAsync(defaultForwaderContractAddress, salt);
            var clonedAdress = txnReceipt.DecodeAllEvents<ForwarderClonedEventDTO>()[0].Event.ClonedAdress;
            Assert.True(clonedAdress.IsTheSameAddress(contractCalculatedAddress));


            var txnReceipt2 = await factoryService.CloneForwarderRequestAndWaitForReceiptAsync(defaultForwaderContractAddress, salt2);
            var clonedAdress2 = txnReceipt2.DecodeAllEvents<ForwarderClonedEventDTO>()[0].Event.ClonedAdress;
            Assert.True(clonedAdress2.IsTheSameAddress(contractCalculatedAddress2));

            //Flushing from the factory
            var flushAllReceipt = await factoryService.FlushTokensRequestAndWaitForReceiptAsync(new List<string> { contractCalculatedAddress, contractCalculatedAddress2 }, tokenService.ContractHandler.ContractAddress);

            //////validate balances... for two forwarders of 0.001 + 0.001
            var newbalanceDestination = await tokenService.BalanceOfQueryAsync(destinationAddress);
            Assert.Equal((decimal)0.001 + (decimal)0.001, Web3.Web3.Convert.FromWei(newbalanceDestination));
        }
            */
    }
}