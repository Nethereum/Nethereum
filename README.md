# Nethereum

[![Join the chat at https://gitter.im/juanfranblanco/Ethereum.RPC](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/juanfranblanco/Ethereum.RPC?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Ethereum is the Web3 RPC Client Library in .Net.

**Work in progress**, consider this as an alpha version.



To startup a development chain you can use https://github.com/juanfranblanco/Ethereum.TestNet.Genesis. Note that some of the command line tests uses the account in this chain.

Sugestions, ideas, please raise an issue. Want to collaborate, create a pull request.

##Example of deploying a contract and calling a function

This is the Web3 example of how to deploy a contract and call a function.

```csharp

public async Task<string> Test()
        {
            //The compiled solidity contract to be deployed
            //contract test { function multiply(uint a) returns(uint d) { return a * 7; } }
            var contractByteCode = "0x606060405260728060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000600782029050606d565b91905056";
            var abi =
                @"[{""constant"":false,""inputs"":[{""name"":""a"",""type"":""uint256""}],""name"":""multiply"",""outputs"":[{""name"":""d"",""type"":""uint256""}],""type"":""function""}]";

            var addressFrom = "0x12890d2cce102216644c59dae5baed380d84830c";
            var web3 = new Web3();

            //deploy the contract, no need to use the abi as we don't have a constructor
            var transactionHash = await web3.Eth.DeployContract.SendRequestAsync(contractByteCode, addressFrom);
           
            //the contract should be mining now

            //get the contract address 
            EthTransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                receipt = await web3.Eth.GetTransactionReceipt.SendRequestAsync(transactionHash);
            }

            var contract = web3.Eth.GetContract(abi, receipt.ContractAddress);
            
            //get the function by name
            var multiplyFunction = contract.GetFunction("multiply");

            //do a function call (not transaction) and get the result
            var result = await multiplyFunction.CallAsync<int>(69);
            //visual test 
            return "The result of deploying a contract and calling a function to multiply 7 by 69 is: " + result + " and should be 483";
        }


```

This is an example of all the stages (internal if you want) required to deploy and call a contract using the JSON RPC API, it is aimed to also give an understanding of how Ethereum works. Function calls using eth_call will not be mined, and won't use any gas. To mine a "function call" you will need to use a transaction, calling a transaction won't return any values.

Note: Using solc to compile contracts is currently a hit and miss in Windows, the simplest way to compile and develop at the moment is to [use the online solidity compiler](https://chriseth.github.io/browser-solidity/). If you like Visual Studio Code you can try this [languange add on for Solidity](https://marketplace.visualstudio.com/items/JuanBlanco.solidity). Are you consuming an external contract and want the function encoded and / or events, try this [Ethereum Sha3 ABI](http://juan.blanco.ws/SHA3/)

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

### Events, logs and filters

This is a demonstration how you can raise events from your solidity contract and filter and capture them from the logs. (This is not implemented yet on the Web3 component)

On the example you can see how a contract is deployed, then using the contract address we subscribe to all the events raised by the contract using filters. Transactions are submitted and the logs are retrived by polling using the filter. From the logs we can retrive the event encoded signature and event data (indexed in the logs and in the data).

```csharp
    /* This is the example contract containing an event raised every time we call multiply
            contract test { 
    
                event Multiplied(uint indexed a, address sender);
    
                function multiply(uint a) returns(uint d) 
                { 
                    Multiplied(a, msg.sender);
                    return a * 7; 
                    
                } 
    
            }*/

            //The contract byte code already compiled
            var contractByteCode = "606060405260c08060106000396000f360606040526000357c010000000000000000000000000000000000000000000000000000000090048063c6888fa1146037576035565b005b604b60048080359060200190919050506061565b6040518082815260200191505060405180910390f35b6000817f10f82b5dc139f3677a16d7bfb70c65252e78143313768d2c52e07db775e1c7ab33604051808273ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390a260078202905060bb565b91905056";
            
            //Create a new Eth Send Transanction RPC Handler
            var ethSendTransation = new EthSendTransaction();

            //Create the transaction input for the new contract

            //On transaction input the compiled contract is the Data, together with our sender address 
            var transactionInput = new EthSendTransactionInput();
            transactionInput.Data = contractByteCode;
            transactionInput.From = "0x12890d2cce102216644c59dae5baed380d84830c";
            // retrieve the transaction hash, as we need to get a transaction receipt with the contract address
            var transactionHash = await ethSendTransation.SendRequestAsync(client, transactionInput);

            //the contract should be mining now

            //Get the transaction receipt using the transactionHash
            var ethGetTransactionReceipt = new EthGetTransactionReceipt();
            EthTransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                receipt = await ethGetTransactionReceipt.SendRequestAsync(client, transactionHash);
            }

            //sha3 the event call, we can use this to validate our topics 
            var eventCall = Encoding.ASCII.GetBytes("Multiplied(uint256,address)").ToHexString();
            var eventCallSh3 = await new Web3.Web3Sha3().SendRequestAsync(client, eventCall);
            //create a filter 
            //just listen to anything no more filter topics (ie int indexed number)
            var ethFilterInput = new EthNewFilterInput();
            ethFilterInput.FromBlockParameter.SetValue(receipt.BlockNumberHex);
            ethFilterInput.ToBlockParameter.SetValue(BlockParameter.BlockParameterType.latest);
            ethFilterInput.Address = new [] { receipt.ContractAddress};
           //no topics
            //ethFilterInput.Topics = new object[]{};

            var newEthFilter = new EthNewFilter();
            var filterId = await newEthFilter.SendRequestAsync(client, ethFilterInput);

           
            //create a transaction which will raise the event
            await SendTransaction(client, transactionInput.From, receipt.ContractAddress);

            //get filter changes
            var ethGetFilterChangesForEthNewFilter = new EthGetFilterChangesForEthNewFilter();
            EthNewFilterLog[] logs = null;

            while (logs == null || logs.Length < 1)
            {
                //Get the filter changes logs
                logs = await ethGetFilterChangesForEthNewFilter.SendRequestAsync(client, filterId);

                if (logs.Length > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Topic 0: " + logs[0].Topics[0] + " should be the same as the SH3 encoded event signature " + eventCallSh3);
                    sb.AppendLine("Topic 1: " + logs[0].Topics[1] + " should be 69 hex  0x45, padded");
                    sb.AppendLine("Data " + logs[0].Data + " should be the same as the address padded 32 bytes " + transactionInput.From);
               
                    return sb.ToString();
                }
                else
                {
                    //create another transaction which will raise the event
                    await SendTransaction(client, transactionInput.From, receipt.ContractAddress);
                }

            }
```

### Encoding / Decoding Function parameters and outputs using DTO

DTOs are supported for the encoding and decoding of the input and output of smart contract functions.

``` csharp
   [Function(Name = "test", Sha3Signature = "c6888fa1")]
        [FunctionOutput]
        public class FunctionMultipleInputOutput
        {
            [Parameter("string")]
            public string A { get; set; }

            [Parameter("uint[20]", "b", 2)]
            public List<BigInteger> B { get; set; }

            [Parameter("string", 3)]
            public string C { get; set; }
        }

        [Fact]
        public virtual void ShouldEncodeMultipleTypesIncludingDynamicStringAndIntArray()
        {
            var paramsEncoded =
                "00000000000000000000000000000000000000000000000000000000000002c0000000000000000000000000000000000000000000000000000000000003944700000000000000000000000000000000000000000000000000000000000394480000000000000000000000000000000000000000000000000000000000039449000000000000000000000000000000000000000000000000000000000003944a000000000000000000000000000000000000000000000000000000000003944b000000000000000000000000000000000000000000000000000000000003944c000000000000000000000000000000000000000000000000000000000003944d000000000000000000000000000000000000000000000000000000000003944e000000000000000000000000000000000000000000000000000000000003944f0000000000000000000000000000000000000000000000000000000000039450000000000000000000000000000000000000000000000000000000000003945100000000000000000000000000000000000000000000000000000000000394520000000000000000000000000000000000000000000000000000000000039453000000000000000000000000000000000000000000000000000000000003945400000000000000000000000000000000000000000000000000000000000394550000000000000000000000000000000000000000000000000000000000039456000000000000000000000000000000000000000000000000000000000003945700000000000000000000000000000000000000000000000000000000000394580000000000000000000000000000000000000000000000000000000000039459000000000000000000000000000000000000000000000000000000000003945a0000000000000000000000000000000000000000000000000000000000000300000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000";

            var function = new FunctionMultipleInputOutput();
            function.A = "hello";
            function.C = "world";

            var array = new BigInteger[20];
            for (uint i = 0; i < 20; i++)
            {
                array[i] = i + 234567;
            }

            function.B = array.ToList();

            var result = new FunctionCallEncoder().EncodeRequest(function);

            Assert.Equal("0x" + "c6888fa1" + paramsEncoded, result);
        }



        [Fact]
        public virtual void ShouldDecodeMultipleTypesIncludingDynamicStringAndIntArray()
        {

            var functionCallDecoder = new FunctionCallDecoder();

            var array = new uint[20];
            for (uint i = 0; i < 20; i++)
            {
                array[i] = i + 234567;
            }

            var result = functionCallDecoder.
                DecodeOutput<FunctionMultipleInputOutput>("0x" + "00000000000000000000000000000000000000000000000000000000000002c0000000000000000000000000000000000000000000000000000000000003944700000000000000000000000000000000000000000000000000000000000394480000000000000000000000000000000000000000000000000000000000039449000000000000000000000000000000000000000000000000000000000003944a000000000000000000000000000000000000000000000000000000000003944b000000000000000000000000000000000000000000000000000000000003944c000000000000000000000000000000000000000000000000000000000003944d000000000000000000000000000000000000000000000000000000000003944e000000000000000000000000000000000000000000000000000000000003944f0000000000000000000000000000000000000000000000000000000000039450000000000000000000000000000000000000000000000000000000000003945100000000000000000000000000000000000000000000000000000000000394520000000000000000000000000000000000000000000000000000000000039453000000000000000000000000000000000000000000000000000000000003945400000000000000000000000000000000000000000000000000000000000394550000000000000000000000000000000000000000000000000000000000039456000000000000000000000000000000000000000000000000000000000003945700000000000000000000000000000000000000000000000000000000000394580000000000000000000000000000000000000000000000000000000000039459000000000000000000000000000000000000000000000000000000000003945a0000000000000000000000000000000000000000000000000000000000000300000000000000000000000000000000000000000000000000000000000000000568656c6c6f0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000005776f726c64000000000000000000000000000000000000000000000000000000"
                );

            Assert.Equal("hello", result.A);
            Assert.Equal("world", result.C);
            Assert.Equal(array[6], result.B[6]);
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
* ~~Generic RPC~~
* ~~ABI encoding, contract deployment, contract transaction and calls example, together with an end to end example on how Ethereum works~~.
* ~~Windows / Linux deployment and unit test~~
* ~~Filters, Events and Logging, together with and end to end example for reference on how Ethereum works.~~
* ~~Refactor~~ 
* ~~BigIntegers everywhere as opposed to long / int64~~
* ~~Hex Types (BigInteger, String) to simplify Rpc encoding~~
* ~~ABI Encoding decoding simplification using DTO pattern and attributes for encoding / decoding values~~
* ~~Extract projects for RPC / ABI / Web3~~
* ~~Create Web3 similar wrapper (wont be the same for contracts / functions) to simplify usage (ie Web3.Eth.Get..)~~
* ~~Complete Eth, Net partial Shh~~
* Add wei conversion support
* Filtering / Events as in Web3
* Examples / Documentation
* Nuget (beta)
* Other shh methods
* Code generate Typed DTO Functions to simplify usage 
* Example of windows universal app using a contract (Windows, Mobile, RPI2)
* Example of unit testing contracts (.net driven)
* Example of using [dapple / dappsys](https://github.com/NexusDevelopment/dapple) unit testing (solidity driven).

