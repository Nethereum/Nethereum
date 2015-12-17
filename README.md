# Ethereum.RPC

[![Join the chat at https://gitter.im/juanfranblanco/Ethereum.RPC](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/juanfranblanco/Ethereum.RPC?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Ethereum RPC Client Library in .Net C#, Web3.js in .net.

Work in progress, have a look at the different test projects for usage. 

To startup a development chain you can use https://github.com/juanfranblanco/Ethereum.TestNet.Genesis

##Example of Deploying and calling a function
```csharp
 public dynamic ExecuteTest(RpcClient client)
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
            var transactionHash =  ethSendTransation.SendRequestAsync(client, transactionInput).Result;
            
            //the contract should be mining now

            //get contract address 
            var ethGetTransactionReceipt = new EthGetTransactionReceipt();
            EthTransactionReceipt receipt = null;
            //wait for the contract to be mined to the address
            while (receipt == null)
            {
                receipt = ethGetTransactionReceipt.SendRequestAsync(client, transactionHash).Result;
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
            var resultFunction = ethCall.SendRequestAsync(client, transactionInput).Result;
            // decode the output
            var output = (BigInteger)function.DecodeOutput(resultFunction)[0].Result;
            //visual test 
            return "The result of deploying a contract and calling a function to multiply 7 by 69 is: " + (int)output  + " and should be 483";

           

        }
```
