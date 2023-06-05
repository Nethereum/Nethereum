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

namespace Nethereum.EVM.Contracts.ERC20
{
    public class ERC20Simulator
    {
        public async Task<BigInteger> CalculateMappingBalanceSlot(string contractAddress, string addressWithAmount, BigInteger chainId, IWeb3 web3, ulong numberOfSlotsToTry = 10000)
        {
            //current block number
            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
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
