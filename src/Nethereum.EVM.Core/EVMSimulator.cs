using Nethereum.Documentation;
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
        [NethereumDocExample(DocSection.EvmSimulator, "run-bytecode", "Drive a Program through the call stack - the EVM_SYNC arm")]
        public Program ExecuteWithCallStack(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true)
#else
        [NethereumDocExample(DocSection.EvmSimulator, "run-bytecode", "Drive a Program through the call stack - the async host arm")]
        public async Task<Program> ExecuteWithCallStackAsync(Program program, int vmExecutionCounter = 0, int depth = 0, bool traceEnabled = true)
#endif
        {
            if (StartsBeyondMaxCallDepth(depth))
            {
                program.Stop();
                return program;
            }

            var callStack = new Stack<CallFrame>();

            var initialFrame = BuildInitialFrame(program, vmExecutionCounter, depth, traceEnabled);
            ApplyEip8024DecodingWhereTheForkDefinesIt(program);

            callStack.Push(initialFrame);

            program.ProgramContext?.InitialiaseContractBalanceFromCallInputValue();

            while (callStack.Count > 0)
            {
                var currentFrame = callStack.Peek();
                var currentProgram = currentFrame.Program;

                if (currentProgram.Stopped)
                {
                    PopAndMergeCompletedFrame(callStack);
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
                    if (RefusedBeforePricing(currentProgram, currentInstruction.Instruction.Value)) continue;

#if EVM_SYNC
                    gasCost = Config.OpcodeHandlers.GetGasCost(currentInstruction.Instruction.Value, currentProgram);
                    if (currentProgram.HasExecutionError) continue;
#else
                    gasCost = await Config.OpcodeHandlers.GetGasCostAsync(currentInstruction.Instruction.Value, currentProgram);
#endif
                    gasBeforeOp = currentProgram.GasRemaining;

                    ProgramTrace trace = null;
                    if (currentFrame.TraceEnabled)
                    {
                        trace = CreateFrameTrace(currentFrame, currentInstruction);
                        trace.GasCost = gasCost;
                        trace.GasRemaining = gasBeforeOp;
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

                    AttributeForwardedGasToCallTrace(trace, subCallSetup, currentInstruction, gasCost);

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

                    if (SpawnsAChildFrame(subCallSetup))
                    {
                        PushChildFrame(callStack, subCallSetup.NewFrame);
                    }
                }
                catch (OutOfGasException ex)
                {
                    RecordHaltTrace(currentFrame, currentInstruction, ex.GasRequired, ex.GasRemaining, outOfGas: true);
                    HaltFrameExceptionally(currentProgram);
                }
                catch (Exceptions.SStoreGasStipendException)
                {
                    RecordHaltTrace(currentFrame, currentInstruction, 0, gasBeforeOp, outOfGas: true);
                    HaltFrameExceptionally(currentProgram);
                }
                catch (Exception ex) when (BlockchainState.EvmHostException.IsHostOrSystemFault(ex))
                {
                    throw;
                }
                catch (Exception)
                {
                    RecordHaltTrace(currentFrame, currentInstruction, gasCost, gasBeforeOp);
                    HaltFrameExceptionally(currentProgram);
                }
            }

            if (traceEnabled && program.StoppedImplicitly)
            {
                RecordImplicitStopTrace(program, vmExecutionCounter, depth);
            }

            return program;
        }

        private static bool StartsBeyondMaxCallDepth(int depth)
            => depth > GasConstants.MAX_CALL_DEPTH;

        private static CallFrame BuildInitialFrame(Program program, int vmExecutionCounter, int depth, bool traceEnabled)
            => new CallFrame
            {
                Program = program,
                VmExecutionCounter = vmExecutionCounter,
                ProgramExecutionCounter = 0,
                Depth = depth,
                TraceEnabled = traceEnabled,
                FrameType = CallFrameType.Initial
            };

        /// <summary>
        /// EIP-8024 (Amsterdam): the Program was decoded by its
        /// constructor WITHOUT DUPN/SWAPN/EXCHANGE immediate-byte
        /// absorption (see ProgramInstructionsUtils.GetProgramInstructions),
        /// so every pre-Amsterdam construction call site — including the
        /// ones in TransactionExecutor/BlockExecutor — stays byte-identical
        /// with no changes required there. Re-decode here, gated on the
        /// active fork's own opcode table, the single source of truth for
        /// "is DUPN defined at this fork" already used for gas/exec dispatch.
        /// </summary>
        private void ApplyEip8024DecodingWhereTheForkDefinesIt(Program program)
        {
            if (Config.OpcodeHandlers.IsRegistered(Instruction.DUPN))
            {
                program.ApplyEip8024Decoding();
            }
        }

        private void PopAndMergeCompletedFrame(Stack<CallFrame> callStack)
        {
            var completedFrame = callStack.Pop();

            if (callStack.Count > 0)
            {
                var parentFrame = callStack.Peek();
                MergeCompletedFrameIntoParent(completedFrame, parentFrame);
            }
        }

        private static ProgramTrace CreateFrameTrace(CallFrame frame, ProgramInstruction instruction)
        {
            var program = frame.Program;
            return ProgramTrace.CreateTraceFromCurrentProgram(
                program.ProgramContext.AddressContract,
                frame.VmExecutionCounter,
                frame.ProgramExecutionCounter,
                frame.Depth,
                program,
                instruction,
                program.ProgramContext.CodeAddress);
        }

        private static void RecordHaltTrace(CallFrame frame, ProgramInstruction instruction, long gasCost, long gasRemaining, bool outOfGas = false)
        {
            if (!frame.TraceEnabled) return;

            var trace = CreateFrameTrace(frame, instruction);
            trace.GasCost = gasCost;
            trace.GasRemaining = gasRemaining;
            trace.OutOfGas = outOfGas;
            frame.Program.Trace.Add(trace);
        }

        private static void HaltFrameExceptionally(Program program)
        {
            program.GasRemaining = 0;
            program.ProgramResult.IsRevert = true;
            program.MarkExceptionalHalt();
            program.Stop();
        }

        private static bool IsCallFamilyOpcode(Instruction opcode)
            => opcode == Instruction.CALL || opcode == Instruction.CALLCODE ||
               opcode == Instruction.DELEGATECALL || opcode == Instruction.STATICCALL;

        private static void AttributeForwardedGasToCallTrace(ProgramTrace trace, SubCallSetup subCallSetup, ProgramInstruction instruction, long gasCost)
        {
            if (trace != null && subCallSetup != null && (subCallSetup.ShouldCreateSubCall || subCallSetup.IsPrecompileHandled) && subCallSetup.GasForwarded > 0)
            {
                var opcode = instruction.Instruction.Value;
                if (IsCallFamilyOpcode(opcode))
                {
                    trace.GasCost = gasCost + subCallSetup.GasForwarded;
                }
            }
        }

        private static bool SpawnsAChildFrame(SubCallSetup subCallSetup)
            => subCallSetup != null && subCallSetup.ShouldCreateSubCall && subCallSetup.NewFrame != null;

        private void PushChildFrame(Stack<CallFrame> callStack, CallFrame childFrame)
        {
            ApplyEip8024DecodingWhereTheForkDefinesIt(childFrame.Program);
            callStack.Push(childFrame);
        }

        private static void RecordImplicitStopTrace(Program program, int vmExecutionCounter, int depth)
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

        private void MergeCompletedFrameIntoParent(CallFrame completedFrame, CallFrame parentFrame)
        {
            var childProgram = completedFrame.Program;
            var parentProgram = parentFrame.Program;

            if (childProgram.ProgramResult.IsRevert)
            {
                StateGasMeter.RestoreStateGas(childProgram);
            }

            if (IsCreateFrame(completedFrame))
            {
                MergeCreateResult(completedFrame, parentFrame);
            }
            else if (completedFrame.FrameType != CallFrameType.Initial)
            {
                MergeCallResult(completedFrame, parentFrame);
            }

            MergeChildTraceIntoParent(parentFrame, parentProgram, childProgram);
        }

        private static bool IsCreateFrame(CallFrame frame)
            => frame.FrameType == CallFrameType.Create || frame.FrameType == CallFrameType.Create2;

        private static void MergeChildTraceIntoParent(CallFrame parentFrame, Program parentProgram, Program childProgram)
        {
            parentFrame.VmExecutionCounter += childProgram.Trace.Count;
            parentProgram.Trace.AddRange(childProgram.Trace);
        }

        private static void AbortCompletedCreateFrame(CallFrame completedFrame, Program parentProgram, Program childProgram, bool restoreChildStateGas)
        {
            if (restoreChildStateGas)
            {
                StateGasMeter.RestoreStateGas(childProgram);
                StateGasMeter.AbsorbChild(parentProgram, childProgram);
                CreditNewAccountStateGasBack(parentProgram, completedFrame);
            }
            RevertFrameSnapshot(completedFrame, parentProgram);
            parentProgram.StackPush(0);
            parentProgram.ProgramResult.LastCallReturnData = null;
            parentProgram.TotalGasUsed += completedFrame.GasAllocated;
            parentProgram.Step();
        }

        private static void CommitFrameSnapshot(CallFrame completedFrame, Program parentProgram)
        {
            if (completedFrame.SnapshotId.HasValue)
            {
                parentProgram.ProgramContext.ExecutionStateService.CommitSnapshot(completedFrame.SnapshotId.Value);
            }
        }

        private static void RevertFrameSnapshot(CallFrame completedFrame, Program parentProgram)
        {
            if (completedFrame.SnapshotId.HasValue)
            {
                parentProgram.ProgramContext.ExecutionStateService.RevertToSnapshot(completedFrame.SnapshotId.Value);
            }
        }

        /// <summary>
        /// EIP-8037, Gas accounting for new accounts, CALL* family: "The STATE_BYTES_PER_NEW_ACCOUNT
        /// × CPSB account-creation charge is applied conditionally right before entering the child
        /// frame: only when the target account does not exist and a positive value is transferred.
        /// If the operation is unsuccessful before entering the call frame (e.g., due to insufficient
        /// balance or due to the stack depth), or if the child frame reverts or halts exceptionally,
        /// the charged state-gas is refilled in LIFO order and evm_state_gas_used decreases by the
        /// same amount."
        ///
        /// <para>The quoted sentence is scoped to the CALL* family, but two CREATE call sites reach
        /// this helper. The CREATE rule is stated separately at
        /// <c>EVMSimulator.SetupCreateFrame.cs</c>'s charge site; the credit-back behaviour is the
        /// same, the sentence authorising it is not.</para>
        /// </summary>
        private static void CreditNewAccountStateGasBack(Program parentProgram, CallFrame completedFrame)
        {
            if (completedFrame.NewAccountStateGasCharged)
            {
                StateGasMeter.CreditStateGasRefund(parentProgram, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
            }
        }

        private void MergeCreateResult(CallFrame completedFrame, CallFrame parentFrame)
        {
            var childProgram = completedFrame.Program;
            var parentProgram = parentFrame.Program;

            var gasToReturn = Math.Min(childProgram.GasRemaining, completedFrame.GasAllocated);

            if (!childProgram.ProgramResult.IsRevert)
            {
                var code = childProgram.ProgramResult.Result;

                if (ExceedsMaxCodeSize(code))
                {
                    AbortCompletedCreateFrame(completedFrame, parentProgram, childProgram, restoreChildStateGas: true);
                    return;
                }

                if (DeployedCodeHasReservedEfPrefix(code))
                {
                    AbortCompletedCreateFrame(completedFrame, parentProgram, childProgram, restoreChildStateGas: true);
                    return;
                }

                var isAmsterdamPlus = Config.IntrinsicGasRules.StateGasActive;
                var depositResult = Execution.Create.CodeDepositCharger.Charge(childProgram, code, Config, childProgram.GasRemaining);
                if (depositResult.Failed)
                {
                    AbortCompletedCreateFrame(completedFrame, parentProgram, childProgram, restoreChildStateGas: isAmsterdamPlus);
                    return;
                }
                code = depositResult.FinalCode;
                var codeSize = code?.Length ?? 0;
                var codeDepositCost = depositResult.FinalCodeDepositCost;
                var depositAlreadyCharged = isAmsterdamPlus;

                StateGasMeter.AbsorbChild(parentProgram, childProgram);
                StateGasMeter.ReturnOutstandingStateGasToGasLeft(parentProgram);

                CommitFrameSnapshot(completedFrame, parentProgram);

                if (!depositAlreadyCharged)
                {
                    childProgram.GasRemaining -= codeDepositCost;
                }

                var gasToReturnAfterDeposit = Math.Min(childProgram.GasRemaining, completedFrame.GasAllocated);
                var gasActuallySpentOnCreate = completedFrame.GasAllocated - gasToReturnAfterDeposit;

                PublishDeployedCode(completedFrame, parentProgram, code);
                AbsorbSuccessfulCreateIntoParent(completedFrame, parentProgram, childProgram, code, gasActuallySpentOnCreate, codeDepositCost);
                SettleCreateGasInParent(parentProgram, childProgram, gasToReturnAfterDeposit, gasActuallySpentOnCreate);
            }
            else
            {
                ReportCreateRevertToCaller(completedFrame, parentProgram, childProgram, gasToReturn);
            }
        }

        /// <summary>
        /// EIP-170, Specification: "If block.number &gt;= FORK_BLKNUM, then if contract creation
        /// initialization returns data with length of more than MAX_CODE_SIZE bytes, contract
        /// creation fails with an out of gas error." MAX_CODE_SIZE is 0x6000, raised at Amsterdam by
        /// EIP-7954.
        ///
        /// <para><c>Config.MaxCodeSize</c> is 0 at every pre-Spurious-Dragon fork, which is how those
        /// forks answer — the same fork-gating shape as <see cref="ExceedsMaxInitcodeSize"/>. "Fails
        /// with an out of gas error" is why the abort bills the whole allocation rather than
        /// returning it.</para>
        /// </summary>
        private bool ExceedsMaxCodeSize(byte[] code)
            => Config.MaxCodeSize > 0 && code != null && code.Length > Config.MaxCodeSize;

        /// <summary>
        /// EIP-3541, Specification: "After block.number == HF_BLOCK new contract creation (via create
        /// transaction, CREATE or CREATE2 instructions) results in an exceptional abort if the code's
        /// first byte is 0xEF."
        /// </summary>
        private bool DeployedCodeHasReservedEfPrefix(byte[] code)
            => Config.RejectEfPrefix && code != null && code.Length > 0 && code[0] == 0xEF;

        private static void PublishDeployedCode(CallFrame completedFrame, Program parentProgram, byte[] code)
        {
            parentProgram.StackPush(AddressUtil.EncodeAddressTo32Bytes(completedFrame.NewContractAddress));
            parentProgram.ProgramContext.ExecutionStateService.SaveCode(completedFrame.NewContractAddress, code);
        }

        private static void AbsorbSuccessfulCreateIntoParent(CallFrame completedFrame, Program parentProgram, Program childProgram, byte[] code, long gasActuallySpentOnCreate, long codeDepositCost)
        {
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
            parentProgram.ProgramResult.ClearedContractAccounts.AddRange(childProgram.ProgramResult.ClearedContractAccounts);
            parentProgram.ProgramResult.LastCallReturnData = null;
        }

        private static void SettleCreateGasInParent(Program parentProgram, Program childProgram, long gasToReturnAfterDeposit, long gasActuallySpentOnCreate)
        {
            parentProgram.GasRemaining += gasToReturnAfterDeposit;
            parentProgram.TotalGasUsed += gasActuallySpentOnCreate;
            parentProgram.RefundCounter += childProgram.RefundCounter;
            parentProgram.Step();
        }

        private static void ReportCreateRevertToCaller(CallFrame completedFrame, Program parentProgram, Program childProgram, long gasToReturn)
        {
            StateGasMeter.AbsorbChild(parentProgram, childProgram);
            CreditNewAccountStateGasBack(parentProgram, completedFrame);
            RevertFrameSnapshot(completedFrame, parentProgram);
            parentProgram.StackPush(0);
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
            parentProgram.Step();
        }

        private void MergeCallResult(CallFrame completedFrame, CallFrame parentFrame)
        {
            var childProgram = completedFrame.Program;
            var parentProgram = parentFrame.Program;

            var gasToReturn = childProgram.GasRemaining;
            var gasActuallySpent = completedFrame.GasAllocated - Math.Min(gasToReturn, completedFrame.GasAllocated);

            RefundUnusedCallStipendToParent(parentProgram, completedFrame, gasToReturn);

            StateGasMeter.AbsorbChild(parentProgram, childProgram);

            if (!childProgram.ProgramResult.IsRevert)
            {
                StateGasMeter.ReturnOutstandingStateGasToGasLeft(parentProgram);
                CommitFrameSnapshot(completedFrame, parentProgram);

                parentProgram.StackPush(1);
                var result = childProgram.ProgramResult.Result;
                parentProgram.ProgramResult.LastCallReturnData = result;

                WriteCallReturnDataToParentMemory(completedFrame, parentProgram, result);
                AbsorbSuccessfulCallIntoParent(completedFrame, parentProgram, childProgram, gasActuallySpent);
                CopyInnerContractCodeWhenTracing(completedFrame, parentProgram, childProgram);
                SettleCallGasInParent(parentProgram, childProgram, gasToReturn, gasActuallySpent);
            }
            else
            {
                ReportCallRevertToCaller(completedFrame, parentProgram, childProgram, gasToReturn, gasActuallySpent);
            }
        }

        private static void RefundUnusedCallStipendToParent(Program parentProgram, CallFrame completedFrame, long gasToReturn)
        {
            var excessReturn = gasToReturn - completedFrame.GasAllocated;
            if (excessReturn > 0)
            {
                parentProgram.TotalGasUsed -= excessReturn;
            }
        }

        private static void WriteCallReturnDataToParentMemory(CallFrame completedFrame, Program parentProgram, byte[] result)
        {
            if (result != null && result.Length > 0)
            {
                var resultLength = Math.Min(completedFrame.ResultMemoryDataLength, result.Length);
                parentProgram.WriteToMemory(completedFrame.ResultMemoryDataIndex, resultLength, result);
            }
        }

        private static void AbsorbSuccessfulCallIntoParent(CallFrame completedFrame, Program parentProgram, Program childProgram, long gasActuallySpent)
        {
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
            parentProgram.ProgramResult.ClearedContractAccounts.AddRange(childProgram.ProgramResult.ClearedContractAccounts);
        }

        private static void CopyInnerContractCodeWhenTracing(CallFrame completedFrame, Program parentProgram, Program childProgram)
        {
            if (completedFrame.TraceEnabled)
            {
                foreach (var codeItem in childProgram.ProgramResult.InnerContractCodeCalls)
                {
                    parentProgram.ProgramResult.InsertInnerContractCodeIfDoesNotExist(codeItem.Key, codeItem.Value);
                }
            }
        }

        private static void SettleCallGasInParent(Program parentProgram, Program childProgram, long gasToReturn, long gasActuallySpent)
        {
            parentProgram.GasRemaining += gasToReturn;
            parentProgram.TotalGasUsed += gasActuallySpent;
            parentProgram.RefundCounter += childProgram.RefundCounter;
            parentProgram.Step();
        }

        private static void ReportCallRevertToCaller(CallFrame completedFrame, Program parentProgram, Program childProgram, long gasToReturn, long gasActuallySpent)
        {
            RevertFrameSnapshot(completedFrame, parentProgram);

            CreditNewAccountStateGasBack(parentProgram, completedFrame);

            parentProgram.StackPush(0);
            var result = childProgram.ProgramResult.Result;
            parentProgram.ProgramResult.LastCallReturnData = result;

            WriteCallReturnDataToParentMemory(completedFrame, parentProgram, result);

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
            parentProgram.Step();
        }

        /// <summary>
        /// EIP-7928, Gas Validation Before State Access: "Pre-state validation MUST pass
        /// before any state access occurs. If pre-state validation fails, the target
        /// resource (address or storage slot) is never accessed and MUST NOT be included
        /// in the BAL." Both refusals below therefore answer before the opcode is priced,
        /// because pricing SSTORE and SELFDESTRUCT reads state.
        ///
        /// <para>EIP-7928: "SSTORE performs an implicit read of the current storage value
        /// for gas calculation. Before this read, the frame MUST cover the storage slot's
        /// <c>access_cost</c> and MUST also satisfy the <c>GAS_CALL_STIPEND</c> sentry
        /// (<c>gas_left &gt; GAS_CALL_STIPEND</c>); because the access cost can exceed
        /// <c>GAS_CALL_STIPEND</c> after repricing, the stipend sentry alone is no longer
        /// sufficient." Only the stipend is checked here: COLD_STORAGE_ACCESS is 2,100 at
        /// every fork this engine runs, so it cannot exceed the 2,300 stipend. A fork that
        /// reprices it above 2,300 must check <c>access_cost</c> here too.</para>
        /// </summary>
        private static bool GasIsBelowTheSstoreStipend(Program program) =>
            program.ProgramContext.EnforceSstoreGasStipend
            && program.GasRemaining <= Gas.GasConstants.SSTORE_GAS_STIPEND;

        private static bool RefusedBeforePricing(Program program, Instruction opcode)
        {
            if (Execution.StaticContextWriteProtection.Forbids(program, opcode))
            {
#if EVM_SYNC
                program.SetExecutionError(); return true;
#else
                throw new Exceptions.StaticCallViolationException(opcode.ToString());
#endif
            }

            if (opcode == Instruction.SSTORE && GasIsBelowTheSstoreStipend(program))
            {
#if EVM_SYNC
                program.SetExecutionError(); return true;
#else
                throw new Exceptions.SStoreGasStipendException(program.GasRemaining, Gas.GasConstants.SSTORE_GAS_STIPEND);
#endif
            }

            return false;
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
