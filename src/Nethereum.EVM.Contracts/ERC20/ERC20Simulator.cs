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
        public IWeb3 Web3 { get; }
        public BigInteger ChainId { get; }
        public string ContractAddress { get; }
        private byte[] Code { get; set; }

        public ERC20Simulator(IWeb3 web3, BigInteger chainId, string contractAddress, byte[] code = null)
        {
            Web3 = web3;
            ChainId = chainId;
            ContractAddress = contractAddress;
            Code = code;
        }

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

        protected async Task<byte[]> GetCodeAsync()
        {
            if (Code == null)
            {
                var code = await Web3.Eth.GetCode.SendRequestAsync(ContractAddress); // runtime code;
                Code = code.HexToByteArray();
            }
            return Code;
        }

        public async Task<TransferSimulationResult> SimulateTransferAndBalanceStateAsync(string addressSender, string addressReceiver, BigInteger amount)
        {

            var transferSimulationResult = new TransferSimulationResult();
            var blockNumber = await Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var erc20Service = Web3.Eth.ERC20.GetContractService(ContractAddress);
            var balanceSenderBefore = await erc20Service.BalanceOfQueryAsync(addressSender);
            var balanceReceiverBefore = await erc20Service.BalanceOfQueryAsync(addressReceiver);

            transferSimulationResult.BalanceSenderBefore = balanceSenderBefore;
            transferSimulationResult.BalanceReceiverBefore = balanceReceiverBefore;

            var slot = await CalculateMappingBalanceSlotAsync(addressSender, 10000, blockNumber);
          
          
            var nodeDataService = new RpcNodeDataService(Web3.Eth, new BlockParameter(blockNumber));
            var executionStateService = new ExecutionStateService(nodeDataService);

            var programResult = await SimulateTransferAsync(addressSender, addressReceiver, amount, executionStateService);
            transferSimulationResult.TransferLogs = programResult.Logs;

            var balanceSenderAfter = await SimulateGetBalanceAsync(addressSender, executionStateService);
            var balanceReceiverAfter = await SimulateGetBalanceAsync(addressReceiver, executionStateService);

            transferSimulationResult.BalanceSenderAfter = balanceSenderAfter;
            transferSimulationResult.BalanceReceiverAfter = balanceReceiverAfter;

            var balanceStorageSenderAfter = await executionStateService.GetFromStorageAsync(ContractAddress, StorageUtil.CalculateMappingAddressStorageKeyAsBigInteger(addressSender, (ulong)slot));
            var balanceStorageReceiverAfter = await executionStateService.GetFromStorageAsync(ContractAddress, StorageUtil.CalculateMappingAddressStorageKeyAsBigInteger(addressReceiver, (ulong)slot));

            transferSimulationResult.BalanceReceiverStorageAfter = new IntTypeDecoder().DecodeBigInteger(balanceStorageReceiverAfter);
            transferSimulationResult.BalanceSenderStorageAfter = new IntTypeDecoder().DecodeBigInteger(balanceStorageSenderAfter);

            return transferSimulationResult;

        }

        public async Task<ProgramResult> SimulateTransferAsync(string addressSender, string addressReceiver, BigInteger amount, ExecutionStateService executionStateService)
        {
            var transferFunction = new TransferFunction();
            transferFunction.FromAddress = addressSender;
            transferFunction.To = addressReceiver;
            transferFunction.Value = amount;


            var callInput = transferFunction.CreateCallInput(ContractAddress);
            callInput.ChainId = new HexBigInteger(ChainId);
            var programContext = new ProgramContext(callInput, executionStateService);
            var code = await GetCodeAsync();
            var program = new Program(code, programContext);
            var evmSimulator = new EVMSimulator();
            await evmSimulator.ExecuteAsync(program);
            return program.ProgramResult;
           
        }

        public async Task<BigInteger> SimulateGetBalanceAsync(string addressOwner, ExecutionStateService executionStateService)
        {
            var balanceOfFunction = new BalanceOfFunction();
            balanceOfFunction.Owner = addressOwner;
            var callInput = balanceOfFunction.CreateCallInput(ContractAddress);
            callInput.From = addressOwner;
            callInput.ChainId = new HexBigInteger(ChainId);

            var programContext = new ProgramContext(callInput, executionStateService);
            var code = await GetCodeAsync();
            var program = new Program(code, programContext);
            var evmSimulator = new EVMSimulator();
            await evmSimulator.ExecuteAsync(program);
            var resultEncoded = program.ProgramResult.Result;
            var result = new BalanceOfOutputDTO().DecodeOutput(resultEncoded.ToHex());
            return result.Balance;
        }


        public async Task<BigInteger> CalculateMappingBalanceSlotAsync(string addressWithAmount, ulong numberOfSlotsToTry = 10000, HexBigInteger blockNumber = null)
        {
            //current block number
            if (blockNumber == null)
            {
                blockNumber = await Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            }

            //Creating a balance function to simulate in the evm using the state of mainnet
            var balanceOfFunction = new BalanceOfFunction();
            balanceOfFunction.Owner = addressWithAmount;
            var callInput = balanceOfFunction.CreateCallInput(ContractAddress);
            callInput.From = addressWithAmount;
            callInput.ChainId = new HexBigInteger(ChainId);

            //setting up the nodeDataService to communicate with mainnet to get storage, code, etc
            var nodeDataService = new RpcNodeDataService(Web3.Eth, new BlockParameter(blockNumber));
            //setting up our local execution state service (this stores our execution and loads data from the node if required)
            var executionStateService = new ExecutionStateService(nodeDataService);
            //context with the call input and the state
            var programContext = new ProgramContext(callInput, executionStateService);
            //program with the code
            var code = await GetCodeAsync();
            var program = new Program(code, programContext);
            var evmSimulator = new EVMSimulator();

            //execute the program
            var traceResult = await evmSimulator.ExecuteAsync(program);
            var intTypeDecoder = new ABI.Decoders.IntTypeDecoder();
            //get the result from the program
            var resultEncoded = program.ProgramResult.Result;
            //decoding the result
            var result = new BalanceOfOutputDTO().DecodeOutput(resultEncoded.ToHex());

            var contractStorage = executionStateService.CreateOrGetAccountExecutionState(ContractAddress).GetContractStorageAsHex();

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
