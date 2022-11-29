using System;
using System.Numerics;
using Nethereum.ABI;
using System.Threading.Tasks;
using System.Collections.Generic;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Linq;
using Nethereum.Util;
using Nethereum.Hex.HexTypes;

namespace Nethereum.EVM.Execution
{
    public class EvmCallingCreationExecution
    {
        private readonly EvmProgramExecution evmProgramExecutionParent;

        public EvmCallingCreationExecution(EvmProgramExecution evmProgramExecutionParent) 
        {
            this.evmProgramExecutionParent = evmProgramExecutionParent;
        }
        public async Task<List<ProgramTrace>> CreateAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled)
        {
            var value = program.StackPopAndConvertToBigInteger();
            var memoryIndex = (int)program.StackPopAndConvertToBigInteger();
            var memoryLength = (int)program.StackPopAndConvertToBigInteger();
            var contractAddress = program.ProgramContext.AddressContract;
            var nonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(contractAddress);
            var newContractAddress = ContractUtils.CalculateContractAddress(contractAddress, nonce);
            var byteCode = program.Memory.GetRange(memoryIndex, memoryLength).ToArray();

            return await CreateContractAsync(program, vmExecutionStep, depth, traceEnabled, value, contractAddress, nonce, newContractAddress, byteCode);
        }

        public async Task<List<ProgramTrace>> Create2Async(Program program, int vmExecutionStep, int depth, bool traceEnabled)
        {
            var value = program.StackPopAndConvertToBigInteger();
            var memoryIndex = (int)program.StackPopAndConvertToBigInteger();
            var memoryLength = (int)program.StackPopAndConvertToBigInteger();
            var salt = program.StackPop();
            var contractAddress = program.ProgramContext.AddressContract;
            var nonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(contractAddress);

            var byteCode = program.Memory.GetRange(memoryIndex, memoryLength).ToArray();
            var newContractAddress = ContractUtils.CalculateCreate2Address(contractAddress, salt.ToHex(), byteCode.ToHex());
            return await CreateContractAsync(program, vmExecutionStep, depth, traceEnabled, value, contractAddress, nonce, newContractAddress, byteCode);
        }

