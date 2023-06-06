using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.EVM.BlockchainState;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.ContractStorage;
using Nethereum.ABI.Decoders;

namespace Nethereum.EVM.Contracts.ERC20
{
    public class ERC20Simulator
    {
        public class TransferSimulationResult 
        {
            public BigInteger BalanceSenderBefore { get; set; }
            public BigInteger BalanceSenderStorageAfter { get; set; }
            public BigInteger BalanceSenderAfter { get; set; }

            public BigInteger BalanceReceiverBefore { get; set;}
            public BigInteger BalanceReceiverStorageAfter { get; set; }
            public BigInteger BalanceReceiverAfter { get; set; }

            public List<FilterLog> TransferLogs { get; set;}

        }

        public async Task<TransferSimulationResult> SimulateTransferAndBalanceStateAsync(string contractAddress, string addressSender, string addressReceiver, BigInteger amount, IWeb3 web3, BigInteger chainId)
        {

            var transferSimulationResult = new TransferSimulationResult();
            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var erc20Service = web3.Eth.ERC20.GetContractService(contractAddress);
            var balanceSenderBefore = await erc20Service.BalanceOfQueryAsync(addressSender);
            var balanceReceiverBefore = await erc20Service.BalanceOfQueryAsync(addressReceiver);

            transferSimulationResult.BalanceSenderBefore = balanceSenderBefore;
            transferSimulationResult.BalanceReceiverBefore = balanceReceiverBefore;

            var slot = await CalculateMappingBalanceSlotAsync(contractAddress, addressSender, chainId, web3, 10000, blockNumber);
            var code = await web3.Eth.GetCode.SendRequestAsync(contractAddress); // runtime code;
          
            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
            var executionStateService = new ExecutionStateService(nodeDataService);

            var programResult = await SimulateTransferAsync(contractAddress, addressSender, addressReceiver, amount, code, chainId, executionStateService);
            transferSimulationResult.TransferLogs = programResult.Logs;

            var balanceSenderAfter = await SimulateGetBalanceAsync(contractAddress, addressSender, code, chainId, executionStateService);
            var balanceReceiverAfter = await SimulateGetBalanceAsync(contractAddress, addressReceiver, code, chainId, executionStateService);

            transferSimulationResult.BalanceSenderAfter = balanceSenderAfter;
            transferSimulationResult.BalanceReceiverAfter = balanceReceiverAfter;

            var balanceStorageSenderAfter = await executionStateService.GetFromStorageAsync(contractAddress, StorageUtil.CalculateMappingAddressStorageKeyAsBigInteger(addressSender, (ulong)slot));
            var balanceStorageReceiverAfter = await executionStateService.GetFromStorageAsync(contractAddress, StorageUtil.CalculateMappingAddressStorageKeyAsBigInteger(addressReceiver, (ulong)slot));

            transferSimulationResult.BalanceReceiverStorageAfter = new IntTypeDecoder().DecodeBigInteger(balanceStorageReceiverAfter);
            transferSimulationResult.BalanceSenderStorageAfter = new IntTypeDecoder().DecodeBigInteger(balanceStorageSenderAfter);

            return transferSimulationResult;

        }

        private static async Task<ProgramResult> SimulateTransferAsync(string contractAddress, string addressSender, string addressReceiver, BigInteger amount, string code, BigInteger chainId, ExecutionStateService executionStateService)
        {
            var transferFunction = new TransferFunction();
            transferFunction.FromAddress = addressSender;
            transferFunction.To = addressReceiver;
            transferFunction.Value = amount;


            var callInput = transferFunction.CreateCallInput(contractAddress);
            callInput.ChainId = new HexBigInteger(chainId);
            var programContext = new ProgramContext(callInput, executionStateService);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();
            await evmSimulator.ExecuteAsync(program);
            return program.ProgramResult;
           
        }

        public async Task<BigInteger> SimulateGetBalanceAsync(string contractAddress, string addressOwner, string code, BigInteger chainId, ExecutionStateService executionStateService)
        {
            var balanceOfFunction = new BalanceOfFunction();
            balanceOfFunction.Owner = addressOwner;
            var callInput = balanceOfFunction.CreateCallInput(contractAddress);
            callInput.From = addressOwner;
            callInput.ChainId = new HexBigInteger(chainId);

            var programContext = new ProgramContext(callInput, executionStateService);
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();
            await evmSimulator.ExecuteAsync(program);
            var resultEncoded = program.ProgramResult.Result;
            var result = new BalanceOfOutputDTO().DecodeOutput(resultEncoded.ToHex());
            return result.Balance;
        }


        public async Task<BigInteger> CalculateMappingBalanceSlotAsync(string contractAddress, string addressWithAmount, BigInteger chainId, IWeb3 web3, ulong numberOfSlotsToTry = 10000, HexBigInteger blockNumber = null)
        {
            //current block number
            if (blockNumber == null)
            {
                blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            }
            var code = await web3.Eth.GetCode.SendRequestAsync(contractAddress); // runtime code;

            //Creating a balance function to simulate in the evm using the state of mainnet
            var balanceOfFunction = new BalanceOfFunction();
            balanceOfFunction.Owner = addressWithAmount;
            var callInput = balanceOfFunction.CreateCallInput(contractAddress);
            callInput.From = addressWithAmount;
            callInput.ChainId = new HexBigInteger(chainId);

            //setting up the nodeDataService to communicate with mainnet to get storage, code, etc
            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
            //setting up our local execution state service (this stores our execution and loads data from the node if required)
            var executionStateService = new ExecutionStateService(nodeDataService);
            //context with the call input and the state
            var programContext = new ProgramContext(callInput, executionStateService);
            //program with the code
            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();

            //execute the program
            var traceResult = await evmSimulator.ExecuteAsync(program);
            var intTypeDecoder = new ABI.Decoders.IntTypeDecoder();
            //get the result from the program
            var resultEncoded = program.ProgramResult.Result;
            //decoding the result
            var result = new BalanceOfOutputDTO().DecodeOutput(resultEncoded.ToHex());

            var contractStorage = executionStateService.CreateOrGetAccountExecutionState(contractAddress).GetContractStorageAsHex();

            foreach (var storageItem in contractStorage)
            {
                var valueAsBigInteger = intTypeDecoder.DecodeBigInteger(storageItem.Value);
                //comparing with our balance 
                if (valueAsBigInteger == result.Balance)
                {
                    //lets find the slot by calculating the encoded
                    for (ulong i = 0; i < numberOfSlotsToTry; i++)
                    {
                        var storageKey = StorageUtil.CalculateMappingAddressStorageKeyAsBigInteger(addressWithAmount, i);
                        if (storageKey == BigInteger.Parse(storageItem.Key))
                        {
                            //found it
                            return i;
                        }

                    }
                }

            }
            throw new Exception("Slot not found");
        }

        
    }
}
