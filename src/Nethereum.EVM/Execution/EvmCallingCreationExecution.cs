using System;
using System.Numerics;
using Nethereum.ABI;
using System.Threading.Tasks;
using System.Collections.Generic;
using Nethereum.EVM.Exceptions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Linq;
using Nethereum.Util;
using Nethereum.Hex.HexTypes;
using Nethereum.EVM.Gas;

namespace Nethereum.EVM.Execution
{
    public class EvmCallingCreationExecution
    {
        private readonly EvmProgramExecution evmProgramExecutionParent;

        public EvmCallingCreationExecution(EvmProgramExecution evmProgramExecutionParent) 
        {
            this.evmProgramExecutionParent = evmProgramExecutionParent;
        }
#if ENABLE_OLD_RECURSIVE_ARCHITECTURE
        // OLD RECURSIVE ARCHITECTURE - DISABLED TO PREVENT STACK OVERFLOW
        // These methods are called by EVMSimulator.StepAsync which creates recursive async calls
        // Use EVMSimulator.ExecuteWithCallStackAsync instead which uses an explicit Stack<CallFrame>

        public async Task<List<ProgramTrace>> CreateAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled)
        {
            if (program.ProgramContext.IsStatic)
                throw new StaticCallViolationException("CREATE");

            var value = program.StackPopAndConvertToUBigInteger();
            var memoryIndexBig = program.StackPopAndConvertToUBigInteger();
            var memoryLengthBig = program.StackPopAndConvertToUBigInteger();

            if (memoryIndexBig > int.MaxValue || memoryLengthBig > int.MaxValue)
            {
                program.StackPush(0);
                program.Step();
                return new List<ProgramTrace>();
            }

            var memoryIndex = (int)memoryIndexBig;
            var memoryLength = (int)memoryLengthBig;

            if (memoryLength > GasConstants.MAX_INITCODE_SIZE)
            {
                program.StackPush(0);
                program.Step();
                return new List<ProgramTrace>();
            }

            var contractAddress = program.ProgramContext.AddressContract;
            var nonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(contractAddress);
            var newContractAddress = ContractUtils.CalculateContractAddress(contractAddress, nonce);

            byte[] byteCode;
            if (memoryIndex + memoryLength > program.Memory.Count)
            {
                byteCode = new byte[memoryLength];
                var available = Math.Max(0, program.Memory.Count - memoryIndex);
                if (available > 0)
                {
                    var src = program.Memory.GetRange(memoryIndex, available).ToArray();
                    Array.Copy(src, byteCode, available);
                }
            }
            else
            {
                byteCode = program.Memory.GetRange(memoryIndex, memoryLength).ToArray();
            }

            return await CreateContractAsync(program, vmExecutionStep, depth, traceEnabled, value, contractAddress, nonce, newContractAddress, byteCode);
        }

        public async Task<List<ProgramTrace>> Create2Async(Program program, int vmExecutionStep, int depth, bool traceEnabled)
        {
            if (program.ProgramContext.IsStatic)
                throw new StaticCallViolationException("CREATE2");

            var value = program.StackPopAndConvertToUBigInteger();
            var memoryIndexBig = program.StackPopAndConvertToUBigInteger();
            var memoryLengthBig = program.StackPopAndConvertToUBigInteger();
            var salt = program.StackPop();

            if (memoryIndexBig > int.MaxValue || memoryLengthBig > int.MaxValue)
            {
                program.StackPush(0);
                program.Step();
                return new List<ProgramTrace>();
            }

            var memoryIndex = (int)memoryIndexBig;
            var memoryLength = (int)memoryLengthBig;

            if (memoryLength > GasConstants.MAX_INITCODE_SIZE)
            {
                program.StackPush(0);
                program.Step();
                return new List<ProgramTrace>();
            }
            var contractAddress = program.ProgramContext.AddressContract;
            var nonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(contractAddress);

            byte[] byteCode;
            if (memoryIndex + memoryLength > program.Memory.Count)
            {
                // Extend bytecode array with zeros if memory is insufficient
                byteCode = new byte[memoryLength];
                var available = Math.Max(0, program.Memory.Count - memoryIndex);
                if (available > 0)
                {
                    var src = program.Memory.GetRange(memoryIndex, available).ToArray();
                    Array.Copy(src, byteCode, available);
                }
            }
            else
            {
                byteCode = program.Memory.GetRange(memoryIndex, memoryLength).ToArray();
            }
            var newContractAddress = ContractUtils.CalculateCreate2Address(contractAddress, salt.ToHex(), byteCode.ToHex());
            return await CreateContractAsync(program, vmExecutionStep, depth, traceEnabled, value, contractAddress, nonce, newContractAddress, byteCode);
        }

