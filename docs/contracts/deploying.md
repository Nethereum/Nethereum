
# Deployment of Contracts to Ethereum

The first step to be able to interact with any contract is to deploy it to the Ethereum chain.  

[![Smart contracts, private test chain and deployment to Ethereum with Nethereum](http://img.youtube.com/vi/4t5Z3eX59k4/0.jpg)](http://www.youtube.com/watch?v=4t5Z3eX59k4 "Smart contracts, private test chain and deployment to Ethereum with Nethereum")

## The test contract
This is a very simple example of a solidity contract:

```javascript
    contract test { 
               
        uint _multiplier;
               
        function test(uint multiplier){      
             _multiplier = multiplier;
        }
               
        function multiply(uint a) returns(uint d) { 
             return a * _multiplier;
        }
    }
```

The contract named "test" has a  constructor named after the contract (class) and a function multiply.
The function multiply returns the result of the multiplication of a parameter "a" by the value of the "multiplier" provider at time of deployment to the constructor.

### Contract compilation, the Bytecode and the ABI
Before a contract can be deployed it needs to be compiled. Let's quickly see how to do this with Solidity Online or Visual Studio Code

#### Visual Studio Code 

1. Open Visual Studio Code
2. Copy the contract test into a new file and save it as "test.sol"
3. If you don't have the Solidity extension press F1 and type "ext", then search for "solidity" and install it.
4. Now that is installed you can press F1 again type "compile" and select the option to "Compile current contract" 
5. Your abi and bytecode files can be found now in your bin folder.

//screenshot

## Deployment

### Unlocking the account

### The deployment transaction

### Mining it

### The transaction receipt

### Stop mining it

### Verifying it has been deployed

### The final code

```csharp
    var senderAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
    var password = "password";
    var abi = @"[{""constant"":false,""inputs"":[{""name"":""val"",""type"":""int256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""int256""}],""type"":""function""},{""inputs"":[{""name"":""multiplier"",""type"":""int256""}],""type"":""constructor""}]";
    var byteCode =
        "0x60606040526040516020806052833950608060405251600081905550602b8060276000396000f3606060405260e060020a60003504631df4f1448114601a575b005b600054600435026060908152602090f3";

    var multiplier = 7;

    var web3 = new Web3.Web3();
    var unlockAccountResult =
        await web3.Personal.UnlockAccount.SendRequestAsync(senderAddress, password, new HexBigInteger(120));
    Assert.True(unlockAccountResult);

    var transactionHash =
        await web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, multiplier);

    var mineResult = await web3.Miner.Start.SendRequestAsync(6);

    Assert.True(mineResult);

    var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

    while (receipt == null)
    {
        Thread.Sleep(5000);
        receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
    }
    
    mineResult = await web3.Miner.Stop.SendRequestAsync();
    Assert.True(mineResult);

    var contractAddress = receipt.ContractAddress;

    var contract = web3.Eth.GetContract(abi, contractAddress);

    var multiplyFunction = contract.GetFunction("multiply");

    var result = await multiplyFunction.CallAsync<int>(7);

    Assert.Equal(49, result);

```

