using Nethereum.ABI;
using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM
{
    public class EVMSimulator
    {
#if DEBUG
        public EVMSimulator(EvmProgramExecution evmProgramExecution, bool enableTraceToDebugOuptput = false) :this(evmProgramExecution)
        {
            EnableTraceToDebugOuptput = enableTraceToDebugOuptput;
        }
        public bool EnableTraceToDebugOuptput { get; }

#endif
        public EvmProgramExecution EvmProgramExecution { get; }
        public EVMSimulator(EvmProgramExecution evmProgramExecution = null)
       {
            EvmProgramExecution = evmProgramExecution ?? new EvmProgramExecution();
        }

        public async Task<Program> ExecuteWithCallStackAsync(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true)
        {
            if (depth > GasConstants.MAX_CALL_DEPTH)
            {
                program.Stop();
                return program;
            }

            var callStack = new Stack<CallFrame>();

            var initialFrame = new CallFrame
            {
                Program = program,
                VmExecutionCounter = vmExecutionCounter,
                ProgramExecutionCounter = 0,
                Depth = depth,
                TraceEnabled = traceEnabled,
                FrameType = CallFrameType.Initial
            };
            callStack.Push(initialFrame);

            program.ProgramContext?.InitialiaseContractBalanceFromCallInputValue();

            while (callStack.Count > 0)
            {
                var currentFrame = callStack.Peek();
                var currentProgram = currentFrame.Program;

                if (currentProgram.Stopped)
                {
                    var completedFrame = callStack.Pop();

                    if (callStack.Count > 0)
                    {
                        var parentFrame = callStack.Peek();
                        MergeCompletedFrameIntoParent(completedFrame, parentFrame);
                    }
                    continue;
                }

                var currentInstruction = currentProgram.GetCurrentInstruction();

                // Declare outside try so catch blocks can access for trace recording
                BigInteger gasCost = 0;
                BigInteger gasBeforeOp = currentProgram.GasRemaining;

                try
                {
                    gasCost = await OpcodeGasTable.GetGasCostAsync(currentInstruction.Instruction.Value, currentProgram);
                    gasBeforeOp = currentProgram.GasRemaining; // Capture gas BEFORE deduction for trace

                    // Record trace BEFORE gas deduction so we capture even failed ops (like geth)
                    ProgramTrace trace = null;
                    if (currentFrame.TraceEnabled)
                    {
                        trace = ProgramTrace.CreateTraceFromCurrentProgram(
                            currentProgram.ProgramContext.AddressContract,
                            currentFrame.VmExecutionCounter,
                            currentFrame.ProgramExecutionCounter,
                            currentFrame.Depth,
                            currentProgram,
                            currentInstruction,
                            currentProgram.ProgramContext.CodeAddress);
                        trace.GasCost = gasCost;
                        trace.GasRemaining = gasBeforeOp;
                    }

                    // Sentry check must happen BEFORE gas deduction (EIP-2200)
                    // The check is: if gas_left <= 2300 before SSTORE, fail
                    if (currentInstruction.Instruction.Value == Instruction.SSTORE &&
                        currentProgram.ProgramContext.EnforceGasSentry &&
                        gasBeforeOp <= Gas.GasConstants.SSTORE_SENTRY)
                    {
                        throw new Exceptions.SStoreSentryException(gasBeforeOp, Gas.GasConstants.SSTORE_SENTRY);
                    }

                    currentProgram.UpdateGasUsed(gasCost);

                    var subCallSetup = await StepWithCallStackAsync(currentFrame, currentInstruction, callStack);

                    // Geth includes gas forwarded in cost for CALL-type opcodes, but NOT for CREATE-type opcodes
                    // This applies both to regular subcalls AND precompile calls (which execute inline)
                    if (trace != null && subCallSetup != null && (subCallSetup.ShouldCreateSubCall || subCallSetup.IsPrecompileHandled) && subCallSetup.GasForwarded > 0)
                    {
                        var opcode = currentInstruction.Instruction.Value;
                        bool isCallType = opcode == Instruction.CALL || opcode == Instruction.CALLCODE ||
                                          opcode == Instruction.DELEGATECALL || opcode == Instruction.STATICCALL;
                        if (isCallType)
                        {
                            trace.GasCost = gasCost + subCallSetup.GasForwarded;
                        }
                    }

                    if (trace != null)
                    {
                        currentProgram.Trace.Add(trace);
#if DEBUG
                        if (EnableTraceToDebugOuptput)
                        {
                            Debug.WriteLine(trace.ToString());
                        }
#endif
                    }

                    currentFrame.ProgramExecutionCounter++;
                    currentFrame.VmExecutionCounter++;

                    if (subCallSetup != null && subCallSetup.ShouldCreateSubCall && subCallSetup.NewFrame != null)
                    {
                        callStack.Push(subCallSetup.NewFrame);
                    }
                }
                catch (OutOfGasException ex)
                {
                    // Record trace for the failed instruction (like geth does)
                    if (currentFrame.TraceEnabled)
                    {
                        var trace = ProgramTrace.CreateTraceFromCurrentProgram(
                            currentProgram.ProgramContext.AddressContract,
                            currentFrame.VmExecutionCounter,
                            currentFrame.ProgramExecutionCounter,
                            currentFrame.Depth,
                            currentProgram,
                            currentInstruction,
                            currentProgram.ProgramContext.CodeAddress);
                        trace.GasCost = ex.GasRequired;
                        trace.GasRemaining = ex.GasRemaining;
                        currentProgram.Trace.Add(trace);
                    }

                    currentProgram.GasRemaining = 0;
                    currentProgram.ProgramResult.IsRevert = true;
                    currentProgram.Stop();
                }
                catch (Exceptions.SStoreSentryException)
                {
                    // Record trace for the SSTORE that failed sentry check (like geth does)
                    // Use gasBeforeOp because geth reports gas BEFORE the operation's cost is deducted
                    if (currentFrame.TraceEnabled)
                    {
                        var trace = ProgramTrace.CreateTraceFromCurrentProgram(
                            currentProgram.ProgramContext.AddressContract,
                            currentFrame.VmExecutionCounter,
                            currentFrame.ProgramExecutionCounter,
                            currentFrame.Depth,
                            currentProgram,
                            currentInstruction,
                            currentProgram.ProgramContext.CodeAddress);
                        // When sentry fails, geth reports cost=0 and gas BEFORE deduction
                        trace.GasCost = 0;
                        trace.GasRemaining = gasBeforeOp;
                        currentProgram.Trace.Add(trace);
                    }
                    currentProgram.GasRemaining = 0;
                    currentProgram.ProgramResult.IsRevert = true;
                    currentProgram.Stop();
                }
                catch (Exception ex)
                {
                    // Handle runtime errors (stack underflow, invalid jump, etc.)
                    // These should cause the current frame to fail, not crash the entire execution
                    if (currentFrame.TraceEnabled)
                    {
                        var trace = ProgramTrace.CreateTraceFromCurrentProgram(
                            currentProgram.ProgramContext.AddressContract,
                            currentFrame.VmExecutionCounter,
                            currentFrame.ProgramExecutionCounter,
                            currentFrame.Depth,
                            currentProgram,
                            currentInstruction,
                            currentProgram.ProgramContext.CodeAddress);
                        trace.GasCost = gasCost;
                        trace.GasRemaining = gasBeforeOp;
                        currentProgram.Trace.Add(trace);
                    }

                    currentProgram.GasRemaining = 0;
                    currentProgram.ProgramResult.IsRevert = true;
                    currentProgram.Stop();
                }
            }

            if (traceEnabled && program.StoppedImplicitly)
            {
                var implicitStopInstruction = new ProgramInstruction
                {
                    Instruction = Instruction.STOP,
                    Value = 0,
                    Step = program.ByteCode.Length
                };
                var implicitStopTrace = ProgramTrace.CreateTraceFromCurrentProgram(
                    program.ProgramContext.AddressContract,
                    vmExecutionCounter,
                    0,
                    depth,
                    program,
                    implicitStopInstruction,
                    program.ProgramContext.CodeAddress);
                program.Trace.Add(implicitStopTrace);
            }

            return program;
        }

        private void MergeCompletedFrameIntoParent(CallFrame completedFrame, CallFrame parentFrame)
        {
            var childProgram = completedFrame.Program;
            var parentProgram = parentFrame.Program;

            if (completedFrame.FrameType == CallFrameType.Create || completedFrame.FrameType == CallFrameType.Create2)
            {
                MergeCreateResult(completedFrame, parentFrame);
            }
            else if (completedFrame.FrameType != CallFrameType.Initial)
            {
                MergeCallResult(completedFrame, parentFrame);
            }

            parentFrame.VmExecutionCounter += childProgram.Trace.Count;
            parentProgram.Trace.AddRange(childProgram.Trace);
        }

        private void MergeCreateResult(CallFrame completedFrame, CallFrame parentFrame)
        {
            var childProgram = completedFrame.Program;
            var parentProgram = parentFrame.Program;

            // Cap returned gas at what was allocated (consistent with MergeCallResult pattern)
            var gasToReturn = BigInteger.Min(childProgram.GasRemaining, completedFrame.GasAllocated);

            if (!childProgram.ProgramResult.IsRevert)
            {
                var code = childProgram.ProgramResult.Result;

                if (code != null && code.Length > GasConstants.MAX_CODE_SIZE)
                {
                    if (completedFrame.SnapshotId.HasValue)
                    {
                        parentProgram.ProgramContext.ExecutionStateService.RevertToSnapshot(completedFrame.SnapshotId.Value);
                    }
                    parentProgram.StackPush(0);
                    parentProgram.ProgramResult.LastCallReturnData = null;
                    parentProgram.TotalGasUsed += completedFrame.GasAllocated;
                    parentProgram.Step();
                    return;
                }

                if (code != null && code.Length > 0 && code[0] == 0xEF)
                {
                    if (completedFrame.SnapshotId.HasValue)
                    {
                        parentProgram.ProgramContext.ExecutionStateService.RevertToSnapshot(completedFrame.SnapshotId.Value);
                    }
                    parentProgram.StackPush(0);
                    parentProgram.ProgramResult.LastCallReturnData = null;
                    parentProgram.TotalGasUsed += completedFrame.GasAllocated;
                    parentProgram.Step();
                    return;
                }

                var codeSize = code?.Length ?? 0;
                var codeDepositCost = codeSize * GasConstants.CREATE_DATA_GAS;
                if (childProgram.GasRemaining < codeDepositCost)
                {
                    if (completedFrame.SnapshotId.HasValue)
                    {
                        parentProgram.ProgramContext.ExecutionStateService.RevertToSnapshot(completedFrame.SnapshotId.Value);
                        // Nonce was incremented before snapshot, so it's preserved after revert
                    }
                    parentProgram.StackPush(0);
                    parentProgram.ProgramResult.LastCallReturnData = null;
                    parentProgram.TotalGasUsed += completedFrame.GasAllocated;
                    parentProgram.Step();
                    return;
                }

                if (completedFrame.SnapshotId.HasValue)
                {
                    parentProgram.ProgramContext.ExecutionStateService.CommitSnapshot(completedFrame.SnapshotId.Value);
                }

                childProgram.GasRemaining -= codeDepositCost;

                // Recalculate after code deposit deduction
                var gasToReturnAfterDeposit = BigInteger.Min(childProgram.GasRemaining, completedFrame.GasAllocated);
                var gasActuallySpentOnCreate = completedFrame.GasAllocated - gasToReturnAfterDeposit;

                parentProgram.StackPush(new AddressType().Encode(completedFrame.NewContractAddress));
                parentProgram.ProgramContext.ExecutionStateService.SaveCode(completedFrame.NewContractAddress, code);
                parentProgram.ProgramResult.Logs.AddRange(childProgram.ProgramResult.Logs);
                parentProgram.ProgramResult.InnerCalls.Add(completedFrame.CallInput);
                parentProgram.ProgramResult.InnerCalls.AddRange(childProgram.ProgramResult.InnerCalls);
                parentProgram.ProgramResult.CreatedContractAccounts.Add(completedFrame.NewContractAddress);
                parentProgram.ProgramResult.CreatedContractAccounts.AddRange(childProgram.ProgramResult.CreatedContractAccounts);
                parentProgram.ProgramResult.DeletedContractAccounts.AddRange(childProgram.ProgramResult.DeletedContractAccounts);
                parentProgram.ProgramResult.LastCallReturnData = null;
                parentProgram.GasRemaining += gasToReturnAfterDeposit;
                parentProgram.TotalGasUsed += gasActuallySpentOnCreate;
                parentProgram.RefundCounter += childProgram.RefundCounter;
                parentProgram.Step();
            }
            else
            {
                // Initcode REVERT or OOG - nonce was already incremented before snapshot
                if (completedFrame.SnapshotId.HasValue)
                {
                    parentProgram.ProgramContext.ExecutionStateService.RevertToSnapshot(completedFrame.SnapshotId.Value);
                    // Nonce was incremented before snapshot, so it's preserved after revert
                }
                parentProgram.StackPush(0);
                // Set return data for RETURNDATASIZE/RETURNDATACOPY - the REVERT data from initcode
                parentProgram.ProgramResult.LastCallReturnData = childProgram.ProgramResult.Result;
                var gasActuallySpent = completedFrame.GasAllocated - gasToReturn;
                parentProgram.GasRemaining += gasToReturn;
                parentProgram.TotalGasUsed += gasActuallySpent;
                // Note: RefundCounter is NOT propagated on revert
                parentProgram.Step();
            }
        }

        private void MergeCallResult(CallFrame completedFrame, CallFrame parentFrame)
        {
            var childProgram = completedFrame.Program;
            var parentProgram = parentFrame.Program;

            // Per Yellow Paper: parent receives back whatever gas remains in the callee.
            // The stipend (2300 gas for value calls) is given "for free" but if unused,
            // it IS returned to the parent. This is correct EVM behavior.
            var gasToReturn = childProgram.GasRemaining;
            // Gas actually spent is what was allocated minus what's returned (capped at allocated)
            var gasActuallySpent = completedFrame.GasAllocated - BigInteger.Min(gasToReturn, completedFrame.GasAllocated);

            // If callee returns more gas than was allocated, the excess is the unused stipend.
            // This stipend was already charged in the CALL opcode cost (G_callvalue = 9000 includes
            // G_callstipend = 2300). If unused, we must refund it from TotalGasUsed.
            var excessReturn = gasToReturn - completedFrame.GasAllocated;
            if (excessReturn > 0)
            {
                parentProgram.TotalGasUsed -= excessReturn;
            }

            if (!childProgram.ProgramResult.IsRevert)
            {
                if (completedFrame.SnapshotId.HasValue)
                {
                    parentProgram.ProgramContext.ExecutionStateService.CommitSnapshot(completedFrame.SnapshotId.Value);
                }

                parentProgram.StackPush(1);
                var result = childProgram.ProgramResult.Result;
                parentProgram.ProgramResult.LastCallReturnData = result;

                // Only write return data if there's actual data to write
                // If callee returned no data (STOP vs RETURN), memory should not be modified
                if (result != null && result.Length > 0)
                {
                    var resultLength = Math.Min(completedFrame.ResultMemoryDataLength, result.Length);
                    parentProgram.WriteToMemory(completedFrame.ResultMemoryDataIndex, resultLength, result);
                }
                parentProgram.ProgramResult.Logs.AddRange(childProgram.ProgramResult.Logs);
                parentProgram.ProgramResult.InnerCalls.Add(completedFrame.CallInput);
                parentProgram.ProgramResult.InnerCalls.AddRange(childProgram.ProgramResult.InnerCalls);
                parentProgram.ProgramResult.CreatedContractAccounts.AddRange(childProgram.ProgramResult.CreatedContractAccounts);
                parentProgram.ProgramResult.DeletedContractAccounts.AddRange(childProgram.ProgramResult.DeletedContractAccounts);

                if (completedFrame.TraceEnabled)
                {
                    foreach (var codeItem in childProgram.ProgramResult.InnerContractCodeCalls)
                    {
                        parentProgram.ProgramResult.InsertInnerContractCodeIfDoesNotExist(codeItem.Key, codeItem.Value);
                    }
                }

                parentProgram.GasRemaining += gasToReturn;
                parentProgram.TotalGasUsed += gasActuallySpent;
                parentProgram.RefundCounter += childProgram.RefundCounter;
                parentProgram.Step();
            }
            else
            {
                if (completedFrame.SnapshotId.HasValue)
                {
                    parentProgram.ProgramContext.ExecutionStateService.RevertToSnapshot(completedFrame.SnapshotId.Value);
                }

                parentProgram.StackPush(0);
                var result = childProgram.ProgramResult.Result;
                parentProgram.ProgramResult.LastCallReturnData = result;

                // Per EIP-140: REVERT data is written to the output buffer (same as success)
                if (result != null && result.Length > 0)
                {
                    var resultLength = Math.Min(completedFrame.ResultMemoryDataLength, result.Length);
                    parentProgram.WriteToMemory(completedFrame.ResultMemoryDataIndex, resultLength, result);
                }

                parentProgram.GasRemaining += gasToReturn;
                parentProgram.TotalGasUsed += gasActuallySpent;
                // Note: RefundCounter is NOT propagated on revert - refunds are lost with the reverted state
                parentProgram.Step();
            }
        }

        private async Task<SubCallSetup> StepWithCallStackAsync(CallFrame currentFrame, ProgramInstruction instruction, Stack<CallFrame> callStack)
        {
            var program = currentFrame.Program;
            if (program.Stopped) return null;
            if (instruction.Instruction == null) return null;

            switch (instruction.Instruction.Value)
            {
                case Instruction.CALL:
                    return await SetupCallFrameAsync(program, currentFrame, CallFrameType.Call);
                case Instruction.DELEGATECALL:
                    return await SetupCallFrameAsync(program, currentFrame, CallFrameType.DelegateCall);
                case Instruction.STATICCALL:
                    return await SetupCallFrameAsync(program, currentFrame, CallFrameType.StaticCall);
                case Instruction.CALLCODE:
                    return await SetupCallFrameAsync(program, currentFrame, CallFrameType.CallCode);
                case Instruction.CREATE:
                    return await SetupCreateFrameAsync(program, currentFrame, CallFrameType.Create);
                case Instruction.CREATE2:
                    return await SetupCreateFrameAsync(program, currentFrame, CallFrameType.Create2);
                default:
                    await ExecuteNonCallInstructionAsync(program, instruction);
                    return null;
            }
        }

        private async Task<SubCallSetup> SetupCallFrameAsync(Program program, CallFrame parentFrame, CallFrameType callType)
        {
            var gas = program.StackPopAndConvertToUBigInteger();
            var codeAddress = program.StackPop();
            BigInteger value = 0;
            string from;
            string to;
            bool isStatic = false;

            switch (callType)
            {
                case CallFrameType.StaticCall:
                    from = program.ProgramContext.AddressContract;
                    to = codeAddress.ConvertToEthereumChecksumAddress();
                    isStatic = true;
                    break;
                case CallFrameType.DelegateCall:
                    from = program.ProgramContext.AddressCaller;
                    to = program.ProgramContext.AddressContract;
                    value = program.ProgramContext.Value;
                    break;
                case CallFrameType.CallCode:
                    value = program.StackPopAndConvertToUBigInteger();
                    from = program.ProgramContext.AddressContract;
                    to = program.ProgramContext.AddressContract;
                    break;
                default:
                    value = program.StackPopAndConvertToUBigInteger();
                    from = program.ProgramContext.AddressContract;
                    to = codeAddress.ConvertToEthereumChecksumAddress();
                    break;
            }

            // In STATICCALL context, CALL with value > 0 is not allowed (state modification - transfers funds)
            // CALLCODE with value > 0 IS allowed because it doesn't actually transfer funds
            if (program.ProgramContext.IsStatic && value > 0 && callType == CallFrameType.Call)
            {
                throw new Exceptions.StaticCallViolationException(callType.ToString());
            }

            var dataInputIndexBig = program.StackPopAndConvertToUBigInteger();
            var dataInputLengthBig = program.StackPopAndConvertToUBigInteger();
            var resultMemoryDataIndexBig = program.StackPopAndConvertToUBigInteger();
            var resultMemoryDataLengthBig = program.StackPopAndConvertToUBigInteger();

            if (dataInputIndexBig > int.MaxValue || dataInputLengthBig > int.MaxValue ||
                resultMemoryDataIndexBig > int.MaxValue || resultMemoryDataLengthBig > int.MaxValue)
            {
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            var dataInputIndex = (int)dataInputIndexBig;
            var dataInputLength = (int)dataInputLengthBig;
            var resultMemoryDataIndex = (int)resultMemoryDataIndexBig;
            var resultMemoryDataLength = (int)resultMemoryDataLengthBig;

            if (parentFrame.Depth + 1 > GasConstants.MAX_CALL_DEPTH)
            {
                // Call depth exceeded. If value > 0, the G_callvalue (9000) was already charged
                // and the stipend should be refunded since callee was never invoked.
                var shouldTransferValueDepthCheck = callType != CallFrameType.DelegateCall && callType != CallFrameType.StaticCall;
                if (shouldTransferValueDepthCheck && value > 0)
                {
                    program.GasRemaining += GasConstants.CALL_STIPEND;
                    program.TotalGasUsed -= GasConstants.CALL_STIPEND;
                }
                program.ProgramResult.LastCallReturnData = null;
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            // EVM spec: Expand memory for both input and output regions BEFORE the call
            // This expansion happens regardless of whether the call succeeds or fails
            var inputEnd = dataInputLength > 0 ? dataInputIndex + dataInputLength : 0;
            var outputEnd = resultMemoryDataLength > 0 ? resultMemoryDataIndex + resultMemoryDataLength : 0;
            var requiredMemorySize = Math.Max(inputEnd, outputEnd);
            if (requiredMemorySize > program.Memory.Count)
            {
                program.ExpandMemory(requiredMemorySize);
            }

            var dataInput = new byte[0];
            if (dataInputLength != 0)
            {
                dataInput = new byte[dataInputLength];
                if (dataInputIndex + dataInputLength > program.Memory.Count)
                {
                    var availableLength = Math.Max(0, program.Memory.Count - dataInputIndex);
                    if (availableLength > 0)
                    {
                        var dataToCopy = program.Memory.GetRange(dataInputIndex, availableLength);
                        dataToCopy.CopyTo(dataInput, 0);
                    }
                }
                else
                {
                    var sourceData = program.Memory.GetRange(dataInputIndex, dataInputLength);
                    sourceData.CopyTo(dataInput, 0);
                }
            }

            var callInput = new CallInput
            {
                From = from,
                Value = new HexBigInteger(value),
                To = to,
                Data = dataInput.ToHex(),
                Gas = new HexBigInteger(gas),
                ChainId = new HexBigInteger(program.ProgramContext.ChainId)
            };

            var shouldTransferValue = callType != CallFrameType.DelegateCall && callType != CallFrameType.StaticCall;

            // Check for sufficient balance before proceeding with value transfer
            // If caller doesn't have enough balance, CALL/CALLCODE fails immediately
            if (shouldTransferValue && value > 0)
            {
                var callerAccount = program.ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(program.ProgramContext.AddressContract);
                var callerBalance = callerAccount.Balance.GetTotalBalance();
                if (callerBalance < value)
                {
                    // CALL/CALLCODE fails immediately due to insufficient balance.
                    // The G_callvalue (9000) was already charged in OpcodeGasTable.
                    // Since the callee was never invoked, the stipend (2300) was never given.
                    // The stipend is "free" gas that the callee would have received and could
                    // return to the caller. Since the call failed before invocation, we must
                    // refund it - same as the empty code path where callee returns immediately.
                    // Per EIP-211: failed call clears the return data buffer
                    program.GasRemaining += GasConstants.CALL_STIPEND;
                    program.TotalGasUsed -= GasConstants.CALL_STIPEND;
                    program.ProgramResult.LastCallReturnData = null;
                    program.StackPush(0);
                    program.Step();
                    return new SubCallSetup { ShouldCreateSubCall = false };
                }
            }

            var snapshotId = program.ProgramContext.ExecutionStateService.TakeSnapshot();

            if (shouldTransferValue)
            {
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -value);
            }
            var codeAddressAsChecksum = codeAddress.ConvertToEthereumChecksumAddress();
            program.ProgramContext.RecordAddressAccess(codeAddressAsChecksum);
            var byteCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(codeAddressAsChecksum);

            if (byteCode == null || byteCode.Length == 0)
            {
                if (shouldTransferValue)
                {
                    program.ProgramContext.ExecutionStateService.UpsertInternalBalance(to, value);
                }

                // Calculate gas that would be forwarded (for trace accuracy - geth includes this in CALL cost)
                var maxAllowedGasForEmpty = program.GasRemaining - (program.GasRemaining / 64);
                var gasToForwardForTrace = gas > maxAllowedGasForEmpty ? maxAllowedGasForEmpty : gas;
                if (gasToForwardForTrace < 0) gasToForwardForTrace = 0;

                if (dataInput != null)
                {
                    // Use codeAddressAsChecksum for precompile check, not 'to' (matters for CALLCODE/DELEGATECALL)
                    var isPrecompile = EvmProgramExecution.PreCompiledContracts.IsPrecompiledAdress(codeAddressAsChecksum);
                    if (isPrecompile)
                    {
                        // Calculate precompile gas cost and consume it
                        var precompileGasCost = EvmProgramExecution.PreCompiledContracts.GetPrecompileGasCost(codeAddressAsChecksum, dataInput);

                        // Check if we have enough gas (gas forwarded to precompile)
                        if (gasToForwardForTrace < precompileGasCost)
                        {
                            // Out of gas - precompile call fails, consume ALL forwarded gas, revert state
                            program.GasRemaining -= (long)gasToForwardForTrace;
                            program.TotalGasUsed += (long)gasToForwardForTrace;
                            program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                            program.StackPush(0);
                            program.ProgramResult.LastCallReturnData = null;
                            program.Step();
                            return new SubCallSetup { ShouldCreateSubCall = false, IsPrecompileHandled = true, GasForwarded = gasToForwardForTrace };
                        }

                        // Consume the precompile gas (deduct from caller's remaining gas)
                        program.GasRemaining -= (long)precompileGasCost;
                        program.TotalGasUsed += (long)precompileGasCost;

                        try
                        {
                            var precompiledResult = EvmProgramExecution.PreCompiledContracts.ExecutePreCompile(codeAddressAsChecksum, dataInput);
                            // Write result to memory, respecting the output size limit
                            var resultLength = Math.Min(resultMemoryDataLength, precompiledResult?.Length ?? 0);
                            program.WriteToMemory(resultMemoryDataIndex, resultLength, precompiledResult);
                            program.ProgramResult.LastCallReturnData = precompiledResult;
                            // Precompile succeeded - commit the snapshot
                            program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                            // For precompile with value > 0, refund the stipend.
                            // The G_callvalue (9000) includes G_callstipend (2300), but precompiles
                            // don't receive/use the stipend since they're not EVM code. Refund it.
                            if (shouldTransferValue && value > 0)
                            {
                                program.GasRemaining += GasConstants.CALL_STIPEND;
                                program.TotalGasUsed -= GasConstants.CALL_STIPEND;
                            }
                        }
                        catch
                        {
                            // Precompile execution failed (e.g., invalid input)
                            // Per EIP-196/197: consume all forwarded gas, return 0 (failure)
                            var remainingForwardedGas = gasToForwardForTrace - precompileGasCost;
                            if (remainingForwardedGas > 0)
                            {
                                program.GasRemaining -= (long)remainingForwardedGas;
                                program.TotalGasUsed += (long)remainingForwardedGas;
                            }
                            // Revert state changes (including any value transfer)
                            program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                            program.ProgramResult.LastCallReturnData = null;
                            program.StackPush(0);
                            program.Step();
                            return new SubCallSetup { ShouldCreateSubCall = false, IsPrecompileHandled = true, GasForwarded = gasToForwardForTrace };
                        }
                    }
                    else
                    {
                        // Non-precompile empty code - commit snapshot and handle
                        program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                        program.ProgramResult.LastCallReturnData = null;

                        // For empty code with value > 0, return the stipend to the caller.
                        // When calling code with value, the subcall receives allocated_gas + stipend (free).
                        // If callee uses 0 gas, it returns all including stipend.
                        // For empty code, we simulate this: the stipend is "free" gas that shouldn't
                        // count towards gas used. Both GasRemaining and TotalGasUsed must be adjusted.
                        if (shouldTransferValue && value > 0)
                        {
                            program.GasRemaining += GasConstants.CALL_STIPEND;
                            program.TotalGasUsed -= GasConstants.CALL_STIPEND;
                        }
                    }

                    program.StackPush(1);
                    program.Step();
                }
                else
                {
                    // dataInput is null - just commit the value transfer and succeed
                    program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                    program.ProgramResult.LastCallReturnData = null;

                    // For empty code with value > 0, return the stipend to the caller.
                    // Same as the non-precompile case above - stipend is "free" gas that was
                    // included in G_callvalue (9000) but should be refunded when unused.
                    if (shouldTransferValue && value > 0)
                    {
                        program.GasRemaining += GasConstants.CALL_STIPEND;
                        program.TotalGasUsed -= GasConstants.CALL_STIPEND;
                    }

                    program.StackPush(1);
                    program.Step();
                }
                return new SubCallSetup { ShouldCreateSubCall = false, IsPrecompileHandled = true, GasForwarded = gasToForwardForTrace };
            }

            var programContext = new ProgramContext(
                callInput,
                program.ProgramContext.ExecutionStateService,
                program.ProgramContext.AddressOrigin,
                codeAddress: codeAddressAsChecksum,
                blockNumber: (long)program.ProgramContext.BlockNumber,
                timestamp: (long)program.ProgramContext.Timestamp,
                coinbase: program.ProgramContext.Coinbase,
                baseFee: (long)program.ProgramContext.BaseFee);
            programContext.Difficulty = program.ProgramContext.Difficulty;
            programContext.GasLimit = program.ProgramContext.GasLimit;
            programContext.GasPrice = program.ProgramContext.GasPrice;
            programContext.Depth = parentFrame.Depth + 1;
            programContext.IsStatic = isStatic || program.ProgramContext.IsStatic;
            programContext.EnforceGasSentry = program.ProgramContext.EnforceGasSentry;
            programContext.SetAccessListTracker(program.ProgramContext.AccessListTracker);

            var callProgram = new Program(byteCode, programContext);

            var maxAllowedGas = program.GasRemaining - (program.GasRemaining / 64);
            var gasToAllocate = gas > maxAllowedGas ? maxAllowedGas : gas;
            if (gasToAllocate < 0) gasToAllocate = 0;

            if (shouldTransferValue && value > 0)
            {
                callProgram.GasRemaining = gasToAllocate + GasConstants.CALL_STIPEND;
            }
            else
            {
                callProgram.GasRemaining = gasToAllocate;
            }
            program.GasRemaining -= gasToAllocate;

            if (shouldTransferValue)
            {
                program.ProgramContext.ExecutionStateService.UpsertInternalBalance(to, value);
            }

            if (parentFrame.TraceEnabled)
            {
                program.ProgramResult.InsertInnerContractCodeIfDoesNotExist(codeAddressAsChecksum, callProgram.Instructions);
            }

            var newFrame = new CallFrame
            {
                Program = callProgram,
                VmExecutionCounter = parentFrame.VmExecutionCounter + 1,
                ProgramExecutionCounter = 0,
                Depth = parentFrame.Depth + 1,
                TraceEnabled = parentFrame.TraceEnabled,
                FrameType = callType,
                ResultMemoryDataIndex = resultMemoryDataIndex,
                ResultMemoryDataLength = resultMemoryDataLength,
                Value = value,
                CallInput = callInput,
                GasAllocated = gasToAllocate,
                SnapshotId = snapshotId
            };

            return new SubCallSetup { ShouldCreateSubCall = true, NewFrame = newFrame, GasForwarded = gasToAllocate };
        }

        private async Task<SubCallSetup> SetupCreateFrameAsync(Program program, CallFrame parentFrame, CallFrameType createType)
        {
            if (program.ProgramContext.IsStatic)
            {
                throw new Exceptions.StaticCallViolationException(createType == CallFrameType.Create2 ? "CREATE2" : "CREATE");
            }

            var value = program.StackPopAndConvertToUBigInteger();
            var memoryIndexBig = program.StackPopAndConvertToUBigInteger();
            var memoryLengthBig = program.StackPopAndConvertToUBigInteger();

            byte[] salt = null;
            if (createType == CallFrameType.Create2)
            {
                salt = program.StackPop();
            }

            if (memoryIndexBig > int.MaxValue || memoryLengthBig > int.MaxValue)
            {
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            var memoryIndex = (int)memoryIndexBig;
            var memoryLength = (int)memoryLengthBig;

            if (memoryLength > GasConstants.MAX_INITCODE_SIZE)
            {
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            if (parentFrame.Depth + 1 > GasConstants.MAX_CALL_DEPTH)
            {
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            // EVM spec: Expand memory for the initcode region BEFORE the create
            if (memoryLength > 0 && memoryIndex + memoryLength > program.Memory.Count)
            {
                program.ExpandMemory(memoryIndex + memoryLength);
            }

            var contractAddress = program.ProgramContext.AddressContract;
            var nonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(contractAddress);

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

            string newContractAddress;
            if (createType == CallFrameType.Create2)
            {
                newContractAddress = ContractUtils.CalculateCreate2Address(contractAddress, salt.ToHex(), byteCode.ToHex());
            }
            else
            {
                newContractAddress = ContractUtils.CalculateContractAddress(contractAddress, nonce);
            }

            // EIP-2929: Mark new contract address as warm
            program.ProgramContext.ExecutionStateService.MarkAddressAsWarm(newContractAddress);

            // EIP-3541: Reject initcode starting with 0xEF (before incrementing nonce)
            if (byteCode.Length > 0 && byteCode[0] == 0xEF)
            {
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            // Check sender has sufficient balance before incrementing nonce
            var senderBalance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(contractAddress);
            if (senderBalance < value)
            {
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            // EIP-2681: Nonce limit is 2^64-1. If nonce would exceed, CREATE fails without incrementing
            var maxNonce = BigInteger.Pow(2, 64) - 1;
            if (nonce >= maxNonce)
            {
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            var callInput = new CallInput
            {
                From = contractAddress,
                Value = new HexBigInteger(value),
                To = newContractAddress,
                ChainId = new HexBigInteger(program.ProgramContext.ChainId)
            };

            // Per Yellow Paper and geth: increment nonce FIRST, before snapshot
            // This ensures nonce is preserved even if CREATE fails
            program.ProgramContext.ExecutionStateService.SetNonce(contractAddress, nonce + 1);

            // Take snapshot AFTER nonce increment - nonce won't be reverted on failure
            var snapshotId = program.ProgramContext.ExecutionStateService.TakeSnapshot();

            // EIP-684 + EIP-7610: Check for address collision
            // Collision occurs if target has: code OR nonce > 0 OR non-empty storage
            var targetAccount = await program.ProgramContext.ExecutionStateService.LoadBalanceNonceAndCodeFromStorageAsync(newContractAddress);
            var targetHasCode = targetAccount.Code != null && targetAccount.Code.Length > 0;
            var targetHasNonce = targetAccount.Nonce.HasValue && targetAccount.Nonce.Value > 0;
            var targetHasStorage = targetAccount.Storage != null && targetAccount.Storage.Count > 0;
            if (targetHasCode || targetHasNonce || targetHasStorage)
            {
                // Collision: commit snapshot (keeps current state including nonce increment)
                program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                var collisionGas = program.GasRemaining - (program.GasRemaining / 64);
                program.GasRemaining -= collisionGas;
                program.TotalGasUsed += collisionGas;
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            program.ProgramContext.ExecutionStateService.UpsertInternalBalance(program.ProgramContext.AddressContract, -value);

            var programContext = new ProgramContext(
                callInput,
                program.ProgramContext.ExecutionStateService,
                program.ProgramContext.AddressOrigin,
                null,
                (long)program.ProgramContext.BlockNumber,
                (long)program.ProgramContext.Timestamp,
                program.ProgramContext.Coinbase,
                (long)program.ProgramContext.BaseFee);
            programContext.Difficulty = program.ProgramContext.Difficulty;
            programContext.GasLimit = program.ProgramContext.GasLimit;
            programContext.GasPrice = program.ProgramContext.GasPrice;
            programContext.Depth = parentFrame.Depth + 1;
            programContext.SetAccessListTracker(program.ProgramContext.AccessListTracker);

            var callProgram = new Program(byteCode, programContext);

            var gasToAllocate = program.GasRemaining - (program.GasRemaining / 64);
            if (gasToAllocate < 0) gasToAllocate = 0;
            callProgram.GasRemaining = gasToAllocate;
            program.GasRemaining -= gasToAllocate;

            program.ProgramContext.ExecutionStateService.UpsertInternalBalance(newContractAddress, value);

            // EIP-161: New contracts start with nonce = 1 BEFORE initcode runs
            // This is critical for correct address calculation if initcode does CREATE
            program.ProgramContext.ExecutionStateService.SetNonce(newContractAddress, 1);

            var newFrame = new CallFrame
            {
                Program = callProgram,
                VmExecutionCounter = parentFrame.VmExecutionCounter + 1,
                ProgramExecutionCounter = 0,
                Depth = parentFrame.Depth + 1,
                TraceEnabled = parentFrame.TraceEnabled,
                FrameType = createType,
                NewContractAddress = newContractAddress,
                Value = value,
                CallInput = callInput,
                GasAllocated = gasToAllocate,
                SnapshotId = snapshotId
            };

            return new SubCallSetup { ShouldCreateSubCall = true, NewFrame = newFrame, GasForwarded = gasToAllocate };
        }

        private async Task ExecuteNonCallInstructionAsync(Program program, ProgramInstruction instruction)
        {
            switch (instruction.Instruction.Value)
            {
                case Instruction.STOP:
                    program.Stop();
                    break;
                case Instruction.ADD:
                    EvmProgramExecution.Arithmetic.Add(program);
                    break;
                case Instruction.MUL:
                    EvmProgramExecution.Arithmetic.Mul(program);
                    break;
                case Instruction.SUB:
                    EvmProgramExecution.Arithmetic.Sub(program);
                    break;
                case Instruction.DIV:
                    EvmProgramExecution.Arithmetic.Div(program);
                    break;
                case Instruction.SDIV:
                    EvmProgramExecution.Arithmetic.SDiv(program);
                    break;
                case Instruction.MOD:
                    EvmProgramExecution.Arithmetic.Mod(program);
                    break;
                case Instruction.SMOD:
                    EvmProgramExecution.Arithmetic.SMod(program);
                    break;
                case Instruction.ADDMOD:
                    EvmProgramExecution.Arithmetic.AddMod(program);
                    break;
                case Instruction.MULMOD:
                    EvmProgramExecution.Arithmetic.MulMod(program);
                    break;
                case Instruction.EXP:
                    EvmProgramExecution.Arithmetic.Exp(program);
                    break;
                case Instruction.LT:
                    EvmProgramExecution.Bitwise.LT(program);
                    break;
                case Instruction.GT:
                    EvmProgramExecution.Bitwise.GT(program);
                    break;
                case Instruction.SLT:
                    EvmProgramExecution.Bitwise.SLT(program);
                    break;
                case Instruction.SGT:
                    EvmProgramExecution.Bitwise.SGT(program);
                    break;
                case Instruction.EQ:
                    EvmProgramExecution.Bitwise.EQ(program);
                    break;
                case Instruction.ISZERO:
                    EvmProgramExecution.Bitwise.IsZero(program);
                    break;
                case Instruction.AND:
                    EvmProgramExecution.Bitwise.And(program);
                    break;
                case Instruction.OR:
                    EvmProgramExecution.Bitwise.Or(program);
                    break;
                case Instruction.XOR:
                    EvmProgramExecution.Bitwise.Xor(program);
                    break;
                case Instruction.NOT:
                    EvmProgramExecution.Bitwise.Not(program);
                    break;
                case Instruction.BYTE:
                    EvmProgramExecution.Bitwise.Byte(program);
                    break;
                case Instruction.SHL:
                    EvmProgramExecution.Bitwise.ShiftLeft(program);
                    break;
                case Instruction.SHR:
                    EvmProgramExecution.Bitwise.ShiftRight(program);
                    break;
                case Instruction.SAR:
                    EvmProgramExecution.Bitwise.ShiftSignedRight(program);
                    break;
                case Instruction.SIGNEXTEND:
                    EvmProgramExecution.Bitwise.SignExtend(program);
                    break;
                case Instruction.ORIGIN:
                    EvmProgramExecution.CallInput.Origin(program);
                    break;
                case Instruction.CALLER:
                    EvmProgramExecution.CallInput.Caller(program);
                    break;
                case Instruction.CALLVALUE:
                    EvmProgramExecution.CallInput.CallValue(program);
                    break;
                case Instruction.CALLDATALOAD:
                    EvmProgramExecution.CallData.CallDataLoad(program);
                    break;
                case Instruction.CALLDATASIZE:
                    EvmProgramExecution.CallData.CallDataSize(program);
                    break;
                case Instruction.CALLDATACOPY:
                    EvmProgramExecution.CallData.CallDataCopy(program);
                    break;
                case Instruction.CODESIZE:
                    EvmProgramExecution.Code.CodeSize(program);
                    break;
                case Instruction.CODECOPY:
                    EvmProgramExecution.Code.CodeCopy(program);
                    break;
                case Instruction.EXTCODESIZE:
                    await EvmProgramExecution.Code.ExtCodeSizeAsync(program);
                    break;
                case Instruction.EXTCODECOPY:
                    await EvmProgramExecution.Code.ExtCodeCopyAsync(program);
                    break;
                case Instruction.EXTCODEHASH:
                    await EvmProgramExecution.Code.ExtCodeHashAsync(program);
                    break;
                case Instruction.KECCAK256:
                    EvmProgramExecution.BlockchainCurrentContractContext.SHA3(program);
                    break;
                case Instruction.ADDRESS:
                    EvmProgramExecution.BlockchainCurrentContractContext.Address(program);
                    break;
                case Instruction.BALANCE:
                    await EvmProgramExecution.BlockchainCurrentContractContext.BalanceAsync(program);
                    break;
                case Instruction.COINBASE:
                    EvmProgramExecution.BlockchainCurrentContractContext.Coinbase(program);
                    break;
                case Instruction.TIMESTAMP:
                    EvmProgramExecution.BlockchainCurrentContractContext.TimeStamp(program);
                    break;
                case Instruction.NUMBER:
                    EvmProgramExecution.BlockchainCurrentContractContext.BlockNumber(program);
                    break;
                case Instruction.SELFBALANCE:
                    await EvmProgramExecution.BlockchainCurrentContractContext.SelfBalanceAsync(program);
                    break;
                case Instruction.GASPRICE:
                    EvmProgramExecution.BlockchainCurrentContractContext.GasPrice(program);
                    break;
                case Instruction.GASLIMIT:
                    EvmProgramExecution.BlockchainCurrentContractContext.GasLimit(program);
                    break;
                case Instruction.GAS:
                    EvmProgramExecution.BlockchainCurrentContractContext.Gas(program);
                    break;
                case Instruction.DIFFICULTY:
                    EvmProgramExecution.BlockchainCurrentContractContext.Difficulty(program);
                    break;
                case Instruction.CHAINID:
                    EvmProgramExecution.BlockchainCurrentContractContext.ChainId(program);
                    break;
                case Instruction.BASEFEE:
                    EvmProgramExecution.BlockchainCurrentContractContext.BaseFee(program);
                    break;
                case Instruction.BLOBBASEFEE:
                    EvmProgramExecution.BlockchainCurrentContractContext.BlobBaseFee(program);
                    break;
                case Instruction.BLOBHASH:
                    EvmProgramExecution.BlockchainCurrentContractContext.BlobHash(program);
                    break;
                case Instruction.BLOCKHASH:
                    await EvmProgramExecution.BlockchainCurrentContractContext.BlockHashAsync(program);
                    break;
                case Instruction.POP:
                    EvmProgramExecution.StackFlowExecution.Pop(program);
                    break;
                case Instruction.JUMP:
                    EvmProgramExecution.StackFlowExecution.Jump(program);
                    break;
                case Instruction.JUMPI:
                    EvmProgramExecution.StackFlowExecution.Jumpi(program);
                    break;
                case Instruction.JUMPDEST:
                    EvmProgramExecution.StackFlowExecution.JumpDest(program);
                    break;
                case Instruction.PC:
                    EvmProgramExecution.StackFlowExecution.PC(program);
                    break;
                case Instruction.PUSH0:
                    EvmProgramExecution.StackFlowExecution.PushZero(program);
                    break;
                case Instruction.PUSH1:
                case Instruction.PUSH2:
                case Instruction.PUSH3:
                case Instruction.PUSH4:
                case Instruction.PUSH5:
                case Instruction.PUSH6:
                case Instruction.PUSH7:
                case Instruction.PUSH8:
                case Instruction.PUSH9:
                case Instruction.PUSH10:
                case Instruction.PUSH11:
                case Instruction.PUSH12:
                case Instruction.PUSH13:
                case Instruction.PUSH14:
                case Instruction.PUSH15:
                case Instruction.PUSH16:
                case Instruction.PUSH17:
                case Instruction.PUSH18:
                case Instruction.PUSH19:
                case Instruction.PUSH20:
                case Instruction.PUSH21:
                case Instruction.PUSH22:
                case Instruction.PUSH23:
                case Instruction.PUSH24:
                case Instruction.PUSH25:
                case Instruction.PUSH26:
                case Instruction.PUSH27:
                case Instruction.PUSH28:
                case Instruction.PUSH29:
                case Instruction.PUSH30:
                case Instruction.PUSH31:
                case Instruction.PUSH32:
                    EvmProgramExecution.StackFlowExecution.Push(program);
                    break;
                case Instruction.DUP1:
                case Instruction.DUP2:
                case Instruction.DUP3:
                case Instruction.DUP4:
                case Instruction.DUP5:
                case Instruction.DUP6:
                case Instruction.DUP7:
                case Instruction.DUP8:
                case Instruction.DUP9:
                case Instruction.DUP10:
                case Instruction.DUP11:
                case Instruction.DUP12:
                case Instruction.DUP13:
                case Instruction.DUP14:
                case Instruction.DUP15:
                case Instruction.DUP16:
                    EvmProgramExecution.StackFlowExecution.Dup(program);
                    break;
                case Instruction.SWAP1:
                case Instruction.SWAP2:
                case Instruction.SWAP3:
                case Instruction.SWAP4:
                case Instruction.SWAP5:
                case Instruction.SWAP6:
                case Instruction.SWAP7:
                case Instruction.SWAP8:
                case Instruction.SWAP9:
                case Instruction.SWAP10:
                case Instruction.SWAP11:
                case Instruction.SWAP12:
                case Instruction.SWAP13:
                case Instruction.SWAP14:
                case Instruction.SWAP15:
                case Instruction.SWAP16:
                    EvmProgramExecution.StackFlowExecution.Swap(program);
                    break;
                case Instruction.MLOAD:
                    EvmProgramExecution.StorageMemory.MLoad(program);
                    break;
                case Instruction.MSTORE:
                    EvmProgramExecution.StorageMemory.MStore(program);
                    break;
                case Instruction.MSTORE8:
                    EvmProgramExecution.StorageMemory.MStore8(program);
                    break;
                case Instruction.MSIZE:
                    EvmProgramExecution.StorageMemory.MSize(program);
                    break;
                case Instruction.MCOPY:
                    EvmProgramExecution.StorageMemory.MCopy(program);
                    break;
                case Instruction.SLOAD:
                    await EvmProgramExecution.StorageMemory.SLoad(program);
                    break;
                case Instruction.SSTORE:
                    await EvmProgramExecution.StorageMemory.SStore(program);
                    break;
                case Instruction.TLOAD:
                    EvmProgramExecution.BlockchainCurrentContractContext.TLoad(program);
                    break;
                case Instruction.TSTORE:
                    EvmProgramExecution.BlockchainCurrentContractContext.TStore(program);
                    break;
                case Instruction.LOG0:
                case Instruction.LOG1:
                case Instruction.LOG2:
                case Instruction.LOG3:
                case Instruction.LOG4:
                    EvmProgramExecution.ReturnRevertLogExecution.Log(program);
                    break;
                case Instruction.REVERT:
                    EvmProgramExecution.ReturnRevertLogExecution.Revert(program);
                    break;
                case Instruction.RETURN:
                    EvmProgramExecution.ReturnRevertLogExecution.Return(program);
                    break;
                case Instruction.RETURNDATASIZE:
                    EvmProgramExecution.ReturnRevertLogExecution.ReturnDataSize(program);
                    break;
                case Instruction.RETURNDATACOPY:
                    EvmProgramExecution.ReturnRevertLogExecution.ReturnDataCopy(program);
                    break;
                case Instruction.SELFDESTRUCT:
                    await EvmProgramExecution.CallingCreation.SelfDestructAsync(program);
                    break;
                case Instruction.INVALID:
                    program.GasRemaining = 0;
                    program.ProgramResult.IsRevert = true;
                    program.Stop();
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown instruction: {instruction.Instruction.Value}");
            }
        }
      
#if ENABLE_OLD_RECURSIVE_ARCHITECTURE
        // OLD RECURSIVE ARCHITECTURE - DISABLED TO PREVENT STACK OVERFLOW
        // Use ExecuteWithCallStackAsync instead which uses an explicit Stack<CallFrame>
        public async Task<Program> ExecuteAsync(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true, bool staticCall = false, bool skipInitialBalance = false)
        {

            var programExecutionCounter = 0;
            if (!skipInitialBalance)
            {
                program.ProgramContext.InitialiaseContractBalanceFromCallInputValue();
            }

            while(program.Stopped != true)
            {
                var currentInstruction = program.GetCurrentInstruction();


                var gasCost = await OpcodeGasTable.GetGasCostAsync(currentInstruction.Instruction.Value, program);
                var gasBeforeOp = program.GasRemaining; // Capture gas BEFORE deduction for trace
                program.UpdateGasUsed(gasCost);
                if (traceEnabled)
                {

                    var trace = ProgramTrace.CreateTraceFromCurrentProgram(program.ProgramContext.AddressContract, vmExecutionCounter, programExecutionCounter, depth, program, currentInstruction, program.ProgramContext.CodeAddress);
                    trace.GasCost = gasCost;
                    trace.GasRemaining = gasBeforeOp;

                    program.Trace.Add(trace);
#if DEBUG
                    if (EnableTraceToDebugOuptput)
                    {
                        Debug.WriteLine(trace.ToString());
                    }
#endif
                }
                var innerTrace = await StepAsync(program, vmExecutionCounter, depth, traceEnabled);
                programExecutionCounter++;
                vmExecutionCounter = vmExecutionCounter + 1 + innerTrace.Count;
                program.Trace.AddRange(innerTrace);
            }

            if (traceEnabled && program.StoppedImplicitly)
            {
                var implicitStopInstruction = new ProgramInstruction
                {
                    Instruction = Instruction.STOP,
                    Value = 0,
                    Step = program.ByteCode.Length
                };
                var implicitStopTrace = ProgramTrace.CreateTraceFromCurrentProgram(
                    program.ProgramContext.AddressContract,
                    vmExecutionCounter,
                    programExecutionCounter,
                    depth,
                    program,
                    implicitStopInstruction,
                    program.ProgramContext.CodeAddress);
                program.Trace.Add(implicitStopTrace);
            }

            return program;
        }
        public async Task<List<ProgramTrace>> StepAsync(Program program, int vmExecutionCounter, int depth = 0, bool traceEnabled = true)
        {
            var innerTraceResult = new List<ProgramTrace>();
            if (!program.Stopped) 
            { 
                var instruction = program.GetCurrentInstruction();
                if (instruction.Instruction != null)
                {

                    switch (instruction.Instruction.Value)
                    {
                        case Instruction.STOP:
                            program.Stop();
                            break;
                        case Instruction.ADD:
                            EvmProgramExecution.Arithmetic.Add(program);
                            break;
                        case Instruction.MUL:
                            EvmProgramExecution.Arithmetic.Mul(program);
                            break;
                        case Instruction.SUB:
                            EvmProgramExecution.Arithmetic.Sub(program);
                            break;
                        case Instruction.DIV:
                            EvmProgramExecution.Arithmetic.Div(program);
                            break;
                        case Instruction.SDIV:
                            EvmProgramExecution.Arithmetic.SDiv(program);
                            break;
                        case Instruction.MOD:
                            EvmProgramExecution.Arithmetic.Mod(program);
                            break;
                        case Instruction.SMOD:
                            EvmProgramExecution.Arithmetic.SMod(program);
                            
                            break;
                        case Instruction.ADDMOD:
                            EvmProgramExecution.Arithmetic.AddMod(program);
                            break;
                        case Instruction.MULMOD:
                            EvmProgramExecution.Arithmetic.MulMod(program);
                            break;
                        case Instruction.EXP:
                            EvmProgramExecution.Arithmetic.Exp(program);
                            break;
                   //------------------------///    
                        case Instruction.LT:
                            EvmProgramExecution.Bitwise.LT(program);
                            break;
                        case Instruction.GT:
                            EvmProgramExecution.Bitwise.GT(program);
                            break;
                        case Instruction.SLT:
                            EvmProgramExecution.Bitwise.SLT(program);
                            break;
                        case Instruction.SGT:
                            EvmProgramExecution.Bitwise.SGT(program);
                            break;
                        case Instruction.EQ:
                            EvmProgramExecution.Bitwise.EQ(program);
                            break;
                        case Instruction.ISZERO:
                            EvmProgramExecution.Bitwise.IsZero(program);
                            break;
                        case Instruction.AND:
                            EvmProgramExecution.Bitwise.And(program);
                            break;
                        case Instruction.OR:
                            EvmProgramExecution.Bitwise.Or(program);
                            break;
                        case Instruction.XOR:
                            EvmProgramExecution.Bitwise.Xor(program);
                            break;
                        case Instruction.NOT:
                            EvmProgramExecution.Bitwise.Not(program);
                            break;
                        case Instruction.BYTE:
                            EvmProgramExecution.Bitwise.Byte(program);
                            break;
                        case Instruction.SHL:
                            EvmProgramExecution.Bitwise.ShiftLeft(program);
                            break;
                        case Instruction.SHR:
                            EvmProgramExecution.Bitwise.ShiftRight(program);
                            break;
                        case Instruction.SAR:
                            EvmProgramExecution.Bitwise.ShiftSignedRight(program);
                            break;
                        case Instruction.SIGNEXTEND:
                            EvmProgramExecution.Bitwise.SignExtend(program);
                            break;
                    //-------------------///
                        case Instruction.ORIGIN:
                            EvmProgramExecution.CallInput.Origin(program);
                            break;
                        case Instruction.CALLER:
                            EvmProgramExecution.CallInput.Caller(program);
                            break;
                        case Instruction.CALLVALUE:
                            EvmProgramExecution.CallInput.CallValue(program);
                      //--------------------//      
                            break;
                        case Instruction.CALLDATALOAD:
                            EvmProgramExecution.CallData.CallDataLoad(program);
                            break;
                        case Instruction.CALLDATASIZE:
                            EvmProgramExecution.CallData.CallDataSize(program);
                            break;
                        case Instruction.CALLDATACOPY:
                            EvmProgramExecution.CallData.CallDataCopy(program);
                            break;
                        //--------------------//      
                        case Instruction.CODESIZE:
                            EvmProgramExecution.Code.CodeSize(program);
                            break;
                        case Instruction.CODECOPY:
                            EvmProgramExecution.Code.CodeCopy(program);
                            break;
                        case Instruction.EXTCODESIZE:
                            await EvmProgramExecution.Code.ExtCodeSizeAsync(program);
                            break;
                        case Instruction.EXTCODECOPY:
                            await EvmProgramExecution.Code.ExtCodeCopyAsync(program);
                            break;
                        case Instruction.EXTCODEHASH:
                            await EvmProgramExecution.Code.ExtCodeHashAsync(program);
                            break;
                        //--------------------//      
                        case Instruction.KECCAK256:
                            EvmProgramExecution.BlockchainCurrentContractContext.SHA3(program);
                            break;
                        case Instruction.ADDRESS:
                            EvmProgramExecution.BlockchainCurrentContractContext.Address(program);
                            break;
                        case Instruction.BALANCE:
                            await EvmProgramExecution.BlockchainCurrentContractContext.BalanceAsync(program);
                            break;
                        case Instruction.COINBASE:
                            EvmProgramExecution.BlockchainCurrentContractContext.Coinbase(program);
                            break;
                        case Instruction.TIMESTAMP:
                            EvmProgramExecution.BlockchainCurrentContractContext.TimeStamp(program);
                            break;
                        case Instruction.NUMBER:
                            EvmProgramExecution.BlockchainCurrentContractContext.BlockNumber(program);
                            break;
                        case Instruction.SELFBALANCE:
                            await EvmProgramExecution.BlockchainCurrentContractContext.SelfBalanceAsync(program);
                            break;
                        case Instruction.GASPRICE:
                            EvmProgramExecution.BlockchainCurrentContractContext.GasPrice(program);
                            break;
                        case Instruction.GASLIMIT:
                            EvmProgramExecution.BlockchainCurrentContractContext.GasLimit(program);
                            break;
                        case Instruction.GAS:
                            EvmProgramExecution.BlockchainCurrentContractContext.Gas(program);
                            
                            break;
                        case Instruction.DIFFICULTY:
                            EvmProgramExecution.BlockchainCurrentContractContext.Difficulty(program);
                            break;
                       
                        case Instruction.CHAINID:
                            EvmProgramExecution.BlockchainCurrentContractContext.ChainId(program);
                            
                            break;
                        case Instruction.BASEFEE:
                            EvmProgramExecution.BlockchainCurrentContractContext.BaseFee(program);
                            break;
                        case Instruction.BLOBBASEFEE:
                            EvmProgramExecution.BlockchainCurrentContractContext.BlobBaseFee(program);
                            break;
                        case Instruction.BLOBHASH:
                            EvmProgramExecution.BlockchainCurrentContractContext.BlobHash(program);
                            break;
                        case Instruction.BLOCKHASH:
                            await EvmProgramExecution.BlockchainCurrentContractContext.BlockHashAsync(program);
                            break;
                        //--------------------//      
                        case Instruction.POP:
                             EvmProgramExecution.StackFlowExecution.Pop(program);
                            break;
                        case Instruction.JUMP:
                            EvmProgramExecution.StackFlowExecution.Jump(program);
                            break;
                        case Instruction.JUMPI:
                            EvmProgramExecution.StackFlowExecution.Jumpi(program);
                            break;

                        case Instruction.JUMPDEST:
                            EvmProgramExecution.StackFlowExecution.JumpDest(program);
                            
                            break;
                        case Instruction.PC:
                            EvmProgramExecution.StackFlowExecution.PC(program);
                            break;
                        case Instruction.PUSH0:
                            EvmProgramExecution.StackFlowExecution.PushZero(program);
                            break;
                        case Instruction.PUSH1:
                        case Instruction.PUSH2:
                        case Instruction.PUSH3:
                        case Instruction.PUSH4:
                        case Instruction.PUSH5:
                        case Instruction.PUSH6:
                        case Instruction.PUSH7:
                        case Instruction.PUSH8:
                        case Instruction.PUSH9:
                        case Instruction.PUSH10:
                        case Instruction.PUSH11:
                        case Instruction.PUSH12:
                        case Instruction.PUSH13:
                        case Instruction.PUSH14:
                        case Instruction.PUSH15:
                        case Instruction.PUSH16:
                        case Instruction.PUSH17:
                        case Instruction.PUSH18:
                        case Instruction.PUSH19:
                        case Instruction.PUSH20:
                        case Instruction.PUSH21:
                        case Instruction.PUSH22:
                        case Instruction.PUSH23:
                        case Instruction.PUSH24:
                        case Instruction.PUSH25:
                        case Instruction.PUSH26:
                        case Instruction.PUSH27:
                        case Instruction.PUSH28:
                        case Instruction.PUSH29:
                        case Instruction.PUSH30:
                        case Instruction.PUSH31:
                        case Instruction.PUSH32:
                            EvmProgramExecution.StackFlowExecution.Push(program);
                            break;
                        case Instruction.DUP1:
                        case Instruction.DUP2:
                        case Instruction.DUP3:
                        case Instruction.DUP4:
                        case Instruction.DUP5:
                        case Instruction.DUP6:
                        case Instruction.DUP7:
                        case Instruction.DUP8:
                        case Instruction.DUP9:
                        case Instruction.DUP10:
                        case Instruction.DUP11:
                        case Instruction.DUP12:
                        case Instruction.DUP13:
                        case Instruction.DUP14:
                        case Instruction.DUP15:
                        case Instruction.DUP16:
                            EvmProgramExecution.StackFlowExecution.Dup(program);
                            break;
                        case Instruction.SWAP1:
                        case Instruction.SWAP2:
                        case Instruction.SWAP3:
                        case Instruction.SWAP4:
                        case Instruction.SWAP5:
                        case Instruction.SWAP6:
                        case Instruction.SWAP7:
                        case Instruction.SWAP8:
                        case Instruction.SWAP9:
                        case Instruction.SWAP10:
                        case Instruction.SWAP11:
                        case Instruction.SWAP12:
                        case Instruction.SWAP13:
                        case Instruction.SWAP14:
                        case Instruction.SWAP15:
                        case Instruction.SWAP16:
                            EvmProgramExecution.StackFlowExecution.Swap(program);
                            break;
                        //--------------------//      
                        case Instruction.MLOAD:
                            EvmProgramExecution.StorageMemory.MLoad(program);
                            break;
                        case Instruction.MSTORE:
                            EvmProgramExecution.StorageMemory.MStore(program);
                            break;
                        case Instruction.MSTORE8:
                            EvmProgramExecution.StorageMemory.MStore8(program);
                            break;
                        case Instruction.MSIZE:
                            EvmProgramExecution.StorageMemory.MSize(program);
                            break;
                        case Instruction.MCOPY:
                            EvmProgramExecution.StorageMemory.MCopy(program);
                            break;
                        case Instruction.SLOAD:
                            await EvmProgramExecution.StorageMemory.SLoad(program);
                            break;
                        case Instruction.SSTORE:
                            await EvmProgramExecution.StorageMemory.SStore(program);
                            break;
                        case Instruction.TLOAD:
                            EvmProgramExecution.BlockchainCurrentContractContext.TLoad(program);
                            break;

                        case Instruction.TSTORE:
                            EvmProgramExecution.BlockchainCurrentContractContext.TStore(program);
                            break;
                        //--------------------//      
                        case Instruction.LOG0:
                        case Instruction.LOG1:
                        case Instruction.LOG2:
                        case Instruction.LOG3:
                        case Instruction.LOG4:
                            EvmProgramExecution.ReturnRevertLogExecution.Log(program);
                            break;
                        case Instruction.REVERT:
                            EvmProgramExecution.ReturnRevertLogExecution.Revert(program);
                            break;
                        case Instruction.RETURN:
                            EvmProgramExecution.ReturnRevertLogExecution.Return(program);
                            break;
                       
                        case Instruction.RETURNDATASIZE:
                            EvmProgramExecution.ReturnRevertLogExecution.ReturnDataSize(program);
                            break;
                        case Instruction.RETURNDATACOPY:
                            EvmProgramExecution.ReturnRevertLogExecution.ReturnDataCopy(program);
                            break;
                        //--------------------//      
                        case Instruction.DELEGATECALL:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.DelegateCallAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.CALL:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.CallAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.CALLCODE:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.CallCodeAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.STATICCALL:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.StaticCallAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.SELFDESTRUCT:
                            await EvmProgramExecution.CallingCreation.SelfDestructAsync(program);
                            break;
                        case Instruction.CREATE:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.CreateAsync(program, vmExecutionCounter, depth, traceEnabled);
                            break;
                        case Instruction.CREATE2:
                            innerTraceResult = await EvmProgramExecution.CallingCreation.Create2Async(program, vmExecutionCounter, depth, traceEnabled);
                            break;

                        case Instruction.INVALID:
                            program.GasRemaining = 0;
                            program.ProgramResult.IsRevert = true;
                            program.Stop();
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                
            }
            return innerTraceResult;

        }
#endif
    }
}