        private async Task<List<ProgramTrace>> CreateContractAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled, BigInteger value, string contractAddress, BigInteger nonce, string newContractAddress, byte[] byteCode)
        {
            if (depth + 1 > GasConstants.MAX_CALL_DEPTH)
            {
                program.StackPush(0);
                program.Step();
                return new List<ProgramTrace>();
            }

            // Always increment sender's nonce first (even if CREATE fails)
            program.ProgramContext.ExecutionStateService.SetNonce(contractAddress, nonce + 1);

            // EIP-2929: Mark new contract address as warm
            program.ProgramContext.ExecutionStateService.MarkAddressAsWarm(newContractAddress);

            // EIP-684: Check for address collision - fail if target has code OR nonce > 0
            var targetAccount = await program.ProgramContext.ExecutionStateService.LoadBalanceNonceAndCodeFromStorageAsync(newContractAddress);
            var targetHasCode = targetAccount.Code != null && targetAccount.Code.Length > 0;
            var targetHasNonce = targetAccount.Nonce.HasValue && targetAccount.Nonce.Value > 0;
            if (targetHasCode || targetHasNonce)
            {
                program.StackPush(0);
                program.Step();
                return new List<ProgramTrace>();
            }

            var callInput = new CallInput()
            {
                From = contractAddress,
                Value = new HexBigInteger(value),
                To = newContractAddress,
                ChainId = new HexBigInteger(program.ProgramContext.ChainId)
            };

            program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -value);
            // Credit value to new contract before initcode execution (so SELFBALANCE works)
            program.ProgramContext.ExecutionStateService.UpsertInternalBalance(newContractAddress, value);

            var snapshotId = program.ProgramContext.ExecutionStateService.TakeSnapshot();

            var programContext = new ProgramContext(callInput, program.ProgramContext.ExecutionStateService, program.ProgramContext.AddressOrigin, null,
                (long)program.ProgramContext.BlockNumber, (long)program.ProgramContext.Timestamp, program.ProgramContext.Coinbase, (long)program.ProgramContext.BaseFee);
            programContext.Difficulty = program.ProgramContext.Difficulty;
            programContext.GasLimit = program.ProgramContext.GasLimit;
            programContext.GasPrice = program.ProgramContext.GasPrice;
            programContext.Depth = depth + 1;
            programContext.EnforceGasSentry = program.ProgramContext.EnforceGasSentry;
            programContext.SetAccessListTracker(program.ProgramContext.AccessListTracker);
            var callProgram = new Program(byteCode, programContext);

            // EIP-150: CREATE/CREATE2 get 63/64 of remaining gas - Yellow Paper L(n) = n - floor(n/64)
            var gasToAllocate = program.GasRemaining - (program.GasRemaining / 64);
            if (gasToAllocate < 0) gasToAllocate = 0;
            callProgram.GasRemaining = gasToAllocate;
            program.GasRemaining -= gasToAllocate;

            var vm = new EVMSimulator(evmProgramExecutionParent);
            try
            {
                callProgram = await vm.ExecuteAsync(callProgram, vmExecutionStep, depth + 1, traceEnabled);
                if (callProgram.ProgramResult.IsRevert == false)
                {
                    var code = callProgram.ProgramResult.Result;
                    if (code != null && code.Length > GasConstants.MAX_CODE_SIZE)
                    {
                        program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                        program.StackPush(0);
                        var gasActuallySpent = gasToAllocate - callProgram.GasRemaining;
                        if (gasActuallySpent < 0) gasActuallySpent = 0;
                        program.GasRemaining += callProgram.GasRemaining;
                        program.TotalGasUsed += gasActuallySpent;
                        program.Step();
                        return callProgram.Trace;
                    }

                    var codeSize = code?.Length ?? 0;
                    var codeDepositCost = codeSize * GasConstants.CREATE_DATA_GAS;
                    if (callProgram.GasRemaining < codeDepositCost)
                    {
                        program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                        program.StackPush(0);
                        program.TotalGasUsed += gasToAllocate;
                        program.Step();
                        return callProgram.Trace;
                    }

                    program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);

                    callProgram.GasRemaining -= codeDepositCost;

                    program.StackPush(new AddressType().Encode(newContractAddress));
                    program.ProgramContext.ExecutionStateService.SaveCode(newContractAddress, code);
                    program.ProgramResult.Logs.AddRange(callProgram.ProgramResult.Logs);
                    program.ProgramResult.InnerCalls.Add(callInput);
                    program.ProgramResult.InnerCalls.AddRange(callProgram.ProgramResult.InnerCalls);
                    program.ProgramResult.CreatedContractAccounts.Add(newContractAddress);
                    program.ProgramResult.CreatedContractAccounts.AddRange(callProgram.ProgramResult.CreatedContractAccounts);
                    program.ProgramResult.DeletedContractAccounts.AddRange(callProgram.ProgramResult.DeletedContractAccounts);
                    program.ProgramResult.LastCallReturnData = null;
                    program.ProgramContext.ExecutionStateService.SetNonce(newContractAddress, 1);
                    var gasActuallySpentOnCreate = gasToAllocate - callProgram.GasRemaining;
                    if (gasActuallySpentOnCreate < 0) gasActuallySpentOnCreate = 0;
                    program.GasRemaining += callProgram.GasRemaining;
                    program.TotalGasUsed += gasActuallySpentOnCreate;
                    program.Step();
                }
                else
                {
                    program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                    program.StackPush(0);
                    program.ProgramResult.LastCallReturnData = callProgram.ProgramResult.Result;
                    var gasActuallySpent = gasToAllocate - callProgram.GasRemaining;
                    if (gasActuallySpent < 0) gasActuallySpent = 0;
                    program.GasRemaining += callProgram.GasRemaining;
                    program.TotalGasUsed += gasActuallySpent;
                    program.Step();
                }
            }
            catch (Exception ex)
            {
                program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                program.StackPush(0);
                program.ProgramResult.LastCallReturnData = null;
                var gasActuallySpent = gasToAllocate - callProgram.GasRemaining;
                if (gasActuallySpent < 0) gasActuallySpent = 0;
                program.GasRemaining += callProgram.GasRemaining;
                program.TotalGasUsed += gasActuallySpent;
                program.Step();
            }


            return callProgram.Trace;
        }