        private async Task<List<ProgramTrace>> CreateContractAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled, BigInteger value, string contractAddress, BigInteger nonce, string newContractAddress, byte[] byteCode)
        {
            program.ProgramContext.ExecutionStateService.SetNonce(contractAddress, nonce + 1);


            var callInput = new CallInput()
            {
                From = contractAddress,
                Value = new HexBigInteger(value),
                To = newContractAddress,
                ChainId = new HexBigInteger(program.ProgramContext.ChainId)
            };

            program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -value);


            var programContext = new ProgramContext(callInput, program.ProgramContext.ExecutionStateService, program.ProgramContext.AddressCaller, null,
                (long)program.ProgramContext.BlockNumber, (long)program.ProgramContext.Timestamp, program.ProgramContext.Coinbase, (long)program.ProgramContext.BaseFee);
            programContext.Difficulty = program.ProgramContext.Difficulty;
            programContext.GasLimit = program.ProgramContext.GasLimit;
            var callProgram = new Program(byteCode, programContext);

            var vm = new EVMSimulator(evmProgramExecutionParent);
            try
            {
                callProgram = await vm.ExecuteAsync(callProgram, vmExecutionStep, depth + 1, traceEnabled);
                if (callProgram.ProgramResult.IsRevert == false)
                {
                    program.StackPush(new AddressType().Encode(newContractAddress));
                    var code = callProgram.ProgramResult.Result;
                    program.ProgramResult.Result = callProgram.ProgramResult.Result;
                    program.ProgramContext.ExecutionStateService.SaveCode(newContractAddress, code);
                    program.ProgramResult.Logs.AddRange(callProgram.ProgramResult.Logs);
                    program.ProgramResult.InnerCalls.Add(callInput);
                    program.ProgramResult.InnerCalls.AddRange(callProgram.ProgramResult.InnerCalls);
                    program.ProgramResult.CreatedContractAccounts.Add(newContractAddress);
                    program.ProgramContext.ExecutionStateService.SetNonce(newContractAddress, 1);
                    program.Step();
                }
                else
                {
                    program.ProgramResult.IsRevert = true;
                    program.Stop();
                }
            }
            catch (Exception ex)
            {
                program.Stop();
                program.ProgramResult.Exception = ex;
            }


            return callProgram.Trace;
        }

        public async Task SelfDestructAsync(Program program)
        {
            var addressReceiverFunds = program.StackPop();
            var addressReceiverFundsHex = addressReceiverFunds.ConvertToEthereumChecksumAddress();

            var balanceContract = await evmProgramExecutionParent.BlockchainCurrentContractContext.GetTotalBalanceAsync(program, program.ProgramContext.AddressContractEncoded);
            if (addressReceiverFundsHex.IsTheSameAddress(program.ProgramContext.AddressContract))
            {
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -balanceContract);
            }
            else
            {
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -balanceContract);
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(addressReceiverFundsHex, balanceContract);
            }

            program.ProgramResult.DeletedContractAccounts.Add(program.ProgramContext.AddressContract);
            program.Stop();
        }


        public async Task<List<ProgramTrace>> StaticCallAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToBigInteger();
            var codeAddress = program.StackPop();
            var value = 0;
            var from = program.ProgramContext.AddressContract;
            var to = codeAddress.ConvertToEthereumChecksumAddress();
            return await GenericCallAsync(program, vmExecutionStep, depth, traceEnabled, gas, codeAddress, value, from, to, true);
        }

        public async Task<List<ProgramTrace>> DelegateCallAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToBigInteger();
            var codeAddress = program.StackPop();
            var value = program.ProgramContext.Value; // value is the same
            var from = program.ProgramContext.AddressCaller; //sender is the original caller
            var to = program.ProgramContext.AddressContract; // keeping the same storage
            return await GenericCallAsync(program, vmExecutionStep, depth, traceEnabled, gas, codeAddress, value, from, to);
        }

        public async Task<List<ProgramTrace>> CallCodeAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToBigInteger();
            var codeAddress = program.StackPop();
            var value = program.StackPopAndConvertToBigInteger();
            var from = program.ProgramContext.AddressContract; //sender is the current contract
            var to = program.ProgramContext.AddressContract; // keeping the same storage
            return await GenericCallAsync(program, vmExecutionStep, depth, traceEnabled, gas, codeAddress, value, from, to);
        }

        public async Task<List<ProgramTrace>> CallAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToBigInteger();
            var codeAddress = program.StackPop();
            var value = program.StackPopAndConvertToBigInteger();
            var from = program.ProgramContext.AddressContract;
            var to = codeAddress.ConvertToEthereumChecksumAddress();

            return await GenericCallAsync(program, vmExecutionStep, depth, traceEnabled, gas, codeAddress, value, from, to);

        }



        private async Task<List<ProgramTrace>> GenericCallAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled, BigInteger gas, byte[] codeAddress, BigInteger value, string from, string to,  bool staticCall = false)
        {
            var dataInputIndex = (int)program.StackPopAndConvertToBigInteger();
            var dataInputLength = (int)program.StackPopAndConvertToBigInteger();

            var resultMemoryDataIndex = (int)program.StackPopAndConvertToBigInteger();
            var resultMemoryDataLength = (int)program.StackPopAndConvertToBigInteger();

            var dataInput = new byte[] { };

            if (dataInputLength != 0)
            {
                dataInput = new byte[dataInputLength];
                if (dataInputIndex + dataInputLength > program.Memory.Count)
                {
                    var dataToCopy = program.Memory.Skip(dataInputIndex).ToArray();
                    Array.Copy(dataToCopy, dataInput, dataToCopy.Length);
                }
                else
                {
                    dataInput = program.Memory.GetRange(dataInputIndex, dataInputLength).ToArray();
                }
            }


            var callInput = new CallInput()
            {
                From = from,
                Value = new HexBigInteger(value),
                To = to,
                Data = dataInput.ToHex(),
                Gas = new HexBigInteger(gas),
                ChainId = new HexBigInteger(program.ProgramContext.ChainId)
            };

            program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -value);
            var codeAddressAsChecksum = codeAddress.ConvertToEthereumChecksumAddress();
            var byteCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(codeAddressAsChecksum);
            if (byteCode.Length == 0) // calling / transfering a non contract account
            {
                try
                {
                    program.ProgramContext.ExecutionStateService.UpsertInternalBalance(to, value);


                    if (dataInput != null)
                    {
                        if (evmProgramExecutionParent.PreCompiledContracts.IsPrecompiledAdress(to))
                        {
                            var precompiledResult = evmProgramExecutionParent.PreCompiledContracts.ExecutePreCompile(to, dataInput);
                            program.WriteToMemory(resultMemoryDataIndex, precompiledResult);
                        }
                        
                        program.StackPush(1);
                        program.Step();
                    }
                    return new List<ProgramTrace>();
                }
                catch (Exception ex)
                {
                    program.Stop();
                    program.ProgramResult.Exception = ex;
                    return new List<ProgramTrace>();

                }
            }
            else
            {


                var programContext = new ProgramContext(callInput, program.ProgramContext.ExecutionStateService, program.ProgramContext.AddressCaller, codeAddress: codeAddressAsChecksum,
                        blockNumber: (long)program.ProgramContext.BlockNumber, timestamp: (long)program.ProgramContext.Timestamp,
                        coinbase: program.ProgramContext.Coinbase,
                        baseFee: (long)program.ProgramContext.BaseFee);
                programContext.Difficulty = program.ProgramContext.Difficulty;
                programContext.GasLimit = program.ProgramContext.GasLimit;
                var callProgram = new Program(byteCode, programContext);
                var vm = new EVMSimulator(evmProgramExecutionParent);
                
                try
                {
                    program.ProgramResult.InsertInnerContractCodeIfDoesNotExist(codeAddressAsChecksum, callProgram.Instructions);
                    callProgram = await vm.ExecuteAsync(callProgram, vmExecutionStep + 1, depth + 1, traceEnabled);
                    if (callProgram.ProgramResult.IsRevert == false)
                    {
                        program.StackPush(1);
                        var result = callProgram.ProgramResult.Result;
                        program.ProgramResult.Result = callProgram.ProgramResult.Result;
                        

                        if (program.ProgramResult.Result != null)
                        {
                            if (resultMemoryDataLength > program.ProgramResult.Result.Length)
                            {
                                resultMemoryDataLength = program.ProgramResult.Result.Length;
                            }
                        }

                        program.WriteToMemory(resultMemoryDataIndex, resultMemoryDataLength, result);

                        program.ProgramResult.Logs.AddRange(callProgram.ProgramResult.Logs);
                        program.ProgramResult.InnerCalls.Add(callInput);
                        program.ProgramResult.InnerCalls.AddRange(callProgram.ProgramResult.InnerCalls);

                        foreach(var codeItem in callProgram.ProgramResult.InnerContractCodeCalls)
                        {
                            program.ProgramResult.InsertInnerContractCodeIfDoesNotExist(codeItem.Key, codeItem.Value);
                        }
                        program.Step();
                    }
                    else
                    {
                        program.ProgramResult.IsRevert = true;
                        program.Stop();
                    }


                }
                catch (Exception ex)
                {
                    program.Stop();
                    program.ProgramResult.Exception = ex;
                    return callProgram.Trace;
                }
                return callProgram.Trace;
            }


        }
    }
}