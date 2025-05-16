using System.Text.Json.Serialization;
using Nethereum.JsonRpc.Client.RpcMessages;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Threading;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ENS;
using System.Collections.Generic;
using Nethereum.ABI.EIP712;
using Nethereum.Signer.EIP712;
using Nethereum.Signer;
using Nethereum.JsonRpc.SystemTextJsonRpcClient;
using NBitcoin.Secp256k1;


namespace Nethereum.AOTSigningTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var client = new SimpleRpcClient("https://eth.drpc.org");
            var web3 = new Web3.Web3(client);
            var balance = await web3
                .Eth
                .GetBalance.SendRequestAsync("0x742d35Cc6634C0532925a3b844Bc454e4438f44e");
            Console.WriteLine($"Balance: {balance}");

            var block = await web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
            Console.WriteLine($"Block Number: {block.Number}");
            Console.WriteLine($"Block Hash: {block.BlockHash}");
            Console.WriteLine($"Block Transactions: {block.Transactions.Length}");

            var tokenBalance = await web3.Eth.ERC20.GetContractService("0x9f8f72aa9304c8b593d555f12ef6589cc3a579a2")
                                                               .BalanceOfQueryAsync("0x8ee7d9235e01e6b42345120b5d270bdb763624c7");
            //Converting the lowest unit of 18 decimal places of the ERC20 smart contract and display the balance
            Console.WriteLine(Nethereum.Web3.Web3.Convert.FromWei(tokenBalance, 18));

            var localClient = new RpcClient("http://localhost:8545");
            var account = new Nethereum.Web3.Accounts.Account("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");
            var web3Local = new Web3.Web3(account, localClient);

            Nethereum.Signer.EthECKey.SignRecoverable = false;

            var localBalance = await web3Local
                .Eth
                .GetBalance.SendRequestAsync("0x12890d2cce102216644c59daE5baed380d84830c");

            var ethSenderService = web3Local.Eth.GetEtherTransferService();
            var transactionHash = await ethSenderService.TransferEtherAsync("0x12890d2cce102216644c59daE5baed380d84830c", 0.01m);
            Console.WriteLine($"Transaction Hash: {transactionHash}");
            try
            {
                var receipt = await web3Local.TransactionReceiptPolling.PollForReceiptAsync(transactionHash, CancellationToken.None);
                if (receipt != null)
                {
                    Console.WriteLine($"Transaction Receipt: {receipt.TransactionHash}");
                    Console.WriteLine($"Block Number: {receipt.BlockNumber}");
                    Console.WriteLine($"Gas Used: {receipt.GasUsed}");
                    Console.WriteLine($"Status: {receipt.Status}");
                }
                else
                {
                    Console.WriteLine("Transaction receipt not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            //receipt decoding
            var txnReceipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync("0x654288d8536948f30131a769043754bb9af2f5164c6668414751bcfa75c7f4e0");

            if (txnReceipt == null)
            {
                Console.WriteLine("Transaction receipt not found.");
                return;
            }

            Console.WriteLine("Logs");

            foreach (var log in txnReceipt.Logs)
            {
                Console.WriteLine($"Log Address: {log.Address}");
                Console.WriteLine($"Log Data: {log.Data}");
                Console.WriteLine($"Log Topics: {string.Join(", ", log.Topics)}");
            }
            var events = txnReceipt.DecodeAllEvents<TransferEventDTO>();
            Console.WriteLine(events[0].Event.From);
            Console.WriteLine(events[0].Event.To);
            Console.WriteLine(events[0].Event.Value);

            await EventSampleEnd2End(account, web3Local);

            await EnsSamples(web3);
            await EIP712Sample();

            await ErrorSample(web3Local);
            await StructsJson(web3Local);
        }

        public static async Task StructsJson(Web3.Web3 web3)
        {

            Console.WriteLine("Structs Json Sample");
            Nethereum.ABI.ABIDeserialisation.AbiDeserializationSettings.UseSystemTextJson = true;
            var abi =
                @"[{'constant':true,'inputs':[{'name':'','type':'bytes32'},{'name':'','type':'uint256'}],'name':'documents','outputs':[{'name':'name','type':'string'},{'name':'description','type':'string'},{'name':'sender','type':'address'}],'payable':false,'stateMutability':'view','type':'function'},{'constant':false,'inputs':[{'name':'key','type':'bytes32'},{'name':'name','type':'string'},{'name':'description','type':'string'}],'name':'storeDocument','outputs':[{'name':'success','type':'bool'}],'payable':false,'stateMutability':'nonpayable','type':'function'}]";

            var smartContractByteCode =
                "6060604052341561000f57600080fd5b6105408061001e6000396000f30060606040526004361061004b5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166379c17cc581146100505780638553139c14610189575b600080fd5b341561005b57600080fd5b610069600435602435610235565b60405173ffffffffffffffffffffffffffffffffffffffff821660408201526060808252845460026000196101006001841615020190911604908201819052819060208201906080830190879080156101035780601f106100d857610100808354040283529160200191610103565b820191906000526020600020905b8154815290600101906020018083116100e657829003601f168201915b50508381038252855460026000196101006001841615020190911604808252602090910190869080156101775780601f1061014c57610100808354040283529160200191610177565b820191906000526020600020905b81548152906001019060200180831161015a57829003601f168201915b50509550505050505060405180910390f35b341561019457600080fd5b610221600480359060446024803590810190830135806020601f8201819004810201604051908101604052818152929190602084018383808284378201915050505050509190803590602001908201803590602001908080601f01602080910402602001604051908101604052818152929190602084018383808284375094965061028795505050505050565b604051901515815260200160405180910390f35b60006020528160005260406000208181548110151561025057fe5b60009182526020909120600390910201600281015490925060018301915073ffffffffffffffffffffffffffffffffffffffff1683565b6000610291610371565b60606040519081016040908152858252602080830186905273ffffffffffffffffffffffffffffffffffffffff33168284015260008881529081905220805491925090600181016102e2838261039f565b600092835260209092208391600302018151819080516103069291602001906103d0565b506020820151816001019080516103219291602001906103d0565b506040820151600291909101805473ffffffffffffffffffffffffffffffffffffffff191673ffffffffffffffffffffffffffffffffffffffff9092169190911790555060019695505050505050565b60606040519081016040528061038561044e565b815260200161039261044e565b8152600060209091015290565b8154818355818115116103cb576003028160030283600052602060002091820191016103cb9190610460565b505050565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061041157805160ff191683800117855561043e565b8280016001018555821561043e579182015b8281111561043e578251825591602001919060010190610423565b5061044a9291506104b3565b5090565b60206040519081016040526000815290565b6104b091905b8082111561044a57600061047a82826104cd565b6104886001830160006104cd565b5060028101805473ffffffffffffffffffffffffffffffffffffffff19169055600301610466565b90565b6104b091905b8082111561044a57600081556001016104b9565b50805460018160011615610100020316600290046000825580601f106104f35750610511565b601f01602090049060005260206000209081019061051191906104b3565b505600a165627a7a72305820049f1f3ad86cf097dd9c5de014d2e718b5b6b9a05b091d4daebcf60dd3e1213c0029";


            var accountBalance = await web3.Eth.GetBalance.SendRequestAsync(web3.TransactionManager.Account.Address).ConfigureAwait(false);

          

            var receipt =
                await web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                    abi,
                    smartContractByteCode,
                    web3.TransactionManager.Account.Address,
                    new HexBigInteger(900000)).ConfigureAwait(false);

            var contractAddress = receipt.ContractAddress;

            var contract = web3.Eth.GetContract(abi, contractAddress);
            var storeDocumentFunction = contract.GetFunction("storeDocument");

            var receipt1 = await storeDocumentFunction.SendTransactionAndWaitForReceiptAsync(web3.TransactionManager.Account.Address,
                new HexBigInteger(900000), null, null, "k1", "doc1", "Document 1").ConfigureAwait(false);
            
            var receipt2 = await storeDocumentFunction.SendTransactionAndWaitForReceiptAsync(web3.TransactionManager.Account.Address,
                new HexBigInteger(900000), null, null, "k2", "doc2", "Document 2").ConfigureAwait(false);
            

            var documentsFunction = contract.GetFunction("documents");
            var document1 = await documentsFunction.CallDeserializingToObjectAsync<Document>("k1", 0).ConfigureAwait(false);
            var document2 = await documentsFunction.CallDeserializingToObjectAsync<Document>("k2", 0).ConfigureAwait(false);

           Console.WriteLine(document1.Name);
           Console.WriteLine(document1.Description);

            Console.WriteLine(document1.Sender);
            
            Console.WriteLine(document2.Name);
            Console.WriteLine(document2.Description);
            Console.WriteLine(document2.Sender);

        }

        [FunctionOutput]
        public class Document
        {
            [Parameter("string", "name", 1)] public string Name { get; set; }

            [Parameter("string", "description", 2)]
            public string Description { get; set; }

            [Parameter("address", "sender", 3)] public string Sender { get; set; }
        }

        public static async Task ErrorSample(Web3.Web3 web3)
        {
            Console.WriteLine("Error Sample");
            Nethereum.ABI.ABIDeserialisation.AbiDeserializationSettings.UseSystemTextJson = true;
            var errorThrowDeployment = new TestTokenDeployment();

            var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<TestTokenDeployment>()
                .SendRequestAndWaitForReceiptAsync(errorThrowDeployment).ConfigureAwait(false);
            var contractAddress = transactionReceiptDeployment.ContractAddress;

            var contract = web3.Eth.GetContract("[{'inputs':[{'internalType':'uint256','name':'available','type':'uint256'},{'internalType':'uint256','name':'required','type':'uint256'}],'name':'InsufficientBalance','type':'error'},{'inputs':[{'internalType':'address','name':'to','type':'address'},{'internalType':'uint256','name':'amount','type':'uint256'}],'name':'transfer','outputs':[],'stateMutability':'nonpayable','type':'function'}]", contractAddress);
            var function = contract.GetFunction("transfer");

            try
            {
                //random return value as it is going to error
                await function.EstimateGasAsync(web3.TransactionManager.Account.Address, 100).ConfigureAwait(false);

            }
            catch (SmartContractCustomErrorRevertException error)
            {
                Console.WriteLine("Is Custom Error" + error.IsCustomErrorFor<InsufficientBalance>());
                var insufficientBalance = error.DecodeError<InsufficientBalance>();
                Console.WriteLine(insufficientBalance.Required);
                Console.WriteLine(insufficientBalance.Available);

            }
            
           
        }

        [Error("InsufficientBalance")]
        public class InsufficientBalance
        {
            [Parameter("uint256", "available", 1)]
            public virtual BigInteger Available { get; set; }

            [Parameter("uint256", "required", 1)]
            public virtual BigInteger Required { get; set; }
        }

        public partial class TestTokenDeployment : ContractDeploymentMessage
        {
            public static string BYTECODE = "608060405234801561001057600080fd5b5061019b806100206000396000f3fe608060405234801561001057600080fd5b506004361061002b5760003560e01c8063a9059cbb14610030575b600080fd5b61004361003e3660046100ea565b610045565b005b3360009081526020819052604090205481111561009557336000908152602081905260409081902054905163cf47918160e01b815260048101919091526024810182905260440160405180910390fd5b33600090815260208190526040812080548392906100b4908490610138565b90915550506001600160a01b038216600090815260208190526040812080548392906100e1908490610120565b90915550505050565b600080604083850312156100fc578182fd5b82356001600160a01b0381168114610112578283fd5b946020939093013593505050565b600082198211156101335761013361014f565b500190565b60008282101561014a5761014a61014f565b500390565b634e487b7160e01b600052601160045260246000fdfea2646970667358221220036d01bbac8615b9779f8355c03bd4da1057c57188f047db3a3190e81f894f7964736f6c63430008040033";

            public TestTokenDeployment() : base(BYTECODE) { }
            public TestTokenDeployment(string byteCode) : base(byteCode) { }
        }


        public static async Task EIP712Sample() {
            var signer = new Eip712TypedDataSigner();

            //The mail typed definition, this provides the typed data schema used for this specific domain
            var typedData = GetMailTypedDefinition();

            //The data we are going to sign (Primary type) mail
            var mail = new Mail
            {
                From = new Person
                {
                    Name = "Cow",
                    Wallets = new List<string> { "0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "0xDeaDbeefdEAdbeefdEadbEEFdeadbeEFdEaDbeeF" }
                },
                To = new List<Person>
                {
                    new Person
                    {
                        Name = "Bob",
                        Wallets = new List<string> { "0xbBbBBBBbbBBBbbbBbbBbbbbBBbBbbbbBbBbbBBbB", "0xB0BdaBea57B0BDABeA57b0bdABEA57b0BDabEa57", "0xB0B0b0b0b0b0B000000000000000000000000000" }
                    }
                },
                Contents = "Hello, Bob!"
            };

            //This type data is specific to the chainId 1
            typedData.Domain.ChainId = 1;

            var key = new EthECKey("94e001d6adf3a3275d5dd45971c2a5f6637d3e9c51f9693f2e678f649e164fa5");
            Console.WriteLine("Signing address: " + key.GetPublicAddress());

            var signature = signer.SignTypedDataV4(mail, typedData, key);

            Console.WriteLine("Signature: " + signature);

            var addressRecovered = signer.RecoverFromSignatureV4(mail, typedData, signature);
            var address = key.GetPublicAddress();
            Console.WriteLine("Recovered address from signature:" + address);
        }

        //GetMailTypedDefinition is the generic function that contains all the metadata required to sign this domain specific message
        //All the different types (Domain, Group, Mail, Person) are defined as classes in a similar way to Nethereum Function Messages
        //In the standard you need to provide the Domain, this can be extended with your own type,
        //The different types that are pare of the domain
        //and the PrimaryType which is the message entry point
        public static TypedData<Domain> GetMailTypedDefinition()
        {
            return new TypedData<Domain>
            {
                Domain = new Domain
                {
                    Name = "Ether Mail",
                    Version = "1",
                    ChainId = 1,
                    VerifyingContract = "0xCcCCccccCCCCcCCCCCCcCcCccCcCCCcCcccccccC"
                },
                Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(Group), typeof(Mail), typeof(Person)),
                PrimaryType = nameof(Mail),
            };
        }

        //The domain type Mail is defined in a similar way to a Nethereum Function Message, with the attribute Struct("Mail")
        //Parameters are the same, although when working with tuples we need to provide the name of the Tuple like "Person" or "Person[]" if it is an array
        [Struct("Mail")]
        public class Mail
        {
            [Parameter("tuple", "from", 1, "Person")]
            public Person From { get; set; }

            [Parameter("tuple[]", "to", 2, "Person[]")]
            public List<Person> To { get; set; }

            [Parameter("string", "contents", 3)]
            public string Contents { get; set; }
        }

        [Struct("Person")]
        public class Person
        {
            [Parameter("string", "name", 1)]
            public string Name { get; set; }

            [Parameter("address[]", "wallets", 2)]
            public List<string> Wallets { get; set; }
        }

        [Struct("Group")]
        public class Group
        {
            [Parameter("string", "name", 1)]
            public string Name { get; set; }

            [Parameter("tuple[]", "members", 2, "Person[]")]
            public List<Person> Members { get; set; }
        }



        private static async Task EnsSamples(Web3.Web3 web3)
        {
            Console.WriteLine("ENS Samples");
            Console.WriteLine("Resolve Address");
            var ensService = web3.Eth.GetEnsService();
            var theAddress = await ensService.ResolveAddressAsync("nick.eth");
            Console.WriteLine(theAddress);

            Console.WriteLine("Resolve Url");
            var url = await ensService.ResolveTextAsync("nethereum.eth", TextDataKey.url);
            Console.WriteLine(url);
        }

        private static async Task EventSampleEnd2End(Web3.Accounts.Account account, Web3.Web3 web3Local)
        {
            var deploymentMessage = new StandardTokenDeployment
            {
                TotalSupply = 100000
            };


            // Then we create a deployment handler using our contract deployment definition and simply deploy the
            // contract using the deployment message. We are auto estimating the gas, getting the latest gas price
            // and nonce so nothing else is set anything on the deployment message.

            // Finally, we wait for the deployment transaction to be mined, and retrieve the contract address of
            // the new contract from the receipt.


            var deploymentHandler = web3Local.Eth.GetContractDeploymentHandler<StandardTokenDeployment>();
            var transactionReceipt = await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractAddress = transactionReceipt.ContractAddress;
            Console.WriteLine("contractAddress is: " + contractAddress);

            // ### Transfer

            // Once we have deployed the contract, we can execute our first transfer transaction.
            // The transfer function will write to the log the transfer event.

            // First we can create a TransactionHandler using the TrasferFunction definition and a
            // TransferFunction message including the “receiverAddress” and the amount of tokens we want to send.

            // Finally do the transaction transfer and wait for the receipt to be “mined”
            // and included in the blockchain.


            var receiverAddress = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var transferHandler = web3Local.Eth.GetContractTransactionHandler<TransferFunction>();
            var transfer = new TransferFunction()
            {
                To = receiverAddress,
                TokenAmount = 100
            };
            var transactionReceipt2 = await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, transfer);


            // ## Decoding the Event from the TransactionReceipt

            // Event logs are part of the TransactionReceipts, so using the Transaction receipt from the previous
            // transfer we can decode the TransferEvent using the extension method
            // “DecodeAllEvents<TransferEventDTO>()”.

            // Note that this method returns an array of Decoded Transfer Events as opposed to a single value,
            // because the receipt can include more than one event of the same signature.


            var transferEventOutput = transactionReceipt2.DecodeAllEvents<TransferEventDTO>();


            // ## Contract Filters and Event Logs

            // Another way to access the event logs of a smart contract is to either get all changes of the logs
            // (providing a filter message) or create filters and retrieve changes which apply to our filter message
            // periodically.                                  \
            // \
            // To access the logs, first of all, we need to create a transfer event handler for our contract address,
            // and Evend definition.(TransferEventDTO).


            var transferEventHandler = web3Local.Eth.GetEvent<TransferEventDTO>(contractAddress);


            // Using the event handler, we can create a filter message for our transfer event using the default values.

            // The default values for BlockParameters are Earliest and Latest, so when we retrieve the logs
            // we will get all the transfer events from the first block to the latest block of this contract.


            var filterAllTransferEventsForContract = transferEventHandler.CreateFilterInput();


            // Once we have created the message we can retrieve all the logs using the event and GetAllChanges.
            // In this scenario, because we have made only one transfer, we will have only one Transfer Event.


            var allTransferEventsForContract = await transferEventHandler.GetAllChangesAsync(filterAllTransferEventsForContract);

            Console.WriteLine("Transfer event TransactionHash : " + allTransferEventsForContract[0].Log.TransactionHash);

            // If we now make another Transfer to a different address


            var receiverAddress2 = "0x3e0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
            var transfer2 = new TransferFunction()
            {
                To = receiverAddress2,
                TokenAmount = 1000
            };
            var transactionReceipt3 = await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, transfer2);


            // Using the same filter input message and making another GetAllChanges call, we will now get
            // the two Transfer Event logs.


            var allTransferEventsForContract2 =
                await transferEventHandler.GetAllChangesAsync(filterAllTransferEventsForContract);

            Console.WriteLine("Transfer events for Contract2");
            for (var i = 0; i < 2; i++)
                Console.WriteLine("Transfer event number : " + i + " - TransactionHash : " +
                                  allTransferEventsForContract2[i].Log.TransactionHash);


            for (var i = 0; i < 2; i++)
            {
                Console.WriteLine("Transfer event number : " + i + " - TransactionHash : " +
                                  allTransferEventsForContract2[i].Log.TransactionHash);
                Console.WriteLine("From: " + allTransferEventsForContract2[i].Event.From);
                Console.WriteLine("To: " + allTransferEventsForContract2[i].Event.To);
                Console.WriteLine("Value: " + allTransferEventsForContract2[i].Event.Value);
            }


            // Filter messages can limit the results (similar to block ranges) to the indexed parameters,
            // for example we can create a filter for only our sender address AND the receiver address.
            // As a reminder our Event has as indexed parameters the “\_from” address
            // and “\_to” address.


            var filterTransferEventsForContractReceiverAddress2 =
                transferEventHandler.CreateFilterInput(account.Address, receiverAddress2);
            var transferEventsForContractReceiverAddress2 =
                await transferEventHandler.GetAllChangesAsync(filterTransferEventsForContractReceiverAddress2);

            Console.WriteLine("Transfer events for ContractReceiverAddress2 GetAllChanges Filtered From Sender");
            foreach (var transferEvent in transferEventsForContractReceiverAddress2)
            {
                Console.WriteLine("Transfer event TransactionHash : " + transferEvent.Log.TransactionHash);
                Console.WriteLine("From: " + transferEvent.Event.From);
                Console.WriteLine("To: " + transferEvent.Event.To);
                Console.WriteLine("Value: " + transferEvent.Event.Value);
            }


            // The order the filter values is based on the event parameters order, if we want to include all the transfers to the “receiverAddress2”, the account address from will need to be set to null to be ignored.

            // Note: We are using the array format to allow for null input of the first parameter.


            var filterTransferEventsForContractAllReceiverAddress2 =
                transferEventHandler.CreateFilterInput(null, new[] { receiverAddress2 });
            var transferEventsForContractAllReceiverAddress2 =
                await transferEventHandler.GetAllChangesAsync(filterTransferEventsForContractAllReceiverAddress2);



            Console.WriteLine("Transfer events for ContractReceiverAddress2 GetAllChanges Filtered Recipient");
            foreach (var transferEvent in transferEventsForContractReceiverAddress2)
            {
                Console.WriteLine("Transfer event TransactionHash : " + transferEvent.Log.TransactionHash);
                Console.WriteLine("From: " + transferEvent.Event.From);
                Console.WriteLine("To: " + transferEvent.Event.To);
                Console.WriteLine("Value: " + transferEvent.Event.Value);
            }

            // Another scenario is when you want to include multiple indexed values, for example transfers for
            // “receiverAddress1” OR “receiverAddress2”.
            // Then you will need to use an array of the values you are interested.


            var filterTransferEventsForContractAllReceiverAddresses =
                transferEventHandler.CreateFilterInput(null, new[] { receiverAddress2, receiverAddress });
            var transferEventsForContractAllReceiverAddresses =
                await transferEventHandler.GetAllChangesAsync(filterTransferEventsForContractAllReceiverAddresses);

            Console.WriteLine("Transfer events for ContractReceiverAddress2 GetAllChanges Filtered From Or Recipient");
            foreach (var transferEvent in transferEventsForContractReceiverAddress2)
            {
                Console.WriteLine("Transfer event TransactionHash : " + transferEvent.Log.TransactionHash);
                Console.WriteLine("From: " + transferEvent.Event.From);
                Console.WriteLine("To: " + transferEvent.Event.To);
                Console.WriteLine("Value: " + transferEvent.Event.Value);
            }

            // ### Creating filters to retrieve periodic changes

            // Another option is to create filters that return only the changes occurred since we got the previous results.
            // This eliminates the need of tracking the last block the events were checked and delegate this
            // to the Ethereum client.

            // Using the same filter message we created before we can create the filter and get the filterId.


            var filterIdTransferEventsForContractAllReceiverAddress2 =
                await transferEventHandler.CreateFilterAsync(filterTransferEventsForContractAllReceiverAddress2);


            // One thing to note, if  try to get the filter changes now, we will not get any results because
            // the filter only returns the changes since creation.


            var result = await transferEventHandler.GetFilterChangesAsync(filterIdTransferEventsForContractAllReceiverAddress2);


            // But, if we make another transfer using the same values


            await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress, transfer2);


            // and execute get filter changes using the same filter id, we will get the event for the previous transfer.


            var result2 = await transferEventHandler.GetFilterChangesAsync(filterIdTransferEventsForContractAllReceiverAddress2);
            Console.WriteLine("result2/TransactionHash: " + result2[0].Log.TransactionHash);


            // Executing the same again will return no results because no new transfers have occurred
            // since the last execution of GetFilterChanges.


            var result3 = await transferEventHandler.GetFilterChangesAsync(filterIdTransferEventsForContractAllReceiverAddress2);
            Console.WriteLine("result3/Transaction Count: " + result3.Count);

            // ## Events for all Contracts

            // Different contracts can have and raise/log the same event with the same signature,
            // a simple example is the multiple standard token ERC20 smart contracts that are part of Ethereum.
            // There might be scenarios you want to capture all the Events for different contracts using a specific filter,
            // for example all the transfers to an address.

            // In Nethereum creating an Event (handler) without a contract address allows to create filters
            // which are not attached to a specific contract.


            var transferEventHandlerAnyContract = web3Local.Eth.GetEvent<TransferEventDTO>();


            // There is already a contract deployed in the chain, from the previous sample,
            // so to demonstrate the access to events of multiple contracts we can deploy another standard token contract.


            var transactionReceiptNewContract =
                await deploymentHandler.SendRequestAndWaitForReceiptAsync(deploymentMessage);
            var contractAddress2 = transactionReceiptNewContract.ContractAddress;


            // and make another transfer using this new contract and the same receiver address.


            await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress2, transfer);


            // Creating a default filter input, and getting all changes, will retrieve all the transfer events
            // for all contracts.


            var filterAllTransferEventsForAllContracts = transferEventHandlerAnyContract.CreateFilterInput();
            var allTransferEventsForContract3 =
                await transferEventHandlerAnyContract.GetAllChangesAsync(filterAllTransferEventsForAllContracts);

            Console.WriteLine("Transfer events for All Contracts");

            foreach (var transferEvent in allTransferEventsForContract3)
            {
                Console.WriteLine("Transfer event TransactionHash : " + transferEvent.Log.TransactionHash);
                Console.WriteLine("From: " + transferEvent.Event.From);
                Console.WriteLine("To: " + transferEvent.Event.To);
                Console.WriteLine("Value: " + transferEvent.Event.Value);
            }


            // If we want to retrieve only all the transfers to the “receiverAddress”,
            // we can create the same filter as before ,including only the second indexed parameter (“to”). This will return the Transfers only to this address for both contracts.


            var filterTransferEventsForAllContractsReceiverAddress2 =
                transferEventHandlerAnyContract.CreateFilterInput(null, new[] { receiverAddress });
            var result4 =
                await transferEventHandlerAnyContract.GetAllChangesAsync(filterTransferEventsForAllContractsReceiverAddress2);


            for (var i = 0; i < 2; i++)
                Console.WriteLine("Transfer event number : " + i + " - TransactionHash : " +
                                  result4[i].Log.TransactionHash);


        }
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

    //*** FUNCTION MESSAGES **** ///

    // We can call the functions of smart contract to query the state of a smart contract or do any computation, 
    // which will not affect the state of the blockchain.

    // To do so,  we will need to create a class which inherits from "FunctionMessage". 
    // First we will decorate the class with a "Function" attribute, including the name and return type.
    // Each parameter of the function will be a property of the class, each of them decorated with the "Parameter" attribute, 
    // including the smart contract’s parameter name, type and parameter order.
    // For the ERC20 smart contract, the "balanceOf" function definition, 
    // provides the query interface to get the token balance of a given address. 
    // As we can see this function includes only one parameter "\_owner", of the type "address".


    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction : FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public string Owner { get; set; }
    }


    // Another type of smart contract function will be a transaction 
    // that will change the state of the smart contract (or smart contracts).
    // For example The "transfer" function definition for the ERC20 smart contract, 
    // includes the parameters “\_to”, which is an address parameter as a string, and the “\_value” 
    // or TokenAmount we want to transfer.


    // In a similar way to the "balanceOf" function, all the parameters include the solidity type, 
    // the contract’s parameter name and parameter order.


    // Note: When working with functions, it is very important to have the parameters types and function name correct 
    //as all of these make the signature of the function.

    [Function("transfer", "bool")]
    public class TransferFunction : FunctionMessage
    {
        [Parameter("address", "_to", 1)]
        public string To { get; set; }

        [Parameter("uint256", "_value", 2)]
        public BigInteger TokenAmount { get; set; }
    }

    // Finally, smart contracts also have events. Events defined in smart contracts write in the blockchain log, 
    // providing a way to retrieve further information when a smart contract interaction occurs.
    // To create an Event definition, we need to create a class that inherits from IEventDTO, decorated with the Event attribute.
    // The Transfer Event is similar to a Function: it  also includes parameters with name, order and type. 
    // But also a boolean value indicating if the parameter is indexed or not.
    // Indexed parameters will allow us later on to query the blockchain for those values.


    [Event("Transfer")]
    public class TransferEventDTO : IEventDTO
    {
        [Parameter("address", "_from", 1, true)]
        public string From { get; set; }

        [Parameter("address", "_to", 2, true)]
        public string To { get; set; }

        [Parameter("uint256", "_value", 3, false)]
        public BigInteger Value { get; set; }
    }

    // ### Multiple return types or complex objects
    // Functions of smart contracts can return one or multiple values in a single call. To decode the returned values, we use a FunctionOutputDTO.
    // Function outputs are classes which are decorated with a FunctionOutput attribute and implement the interface IFunctionOutputDTO.
    // An example of this is the following implementation that can be used to return the single value of the Balance on the ERC20 smart contract.

    [FunctionOutput]
    public class BalanceOfOutputDTO : IFunctionOutputDTO
    {
        [Parameter("uint256", "balance", 1)]
        public BigInteger Balance { get; set; }
    }


    // If we were going to return multiple values we could have something like:

    [FunctionOutput]
    public class BalanceOfOutputMultipleDTO : IFunctionOutputDTO
    {
        [Parameter("uint256", "balance1", 1)]
        public BigInteger Balance1 { get; set; }

        [Parameter("uint256", "balance2", 2)]
        public BigInteger Balance2 { get; set; }

        [Parameter("uint256", "balance3", 3)]
        public BigInteger Balance3 { get; set; }
    }


   
}