#endif

        public async Task SelfDestructAsync(Program program)
        {
            if (program.ProgramContext.IsStatic)
                throw new StaticCallViolationException("SELFDESTRUCT");

            var addressReceiverFunds = program.StackPop();
            var addressReceiverFundsHex = addressReceiverFunds.ConvertToEthereumChecksumAddress();

            var balanceContract = await evmProgramExecutionParent.BlockchainCurrentContractContext.GetTotalBalanceAsync(program, program.ProgramContext.AddressContractEncoded);

            if (!addressReceiverFundsHex.IsTheSameAddress(program.ProgramContext.AddressContract))
            {
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -balanceContract);
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(addressReceiverFundsHex, balanceContract);
            }

            program.ProgramResult.DeletedContractAccounts.Add(program.ProgramContext.AddressContract);
            program.Stop();
        }


#if ENABLE_OLD_RECURSIVE_ARCHITECTURE
        public async Task<List<ProgramTrace>> StaticCallAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToUBigInteger();
            var codeAddress = program.StackPop();
            var value = 0;
            var from = program.ProgramContext.AddressContract;
            var to = codeAddress.ConvertToEthereumChecksumAddress();

            return await GenericCallAsync(program, vmExecutionStep, depth, traceEnabled, gas, codeAddress, value, from, to, true);
        }

        public async Task<List<ProgramTrace>> DelegateCallAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToUBigInteger();
            var codeAddress = program.StackPop();
            var value = program.ProgramContext.Value; // value is the same
            var from = program.ProgramContext.AddressCaller; //sender is the original caller
            var to = program.ProgramContext.AddressContract; // keeping the same storage

            return await GenericCallAsync(program, vmExecutionStep, depth, traceEnabled, gas, codeAddress, value, from, to);
        }

        public async Task<List<ProgramTrace>> CallCodeAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToUBigInteger();
            var codeAddress = program.StackPop();
            var value = program.StackPopAndConvertToUBigInteger();
            var from = program.ProgramContext.AddressContract; //sender is the current contract
            var to = program.ProgramContext.AddressContract; // keeping the same storage
            return await GenericCallAsync(program, vmExecutionStep, depth, traceEnabled, gas, codeAddress, value, from, to);
        }

        public async Task<List<ProgramTrace>> CallAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled = true)
        {
            var gas = program.StackPopAndConvertToUBigInteger();
            var codeAddress = program.StackPop();
            var value = program.StackPopAndConvertToUBigInteger();
            var from = program.ProgramContext.AddressContract;
            var to = codeAddress.ConvertToEthereumChecksumAddress();

            return await GenericCallAsync(program, vmExecutionStep, depth, traceEnabled, gas, codeAddress, value, from, to);

        }

        private async Task<List<ProgramTrace>> GenericCallAsync(Program program, int vmExecutionStep, int depth, bool traceEnabled, BigInteger gas, byte[] codeAddress, BigInteger value, string from, string to,  bool staticCall = false)
        {
            var dataInputIndexBig = program.StackPopAndConvertToUBigInteger();
            var dataInputLengthBig = program.StackPopAndConvertToUBigInteger();
            var resultMemoryDataIndexBig = program.StackPopAndConvertToUBigInteger();
            var resultMemoryDataLengthBig = program.StackPopAndConvertToUBigInteger();

            if (dataInputIndexBig > int.MaxValue || dataInputLengthBig > int.MaxValue ||
                resultMemoryDataIndexBig > int.MaxValue || resultMemoryDataLengthBig > int.MaxValue)
            {
                program.StackPush(0);
                program.Step();
                return new List<ProgramTrace>();
            }

            var dataInputIndex = (int)dataInputIndexBig;
            var dataInputLength = (int)dataInputLengthBig;
            var resultMemoryDataIndex = (int)resultMemoryDataIndexBig;
            var resultMemoryDataLength = (int)resultMemoryDataLengthBig;

            if (depth + 1 > GasConstants.MAX_CALL_DEPTH)
            {
                program.StackPush(0);
                program.Step();
                return new List<ProgramTrace>();
            }

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

            var codeAddressAsChecksum = codeAddress.ConvertToEthereumChecksumAddress();

            var snapshotId = program.ProgramContext.ExecutionStateService.TakeSnapshot();

            program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -value);
            program.ProgramContext.RecordAddressAccess(codeAddressAsChecksum);
            var byteCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(codeAddressAsChecksum);

            if (byteCode == null || byteCode.Length == 0) // calling / transfering a non contract account
            {
                try
                {
                    program.ProgramContext.ExecutionStateService.UpsertInternalBalance(to, value);


                    if (dataInput != null)
                    {
                        // Use codeAddressAsChecksum for precompile check, not 'to' (matters for CALLCODE/DELEGATECALL)
                        var isPrecompile = evmProgramExecutionParent.PreCompiledContracts.IsPrecompiledAdress(codeAddressAsChecksum);
                        if (isPrecompile)
                        {
                            var precompiledResult = evmProgramExecutionParent.PreCompiledContracts.ExecutePreCompile(codeAddressAsChecksum, dataInput);
                            program.WriteToMemory(resultMemoryDataIndex, precompiledResult);
                            program.ProgramResult.LastCallReturnData = precompiledResult;
                        }
                        else
                        {
                            program.ProgramResult.LastCallReturnData = null;

                            // For empty code with value > 0, return the stipend to the caller.
                            // When calling code with value, the subcall receives allocated_gas + stipend (free).
                            // If callee uses 0 gas, it returns all including stipend.
                            // For empty code, we simulate this: the stipend is "free" gas that shouldn't
                            // count towards gas used. Both GasRemaining and TotalGasUsed must be adjusted.
                            if (!staticCall && value > 0)
                            {
                                program.GasRemaining += Gas.GasConstants.CALL_STIPEND;
                                program.TotalGasUsed -= Gas.GasConstants.CALL_STIPEND;
                            }
                        }

                        program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                        program.StackPush(1);
                        program.Step();
                    }
                    else
                    {
                        program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                    }
                    return new List<ProgramTrace>();
                }
                catch (Exception ex)
                {
                    program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                    program.Stop();
                    program.ProgramResult.Exception = ex;
                    return new List<ProgramTrace>();

                }
            }
            else
            {


                var programContext = new ProgramContext(callInput, program.ProgramContext.ExecutionStateService, program.ProgramContext.AddressOrigin, codeAddress: codeAddressAsChecksum,
                        blockNumber: (long)program.ProgramContext.BlockNumber, timestamp: (long)program.ProgramContext.Timestamp,
                        coinbase: program.ProgramContext.Coinbase,
                        baseFee: (long)program.ProgramContext.BaseFee);
                programContext.Difficulty = program.ProgramContext.Difficulty;
                programContext.GasLimit = program.ProgramContext.GasLimit;
                programContext.GasPrice = program.ProgramContext.GasPrice;
                programContext.Depth = depth + 1;
                programContext.IsStatic = staticCall || program.ProgramContext.IsStatic;
                programContext.EnforceGasSentry = program.ProgramContext.EnforceGasSentry;
                programContext.SetAccessListTracker(program.ProgramContext.AccessListTracker);
                var callProgram = new Program(byteCode, programContext);

                // EIP-150: Cap gas at 63/64 of remaining gas - Yellow Paper L(n) = n - floor(n/64)
                var maxAllowedGas = program.GasRemaining - (program.GasRemaining / 64);
                var gasToAllocate = gas > maxAllowedGas ? maxAllowedGas : gas;
                if (gasToAllocate < 0) gasToAllocate = 0;

                // EIP-150: Add gas stipend for value transfers
                if (value > 0)
                {
                    callProgram.GasRemaining = gasToAllocate + GasConstants.CALL_STIPEND;
                }
                else
                {
                    callProgram.GasRemaining = gasToAllocate;
                }

                // Deduct allocated gas from parent (will be refunded when subcall returns)
                program.GasRemaining -= gasToAllocate;

                var vm = new EVMSimulator(evmProgramExecutionParent);

                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(to, value);

                try
                {
                    if (traceEnabled)
                    {
                        program.ProgramResult.InsertInnerContractCodeIfDoesNotExist(codeAddressAsChecksum, callProgram.Instructions);
                    }
                    callProgram = await vm.ExecuteAsync(callProgram, vmExecutionStep + 1, depth + 1, traceEnabled, skipInitialBalance: true);
                    if (callProgram.ProgramResult.IsRevert == false)
                    {
                        program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);

                        program.StackPush(1);
                        var result = callProgram.ProgramResult.Result;
                        program.ProgramResult.LastCallReturnData = result;

                        if (result != null)
                        {
                            if (resultMemoryDataLength > result.Length)
                            {
                                resultMemoryDataLength = result.Length;
                            }
                        }

                        program.WriteToMemory(resultMemoryDataIndex, resultMemoryDataLength, result);

                        program.ProgramResult.Logs.AddRange(callProgram.ProgramResult.Logs);
                        program.ProgramResult.InnerCalls.Add(callInput);
                        program.ProgramResult.InnerCalls.AddRange(callProgram.ProgramResult.InnerCalls);

                        if (traceEnabled)
                        {
                            foreach(var codeItem in callProgram.ProgramResult.InnerContractCodeCalls)
                            {
                                program.ProgramResult.InsertInnerContractCodeIfDoesNotExist(codeItem.Key, codeItem.Value);
                            }
                        }

                        var gasActuallySpent = gasToAllocate - callProgram.GasRemaining;
                        if (gasActuallySpent < 0) gasActuallySpent = 0;
                        program.GasRemaining += callProgram.GasRemaining;
                        program.TotalGasUsed += gasActuallySpent;
                        program.Step();
                    }
                    else
                    {
                        program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);

                        var gasActuallySpent = gasToAllocate - callProgram.GasRemaining;
                        if (gasActuallySpent < 0) gasActuallySpent = 0;
                        program.StackPush(0);
                        program.ProgramResult.LastCallReturnData = callProgram.ProgramResult.Result;
                        program.GasRemaining += callProgram.GasRemaining;
                        program.TotalGasUsed += gasActuallySpent;
                        program.Step();
                    }


                }
                catch (Exception ex)
                {
                    program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);

                    var gasActuallySpent = gasToAllocate - callProgram.GasRemaining;
                    if (gasActuallySpent < 0) gasActuallySpent = 0;
                    program.StackPush(0);
                    program.ProgramResult.LastCallReturnData = null;
                    program.GasRemaining += callProgram.GasRemaining;
                    program.TotalGasUsed += gasActuallySpent;
                    program.Step();
                }
                return callProgram.Trace;
            }


        }
#endif
    }
}