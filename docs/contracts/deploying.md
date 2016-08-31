
# Step by Step guide to create and deploy a contract to Ethereum

The first step to be able to interact with any contract is to deploy it to the Ethereum chain.  

## Videos

These are two videos that can take you through all the steps, one in the classic windows, visual studio environment and another in a cross platform mac and visual studio code.

### Windows, Visual Studio, .Net 451 Video
This video takes you through the steps of creating the a smart contract, compilet it, start a private chain and deploy it using Nethereum.

[![Smart contracts, private test chain and deployment to Ethereum with Nethereum](http://img.youtube.com/vi/4t5Z3eX59k4/0.jpg)](http://www.youtube.com/watch?v=4t5Z3eX59k4 "Smart contracts, private test chain and deployment to Ethereum with Nethereum")

### Cross platform, Visual Studio Code, .Net core Video

If you want to develop in a cross platform environment this video takes you through same steps but in a Mac using Visual Studio Code and .Net Core.

[![Cross platform development in Ethereum using .Net Core and VsCode and Nethereum](http://img.youtube.com/vi/M1qKcJyQcMY/0.jpg)](http://www.youtube.com/watch?v=M1qKcJyQcMY "Cross platform development in Ethereum using .Net Core and VsCode and Nethereum")


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

## Contract compilation, the Bytecode and the ABI
Before a contract can be deployed it needs to be compiled. Let's quickly see how to do this with Visual Studio Code

### Visual Studio Code 

1. Open Visual Studio Code
2. Copy the contract test into a new file and save it as "test.sol", you will need to have opened a folder as your workspace.
3. If you don't have the Solidity extension press F1 or Shift+Command+P on a mac and type "ext", then search for "solidity" and install it.
4. Now that is installed you can press F1 again type "compile" and select the option to "Compile current contract" 
5. Your abi and bytecode files can be found now in your bin folder.

![VsCode solidity compilation](https://raw.githubusercontent.com/Nethereum/Nethereum/master/docs/screenshots/vscode.png)

## Deployment

### Unlocking the account
First of all you will need to unlock your account to do so you can use web3.Personal.UnlockAccount.

To unlock an account you will need to pass the address, password and the duration in seconds that you want to unlock your account.

```csharp
 var unlockAccountResult =
        await web3.Personal.UnlockAccount.SendRequestAsync(senderAddress, password, new HexBigInteger(120));
```

### The deployment transaction
After unlocking your account you are ready to create the transaction to deploy it.

To create a deployment transaction you will use web3.Eth.DeployContract, using the abi (as we are having a constructor), the byte code, and any parameters to the constructor

```csharp
   var transactionHash =
        await web3.Eth.DeployContract.SendRequestAsync(abi, byteCode, senderAddress, multiplier);
```

Deploying a transaction will return a transactionHash which will be using later on to retrieve the transaction receipt. 

### Mining it

The transaction that has deployed the contract needs to be verified by the network, if we are running a private chain with a single node we will need to mine the transaction.

```csharp
 var mineResult = await web3.Miner.Start.SendRequestAsync(6);
```

### The transaction receipt
Once we have started mining (or we know that are miners in the network) we can can attempt to retrieve the transaction receipt, we will need this as it contains our contract address.

The transaction might have not be mined yet, so when attempting to get the receipt it might return a null value, in this scenario we will continue trying until we get a not null result.

```csharp
   var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

    while (receipt == null)
    {
        Thread.Sleep(5000);
        receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
    }
```

### Stop mining it

```csharp
    var mineResult = await web3.Miner.Stop.SendRequestAsync();
```

### Calling the contract function and return a value
Once we have the receipt, we can retrieve the contract address of our newly deployed contract. Using the contract address and the abi we can create an instance of the Contract object.

Using the contract we can get a Function object using the name of function.

Now with the function we will be able to do a Call to our multiply function by passing a parameter to do the multiplication. 

Note: Calls are not the same as transactions so are not submitted to the network for consensus. Calls are a simple way to retrieve data or do an operation from a contract as our multiplication.

```csharp
    var contractAddress = receipt.ContractAddress;

    var contract = web3.Eth.GetContract(abi, contractAddress);

    var multiplyFunction = contract.GetFunction("multiply");

    var result = await multiplyFunction.CallAsync<int>(7);

    Assert.Equal(49, result);
```

### The final code

All the source code can be found under deployment in the [Tutorials solution](https://github.com/Nethereum/Nethereum/tree/master/src/Nethereum.Tutorials)

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

