using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Opcodes;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM
{
    public partial class EVMSimulator
    {
        public static Action<int> ZiskTrace { get; set; }
#if DEBUG
        public bool EnableTraceToDebugOuptput { get; }
#endif
        public EvmProgramExecution EvmProgramExecution { get; }
        public HardforkConfig Config { get; }

        public EVMSimulator(HardforkConfig config, EvmProgramExecution evmProgramExecution = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            EvmProgramExecution = evmProgramExecution ?? new EvmProgramExecution();
        }

#if DEBUG
        public EVMSimulator(HardforkConfig config, EvmProgramExecution evmProgramExecution, bool enableTraceToDebugOuptput)
            : this(config, evmProgramExecution)
        {
            EnableTraceToDebugOuptput = enableTraceToDebugOuptput;
        }
#endif

#if EVM_SYNC
        public Program ExecuteWithCallStack(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true)
#else
        public async Task<Program> ExecuteWithCallStackAsync(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true)
#endif
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

#if EVM_SYNC
                ZiskTrace?.Invoke((int)currentInstruction.Value);
#endif

                long gasCost = 0;
                long gasBeforeOp = currentProgram.GasRemaining;

                try
                {
#if EVM_SYNC
                    gasCost = Config.OpcodeHandlers.GetGasCost(currentInstruction.Instruction.Value, currentProgram);
#else
                    gasCost = await Config.OpcodeHandlers.GetGasCostAsync(currentInstruction.Instruction.Value, currentProgram);
#endif
                    gasBeforeOp = currentProgram.GasRemaining;

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
                    if (currentInstruction.Instruction.Value == Instruction.SSTORE &&
                        currentProgram.ProgramContext.EnforceGasSentry &&
                        gasBeforeOp <= Gas.GasConstants.SSTORE_SENTRY)
                    {
#if EVM_SYNC
                        currentProgram.SetExecutionError(); continue;
#else
                        throw new Exceptions.SStoreSentryException(gasBeforeOp, Gas.GasConstants.SSTORE_SENTRY);
#endif
                    }

                    currentProgram.UpdateGasUsed(gasCost);
#if EVM_SYNC
                    if (currentProgram.HasExecutionError) continue;
#endif

#if EVM_SYNC
                    var subCallSetup = StepWithCallStack(currentFrame, currentInstruction, callStack);
                    if (currentProgram.HasExecutionError) continue;
#else
                    var subCallSetup = await StepWithCallStackAsync(currentFrame, currentInstruction, callStack);
#endif

                    // Geth includes gas forwarded in cost for CALL-type opcodes, but NOT for CREATE-type opcodes
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
            var gasToReturn = Math.Min(childProgram.GasRemaining, completedFrame.GasAllocated);

            if (!childProgram.ProgramResult.IsRevert)
            {
                var code = childProgram.ProgramResult.Result;

                if (Config.MaxCodeSize > 0 && code != null && code.Length > Config.MaxCodeSize)
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

                if (Config.RejectEfPrefix && code != null && code.Length > 0 && code[0] == 0xEF)
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
                long codeDepositCost = codeSize * GasConstants.CREATE_DATA_GAS;
                if (childProgram.GasRemaining < codeDepositCost)
                {
                    var depositResult = Config.CodeDepositRule.HandleCodeDepositOOG(
                        new Execution.Create.CodeDepositContext
                        {
                            Code = code,
                            GasRemaining = childProgram.GasRemaining,
                            CodeDepositCost = codeDepositCost
                        });
                    if (depositResult.Failed)
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
                    code = depositResult.FinalCode;
                    codeSize = code?.Length ?? 0;
                    codeDepositCost = depositResult.FinalCodeDepositCost;
                }

                if (completedFrame.SnapshotId.HasValue)
                {
                    parentProgram.ProgramContext.ExecutionStateService.CommitSnapshot(completedFrame.SnapshotId.Value);
                }

                childProgram.GasRemaining -= codeDepositCost;

                // Recalculate after code deposit deduction
                var gasToReturnAfterDeposit = Math.Min(childProgram.GasRemaining, completedFrame.GasAllocated);
                var gasActuallySpentOnCreate = completedFrame.GasAllocated - gasToReturnAfterDeposit;

                parentProgram.StackPush(AddressUtil.EncodeAddressTo32Bytes(completedFrame.NewContractAddress));
                parentProgram.ProgramContext.ExecutionStateService.SaveCode(completedFrame.NewContractAddress, code);
                parentProgram.ProgramResult.Logs.AddRange(childProgram.ProgramResult.Logs);
                parentProgram.ProgramResult.InnerCalls.Add(completedFrame.CallInput);
                parentProgram.ProgramResult.InnerCalls.AddRange(childProgram.ProgramResult.InnerCalls);
                parentProgram.ProgramResult.InnerCallResults.Add(new InnerCallResult
                {
                    CallInput = completedFrame.CallInput,
                    FrameType = (int)completedFrame.FrameType,
                    Depth = completedFrame.Depth,
                    GasUsed = gasActuallySpentOnCreate + codeDepositCost,
                    Output = code,
                    Success = true
                });
                parentProgram.ProgramResult.InnerCallResults.AddRange(childProgram.ProgramResult.InnerCallResults);
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
                parentProgram.ProgramResult.InnerCallResults.Add(new InnerCallResult
                {
                    CallInput = completedFrame.CallInput,
                    FrameType = (int)completedFrame.FrameType,
                    Depth = completedFrame.Depth,
                    GasUsed = gasActuallySpent,
                    Output = childProgram.ProgramResult.Result,
                    Success = false,
                    Error = childProgram.ProgramResult.IsRevert ? "execution reverted" : "out of gas",
#if EVM_SYNC
                    RevertReason = null
#else
                    RevertReason = childProgram.ProgramResult.IsRevert ? childProgram.ProgramResult.GetRevertMessage() : null
#endif
                });
                parentProgram.ProgramResult.InnerCallResults.AddRange(childProgram.ProgramResult.InnerCallResults);
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
            var gasActuallySpent = completedFrame.GasAllocated - Math.Min(gasToReturn, completedFrame.GasAllocated);


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
                parentProgram.ProgramResult.InnerCallResults.Add(new InnerCallResult
                {
                    CallInput = completedFrame.CallInput,
                    FrameType = (int)completedFrame.FrameType,
                    Depth = completedFrame.Depth,
                    GasUsed = gasActuallySpent,
                    Output = childProgram.ProgramResult.Result,
                    Success = true
                });
                parentProgram.ProgramResult.InnerCallResults.AddRange(childProgram.ProgramResult.InnerCallResults);
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

                parentProgram.ProgramResult.InnerCallResults.Add(new InnerCallResult
                {
                    CallInput = completedFrame.CallInput,
                    FrameType = (int)completedFrame.FrameType,
                    Depth = completedFrame.Depth,
                    GasUsed = gasActuallySpent,
                    Output = result,
                    Success = false,
                    Error = "execution reverted",
#if EVM_SYNC
                    RevertReason = null
#else
                    RevertReason = childProgram.ProgramResult.GetRevertMessage()
#endif
                });
                parentProgram.ProgramResult.InnerCallResults.AddRange(childProgram.ProgramResult.InnerCallResults);

                parentProgram.GasRemaining += gasToReturn;
                parentProgram.TotalGasUsed += gasActuallySpent;
                // Note: RefundCounter is NOT propagated on revert - refunds are lost with the reverted state
                parentProgram.Step();
            }
        }

#if EVM_SYNC
        private SubCallSetup StepWithCallStack(CallFrame currentFrame, ProgramInstruction instruction, Stack<CallFrame> callStack)
#else
        private async Task<SubCallSetup> StepWithCallStackAsync(CallFrame currentFrame, ProgramInstruction instruction, Stack<CallFrame> callStack)
#endif
        {
            var program = currentFrame.Program;
            if (program.Stopped) return null;
            if (instruction.Instruction == null) return null;

            var opcode = instruction.Instruction.Value;
            var gasCostTable = Config.OpcodeHandlers;

            // Gate fork-specific frame opcodes: if the gas cost table doesn't
            // register the opcode, treat it as invalid (fall through to executors).
            if (!gasCostTable.IsRegistered(opcode))
            {
                if (opcode == Instruction.CREATE2 || opcode == Instruction.DELEGATECALL || opcode == Instruction.STATICCALL)
                {
#if EVM_SYNC
                    program.SetExecutionError();
#else
                    throw new System.ArgumentOutOfRangeException($"Unknown instruction: {opcode}");
#endif
                    return null;
                }
            }

            switch (opcode)
            {
                case Instruction.CALL:
#if EVM_SYNC
                    return SetupCallFrame(program, currentFrame, CallFrameType.Call);
#else
                    return await SetupCallFrameAsync(program, currentFrame, CallFrameType.Call);
#endif
                case Instruction.DELEGATECALL:
#if EVM_SYNC
                    return SetupCallFrame(program, currentFrame, CallFrameType.DelegateCall);
#else
                    return await SetupCallFrameAsync(program, currentFrame, CallFrameType.DelegateCall);
#endif
                case Instruction.STATICCALL:
#if EVM_SYNC
                    return SetupCallFrame(program, currentFrame, CallFrameType.StaticCall);
#else
                    return await SetupCallFrameAsync(program, currentFrame, CallFrameType.StaticCall);
#endif
                case Instruction.CALLCODE:
#if EVM_SYNC
                    return SetupCallFrame(program, currentFrame, CallFrameType.CallCode);
#else
                    return await SetupCallFrameAsync(program, currentFrame, CallFrameType.CallCode);
#endif
                case Instruction.CREATE:
#if EVM_SYNC
                    return SetupCreateFrame(program, currentFrame, CallFrameType.Create);
#else
                    return await SetupCreateFrameAsync(program, currentFrame, CallFrameType.Create);
#endif
                case Instruction.CREATE2:
#if EVM_SYNC
                    return SetupCreateFrame(program, currentFrame, CallFrameType.Create2);
#else
                    return await SetupCreateFrameAsync(program, currentFrame, CallFrameType.Create2);
#endif
                default:
#if EVM_SYNC
                    bool handled = Config.OpcodeHandlers.Execute(opcode, program);
#else
                    bool handled = await Config.OpcodeHandlers.ExecuteAsync(opcode, program);
#endif
                    if (!handled)
                    {
#if EVM_SYNC
                        program.SetExecutionError();
#else
                        throw new System.ArgumentOutOfRangeException($"Unknown instruction: {opcode}");
#endif
                    }
                    return null;
            }
        }


    }
}
