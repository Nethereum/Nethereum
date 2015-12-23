# Ethereum.RPC

[![Join the chat at https://gitter.im/juanfranblanco/Ethereum.RPC](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/juanfranblanco/Ethereum.RPC?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Ethereum RPC Client Library in .Net C#, Web3.js in .net.

**Work in progress**, consider this as a working spike for different rpc commands, including calling contracts. Have a look at the different test projects for usage. 

To startup a development chain you can use https://github.com/juanfranblanco/Ethereum.TestNet.Genesis. Note that some of the command line tests uses the account in this chain.

Sugestions, ideas, please raise an issue. Want to collaborate, create a pull request.

##Example of deploying a contract and calling a function

This is an example of all the stages required to deploy and call a contract using the JSON RPC API, it is aimed to also give an understanding of how Ethereum works. Function calls using eth_call will not be mined, and won't use any gas. To mine a "function call" you will need to use a transaction, calling a transaction won't return any values.

Note: Using solc to compile contracts is currently a hit and miss in Windows, the simplest way to compile and develop at the moment is to [use the online solidity compiler](https://chriseth.github.io/browser-solidity/). If you like Visual Studio Code you can try this [languange add on for Solidity](https://marketplace.visualstudio.com/items/JuanBlanco.solidity)

ABI encoding and decoding has been tested on windows/linux for different endiannes. For more info on ABI encoding check the [Ethereum Wiki](https://github.com/ethereum/wiki/wiki/Ethereum-Contract-ABI) 



```csharp
 public Task<dynamic> ExecuteTest(RpcClient client)
        {
           //The compiled solidity contract to be deployed
            //contract test { function multiply(uint a) returns(uint d) { return a * 7; } }
            var contractByteCode = "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";

            //Create a new Eth Send Transanction RPC Handler
            var ethSendTransation = new EthSendTransaction();
            //As the input the compiled contract is the Data, together with our address
            var transactionInput = new EthSendTransactionInput();
            transactionInput.Data = contractByteCode;
            transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";
            // retrieve the hash
            var transactionHash =  await ethSendTransation.SendRequestAsync(client, transactionInput);
            
            //the contract should be mining now

            //get contract address 
            var ethGetTransactionReceipt = new EthGetTransactionReceipt();
            EthTransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                receipt = await ethGetTransactionReceipt.SendRequestAsync(client, transactionHash);
            }

            //Encode and build function parameters 
            var function = new ABI.FunctionCallEncoder();
            
            //Input the function method Sha3Encoded (4 bytes) 
            function.FunctionSha3Encoded = "c6888fa1";
            //Define input and output parameters
            function.InputsParameters = new []{new Parameter() {Name = "a", Type = ABIType.CreateABIType("uint")}};
            function.OutputParameters = new []{new Parameter() {Type = ABIType.CreateABIType("uint")}};
            //encode the function call (function + parameter input)
            //using 69 as the input
            var functionCall = function.EncodeRequest(69);
            //reuse the transaction input, (just the address) 
            //the destination address is the contract address
            transactionInput.To = receipt.ContractAddress;
            //use as data the function call
            transactionInput.Data = functionCall;
            // rpc method to do the call
            var ethCall = new EthCall();
            // call and get the result
            var resultFunction = await ethCall.SendRequestAsync(client, transactionInput);
            // decode the output
            var output =  (BigInteger)function.DecodeOutput(resultFunction)[0].Result;
            //visual test 
            return "The result of deploying a contract and calling a function to multiply 7 by 69 is: " + (int)output  + " and should be 483";

        }
```
### Running on Linux
* Install DNX following the [asp.net guide](http://docs.asp.net/en/latest/getting-started/installing-on-linux.html). (Use coreclr)
* Run dnu restore at the solution level, to restore all packages and dependencies.
    ```dnu restore ```
* Compile using only dnxcore 
    ```dnu build --framework dnxcore50 ```
* Run using dnx (ie dnx text, dnx run)

### Current TODO
This is the current TODO list in order of priority 
* Filters, Events and Logging, together with and end to end example for reference on how Ethereum works.
* BigIntegers everywhere as opposed to long / int64
* Complete other RPC methods.
* Refactor 
* ABI is currently implemented following the structure of ethereumj, so both can be used as a point of reference. Encoding and Decoding is slightly different due .Net little Endian. Instead of using objects, generics could be use for the return types to avoid boxing and unboxing, this will diverge both implementations.
* Introduction of different services for Account, Blockchain, Contract creation, Transaction / Call submission (ie Transfer, Contract call)
* Code generate Contract / Function to simplify usage 
* Example of unit testing contracts (.net driven)
* Example of using [dapple / dappsys](https://github.com/NexusDevelopment/dapple) unit testing (solidity driven).
* Example of windows universal app using a contract (Windows, Mobile, RPI2)
