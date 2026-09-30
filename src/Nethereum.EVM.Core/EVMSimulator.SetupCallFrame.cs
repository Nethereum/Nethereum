using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;
using System.Collections.Generic;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM
{
    public partial class EVMSimulator
    {
#if EVM_SYNC
        private SubCallSetup SetupCallFrame(Program program, CallFrame parentFrame, CallFrameType callType)
#else
        private async Task<SubCallSetup> SetupCallFrameAsync(Program program, CallFrame parentFrame, CallFrameType callType)
#endif
        {
            var newAccountStateGasCharged = ReadAndResetNewAccountStateGasHandoff(program);

            var gasU256 = program.StackPopU256();
            var gas = gasU256.ToLongSafe();
            var codeAddress = program.StackPop();
            EvmUInt256 value = EvmUInt256.Zero;
            string from;
            string to;
            bool isStatic = false;

            PopCallParticipants(program, callType, codeAddress, out from, out to, out value, out isStatic);

            var dataInputIndexBig = program.StackPopU256();
            var dataInputLengthBig = program.StackPopU256();
            var resultMemoryDataIndexBig = program.StackPopU256();
            var resultMemoryDataLengthBig = program.StackPopU256();

            if (InputRegionOverflows(dataInputIndexBig, dataInputLengthBig))
            {
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            var dataInputIndex = !dataInputIndexBig.FitsInInt ? int.MaxValue : dataInputIndexBig.ToInt();
            var dataInputLength = dataInputLengthBig.ToInt();
            var resultMemoryDataIndex = resultMemoryDataIndexBig.FitsInInt ? resultMemoryDataIndexBig.ToInt() : int.MaxValue;
            var resultMemoryDataLength = resultMemoryDataLengthBig.FitsInInt ? resultMemoryDataLengthBig.ToInt() : int.MaxValue;

            var codeAddressValue = EvmAddress.From(codeAddress);
            var codeAddressNormalized = codeAddressValue.ToHexLower();
            program.ProgramContext.RecordAddressAccess(codeAddressNormalized);

            var registry = Config.Precompiles;
            int precompileAddress = -1;
            bool isKnownPrecompile = Execution.Precompiles.PrecompileRelocationResolver.TryResolve(
                program.ProgramContext.PrecompileRelocations, registry, codeAddressNormalized, out precompileAddress);

#if EVM_SYNC
            var byteCode = ResolveTargetCode(program, callType, codeAddressNormalized, codeAddressValue, isKnownPrecompile);
#else
            var byteCode = await ResolveTargetCodeAsync(program, callType, codeAddressNormalized, codeAddressValue, isKnownPrecompile);
#endif

            var maxAllowedGasFromRules = Config.GasForwarding.CalculateMaxGasToForward(program.GasRemaining);

            var gasToAllocate = Config.GasForwarding.CalculateGasForCall(program.GasRemaining, gas);
            if (gasToAllocate < 0) gasToAllocate = 0;
            if (ForwardEverythingExceedsRemaining(gasToAllocate, program))
            {
                return ForfeitFrameOnForwardEverythingOverspend(program);
            }

            if (ExceedsMaxCallDepth(parentFrame))
            {
                return AbortCallBeforeChildFrame(program, callType, value, newAccountStateGasCharged);
            }

            ExpandMemoryForCallRegions(program, dataInputIndex, dataInputLength, resultMemoryDataIndex, resultMemoryDataLength);

            var dataInput = ReadCallInputFromMemory(program, dataInputIndex, dataInputLength);

            var callInput = new EvmCallContext
            {
                From = from,
                Value = value,
                To = to,
                Data = dataInput,
                Gas = gas,
                ChainId = program.ProgramContext.ChainId
            };

            var shouldTransferValue = TransfersValue(callType);

#if EVM_SYNC
            var unfundedValueTransfer = RejectUnfundedValueTransfer(program, callType, value, shouldTransferValue, newAccountStateGasCharged);
#else
            var unfundedValueTransfer = await RejectUnfundedValueTransferAsync(program, callType, value, shouldTransferValue, newAccountStateGasCharged);
#endif
            if (unfundedValueTransfer != null) return unfundedValueTransfer;

            var snapshotId = program.ProgramContext.ExecutionStateService.TakeSnapshot();

#if EVM_SYNC
            ReadAndDebitThePayer(program, shouldTransferValue, value);
#else
            await ReadAndDebitThePayerAsync(program, shouldTransferValue, value);
#endif

            if (TargetHasNoCode(byteCode))
            {
                CreditCodelessTargetBalance(program, shouldTransferValue, to, value);

                var gasToForwardForTrace = CapGasForwardedToCodelessTarget(gas, maxAllowedGasFromRules);

                if (isKnownPrecompile)
                {
#if EVM_SYNC
                    var precompileFailure = DispatchPrecompileCall(
                        program, registry, precompileAddress, callType, codeAddressValue, dataInput,
                        gasToForwardForTrace, resultMemoryDataIndex, resultMemoryDataLength, snapshotId, value,
                        newAccountStateGasCharged);
#else
                    var precompileFailure = await DispatchPrecompileCallAsync(
                        program, registry, precompileAddress, callType, codeAddressValue, dataInput,
                        gasToForwardForTrace, resultMemoryDataIndex, resultMemoryDataLength, snapshotId, value,
                        newAccountStateGasCharged);
#endif
                    if (precompileFailure != null) return precompileFailure;
                }
                else
                {
                    SettleNonPrecompileCallTarget(program, snapshotId, shouldTransferValue, value);
                }

                EmitCodelessCallTransferLog(program, shouldTransferValue, from, to, value);

                return SucceedCodelessCall(program, gasToForwardForTrace);
            }

            var programContext = BuildChildProgramContext(
                program, parentFrame, callInput, codeAddressNormalized, isStatic);

            var callProgram = new Program(byteCode, programContext);

            GrantGasBudgetsToCallChild(program, callProgram, gasToAllocate, shouldTransferValue, value);

            TransferValueIntoChildFrame(program, callProgram, shouldTransferValue, from, to, value);

            RecordChildCodeForTrace(program, parentFrame, codeAddressNormalized, callProgram);

            var newFrame = BuildChildCallFrame(
                parentFrame, callProgram, callType, callInput, value, gasToAllocate, snapshotId,
                resultMemoryDataIndex, resultMemoryDataLength, newAccountStateGasCharged);

            return new SubCallSetup { ShouldCreateSubCall = true, NewFrame = newFrame, GasForwarded = gasToAllocate };
        }

        private static bool ReadAndResetNewAccountStateGasHandoff(Program program)
        {
            var newAccountStateGasCharged = program.CallNewAccountStateGasCharged;
            program.CallNewAccountStateGasCharged = false;
            return newAccountStateGasCharged;
        }

#if EVM_SYNC
        private byte[] ResolveTargetCode(
            Program program, CallFrameType callType, string codeAddressNormalized, EvmAddress codeAddressValue, bool isKnownPrecompile)
        {
#else
        private async Task<byte[]> ResolveTargetCodeAsync(
            Program program, CallFrameType callType, string codeAddressNormalized, EvmAddress codeAddressValue, bool isKnownPrecompile)
        {
#endif
            byte[] byteCode;
            if (isKnownPrecompile)
            {
                byteCode = null;
            }
            else if (callType == CallFrameType.Call)
            {
#if EVM_SYNC
                byteCode = ReadCallTargetAccount(program, codeAddressValue).Code;
#else
                byteCode = (await ReadCallTargetAccountAsync(program, codeAddressValue)).Code;
#endif
            }
            else
            {
#if EVM_SYNC
                byteCode = program.ProgramContext.ExecutionStateService.GetCodeReadOnly(codeAddressValue);
#else
                byteCode = await program.ProgramContext.ExecutionStateService.GetCodeReadOnlyAsync(codeAddressValue);
#endif
            }

            var callFrameContext = new Execution.CallFrame.CallFrameSetupContext
            {
                Program = program,
                CodeAddress = codeAddressNormalized,
                ByteCode = byteCode,
                CallType = callType,
                ExecutionState = program.ProgramContext.ExecutionStateService
            };

            var callFrameRules = Config.CallFrameInitRules;
            if (callFrameRules != null)
            {
#if EVM_SYNC
                callFrameRules.Apply(callFrameContext);
#else
                await callFrameRules.ApplyAsync(callFrameContext);
#endif
                byteCode = callFrameContext.ByteCode;
            }
            return byteCode;
        }

        private static void ExpandMemoryForCallRegions(
            Program program, int dataInputIndex, int dataInputLength,
            int resultMemoryDataIndex, int resultMemoryDataLength)
        {
            var inputEnd = dataInputLength > 0 ? dataInputIndex + dataInputLength : 0;
            var outputEnd = resultMemoryDataLength > 0 && resultMemoryDataIndex < int.MaxValue ? resultMemoryDataIndex + resultMemoryDataLength : 0;
            var requiredMemorySize = Math.Max(inputEnd, outputEnd);
            if (requiredMemorySize > program.Memory.Count)
            {
                program.ExpandMemory(requiredMemorySize);
            }
        }

#if EVM_SYNC
        private static SubCallSetup RejectUnfundedValueTransfer(
            Program program, CallFrameType callType, EvmUInt256 value,
            bool shouldTransferValue, bool newAccountStateGasCharged)
        {
#else
        private static async Task<SubCallSetup> RejectUnfundedValueTransferAsync(
            Program program, CallFrameType callType, EvmUInt256 value,
            bool shouldTransferValue, bool newAccountStateGasCharged)
        {
#endif
            if (shouldTransferValue && value > 0)
            {
#if EVM_SYNC
                var callerBalance = program.ProgramContext.ExecutionStateService.GetTotalBalance(program.ProgramContext.AddressContract);
#else
                var callerBalance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(program.ProgramContext.AddressContract);
#endif
                if (callerBalance < value)
                {
                    return AbortCallBeforeChildFrame(program, callType, value, newAccountStateGasCharged);
                }
            }
            return null;
        }

#if EVM_SYNC
        private static void ReadAndDebitThePayer(Program program, bool shouldTransferValue, EvmUInt256 value)
        {
#else
        private static async Task ReadAndDebitThePayerAsync(Program program, bool shouldTransferValue, EvmUInt256 value)
        {
#endif
            if (shouldTransferValue)
            {
#if EVM_SYNC
                ReadThePayerAccount(program);
#else
                await ReadThePayerAccountAsync(program);
#endif
                program.ProgramContext.ExecutionStateService.DebitBalance(program.ProgramContext.AddressContract, value);
            }
        }

        private static void GrantGasBudgetsToCallChild(
            Program program, Program callProgram, long gasToAllocate,
            bool shouldTransferValue, EvmUInt256 value)
        {
            if (shouldTransferValue && value > 0)
            {
                callProgram.GasRemaining = gasToAllocate + GasConstants.CALL_STIPEND;
            }
            else
            {
                callProgram.GasRemaining = gasToAllocate;
            }
            program.GasRemaining -= gasToAllocate;

            callProgram.StateGasLeft = StateGasMeter.DrainReservoir(program);
            callProgram.StateGasBaseline = callProgram.StateGasLeft;
        }

        private static void TransferValueIntoChildFrame(
            Program program, Program callProgram, bool shouldTransferValue,
            string from, string to, EvmUInt256 value)
        {
            if (shouldTransferValue)
            {
                program.ProgramContext.ExecutionStateService.CreditBalance(to, value);
            }

            if (shouldTransferValue)
            {
                program.ProgramContext.EthTransferLogRule.Emit(callProgram.ProgramResult.Logs, from, to, value);
            }
        }

        private static void RecordChildCodeForTrace(
            Program program, CallFrame parentFrame, string codeAddressNormalized, Program callProgram)
        {
            if (parentFrame.TraceEnabled)
            {
                program.ProgramResult.InsertInnerContractCodeIfDoesNotExist(codeAddressNormalized, callProgram.Instructions);
            }
        }

        private static CallFrame BuildChildCallFrame(
            CallFrame parentFrame, Program callProgram, CallFrameType callType, EvmCallContext callInput,
            EvmUInt256 value, long gasToAllocate, int snapshotId,
            int resultMemoryDataIndex, int resultMemoryDataLength, bool newAccountStateGasCharged)
        {
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
                SnapshotId = snapshotId,
                NewAccountStateGasCharged = newAccountStateGasCharged
            };
            return newFrame;
        }

        private static void CreditCodelessTargetBalance(
            Program program, bool shouldTransferValue, string to, EvmUInt256 value)
        {
            if (!shouldTransferValue) return;
            program.ProgramContext.ExecutionStateService.CreditBalance(to, value);
        }

        private static long CapGasForwardedToCodelessTarget(long gas, long maxAllowedGasFromRules)
        {
            var gasToForwardForTrace = gas > maxAllowedGasFromRules ? maxAllowedGasFromRules : gas;
            if (gasToForwardForTrace < 0) gasToForwardForTrace = 0;
            return gasToForwardForTrace;
        }

        private static void EmitCodelessCallTransferLog(
            Program program, bool shouldTransferValue, string from, string to, EvmUInt256 value)
        {
            if (!shouldTransferValue) return;
            program.ProgramContext.EthTransferLogRule.Emit(program.ProgramResult.Logs, from, to, value);
        }

#if EVM_SYNC
        private static AccountExecutionState ReadCallTargetAccount(Program program, EvmAddress codeAddressValue)
        {
            var target = program.ProgramContext.ExecutionStateService
                .LoadBalanceNonceAndCodeFromStorage(codeAddressValue);
#else
        private static async Task<AccountExecutionState> ReadCallTargetAccountAsync(Program program, EvmAddress codeAddressValue)
        {
            var target = await program.ProgramContext.ExecutionStateService
                .LoadBalanceNonceAndCodeFromStorageAsync(codeAddressValue);
#endif
            target.WasMaterialisedByCallFrame = true;
            return target;
        }

#if EVM_SYNC
        private static void ReadThePayerAccount(Program program)
        {
            program.ProgramContext.ExecutionStateService
                .LoadBalanceNonceAndCodeFromStorage(program.ProgramContext.AddressContract);
        }
#else
        private static Task<AccountExecutionState> ReadThePayerAccountAsync(Program program)
        {
            return program.ProgramContext.ExecutionStateService
                .LoadBalanceNonceAndCodeFromStorageAsync(program.ProgramContext.AddressContract);
        }
#endif

        private const int RipemdPrecompileAddress = 3;

        private static bool TryParsePrecompileAddress(string normalizedAddress, out int addressInt)
        {
            addressInt = -1;
            if (string.IsNullOrEmpty(normalizedAddress)) return false;
            var compact = normalizedAddress.ToHexCompact();
            return int.TryParse(compact, System.Globalization.NumberStyles.HexNumber, null, out addressInt);
        }

        private static string NormalizeCallTargetAddress(byte[] codeAddress)
        {
            return NormalizeCallTargetAddressValue(codeAddress).ToHexLower();
        }

        private static EvmAddress NormalizeCallTargetAddressValue(byte[] codeAddress)
        {
            return EvmAddress.From(codeAddress);
        }

        private static void PopCallParticipants(
            Program program, CallFrameType callType, byte[] codeAddress,
            out string from, out string to, out EvmUInt256 value, out bool isStatic)
        {
            value = EvmUInt256.Zero;
            isStatic = false;
            switch (callType)
            {
                case CallFrameType.StaticCall:
                    from = program.ProgramContext.AddressContract;
                    to = NormalizeCallTargetAddress(codeAddress);
                    isStatic = true;
                    break;
                case CallFrameType.DelegateCall:
                    from = program.ProgramContext.AddressCaller;
                    to = program.ProgramContext.AddressContract;
                    value = program.ProgramContext.Value;
                    break;
                case CallFrameType.CallCode:
                    value = program.StackPopU256();
                    from = program.ProgramContext.AddressContract;
                    to = program.ProgramContext.AddressContract;
                    break;
                default:
                    value = program.StackPopU256();
                    from = program.ProgramContext.AddressContract;
                    to = NormalizeCallTargetAddress(codeAddress);
                    break;
            }
        }

        private static byte[] ReadCallInputFromMemory(Program program, int dataInputIndex, int dataInputLength)
        {
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
            return dataInput;
        }


        /// <summary>
        /// Builds the child frame's <see cref="ProgramContext"/> — everything the child inherits
        /// from its parent.
        ///
        /// <para>It is inert: no gas accounting, no state read or write, no early return. Its
        /// whole content is "copy what the child inherits from its parent", and the reason it
        /// is worth a name is that FORGETTING one of these is a live defect class — the
        /// EIP-4844 note records BLOBHASH returning zero inside every inner frame until the
        /// blob context was propagated, first seen at mainnet block 20,000,000 tx[21].</para>
        /// </summary>
        private static ProgramContext BuildChildProgramContext(
            Program program, CallFrame parentFrame, EvmCallContext callInput,
            string codeAddressNormalized, bool isStatic)
        {
                var programContext = new ProgramContext(
                    callInput,
                    program.ProgramContext.ExecutionStateService,
                    program.ProgramContext.AddressOrigin,
                    codeAddress: codeAddressNormalized,
                    blockNumber: program.ProgramContext.BlockNumber,
                    timestamp: program.ProgramContext.Timestamp,
                    coinbase: program.ProgramContext.Coinbase,
                    baseFee: program.ProgramContext.BaseFee,
                    preserveZeroBaseFee: program.ProgramContext.PreserveZeroBaseFee);
                programContext.Difficulty = program.ProgramContext.Difficulty;
                programContext.GasLimit = program.ProgramContext.GasLimit;
                programContext.GasPrice = program.ProgramContext.GasPrice;
                programContext.SlotNumber = program.ProgramContext.SlotNumber;
                programContext.Depth = parentFrame.Depth + 1;
                programContext.IsStatic = isStatic || program.ProgramContext.IsStatic;
                programContext.EnforceSstoreGasStipend = program.ProgramContext.EnforceSstoreGasStipend;
                programContext.SstoreClearsSchedule = program.ProgramContext.SstoreClearsSchedule;
                programContext.SstoreSetRefund = program.ProgramContext.SstoreSetRefund;
                programContext.SstoreResetRefund = program.ProgramContext.SstoreResetRefund;
                programContext.SstoreRefundRule = program.ProgramContext.SstoreRefundRule;
                programContext.EthTransferLogRule = program.ProgramContext.EthTransferLogRule;
                programContext.BlockHashRule = program.ProgramContext.BlockHashRule;
                programContext.PreserveZeroBaseFee = program.ProgramContext.PreserveZeroBaseFee;
                programContext.PrecompileRelocations = program.ProgramContext.PrecompileRelocations;
                programContext.StateGasActive = program.ProgramContext.StateGasActive;
                programContext.TransientStorage = program.ProgramContext.TransientStorage;
                programContext.SetAccessListTracker(program.ProgramContext.AccessListTracker);
                programContext.BlobHashes = program.ProgramContext.BlobHashes;
                programContext.BlobBaseFee = program.ProgramContext.BlobBaseFee;

                return programContext;
        }


        private static bool InputRegionOverflows(EvmUInt256 dataInputIndexBig, EvmUInt256 dataInputLengthBig)
            => (!dataInputLengthBig.IsZero && !dataInputIndexBig.FitsInInt) || !dataInputLengthBig.FitsInInt;

        private static bool ExceedsMaxCallDepth(CallFrame parentFrame)
            => parentFrame.Depth + 1 > GasConstants.MAX_CALL_DEPTH;

        private static bool TransfersValue(CallFrameType callType)
            => callType != CallFrameType.DelegateCall && callType != CallFrameType.StaticCall;

        private static bool TargetHasNoCode(byte[] byteCode)
            => byteCode == null || byteCode.Length == 0;


        /// <summary>
        /// Marks a called precompile as touched, which is what decides whether EIP-161's
        /// end-of-transaction sweep deletes it.
        ///
        /// <para>The canonical rules touch the target on CALL and STATICCALL only: CALL adds
        /// balance to the target even when value == 0, and STATICCALL does an explicit
        /// AddBalance(addr, 0) "just for the sake of triggering a touch". CALLCODE and
        /// DELEGATECALL do not touch the target.</para>
        ///
        /// <para>RIPEMD-160 (0x…003) is the special case: the touch marks ripemd dirty OUTSIDE
        /// the journaled touchChange — the dirty mark survives snapshot revert while the
        /// touchChange itself can be rolled back. So when a sub-call that touched RIPEMD
        /// reverts, the other 7 precompile touches are journal-rolled back but RIPEMD stays
        /// dirty and is deleted by the EIP-161 sweep. RevertPrecompiledTouch.json d0/d3
        /// canonical hashes encode exactly that asymmetry.</para>
        /// </summary>
#if EVM_SYNC
        private static void MaterialiseAndTouchPrecompileTarget(
            Program program, CallFrameType callType, EvmAddress codeAddressValue, int precompileAddress)
        {
            var precompileAccount = program.ProgramContext.ExecutionStateService
                .LoadBalanceNonceAndCodeFromStorage(codeAddressValue);
#else
        private static async Task MaterialiseAndTouchPrecompileTargetAsync(
            Program program, CallFrameType callType, EvmAddress codeAddressValue, int precompileAddress)
        {
            var precompileAccount = await program.ProgramContext.ExecutionStateService
                .LoadBalanceNonceAndCodeFromStorageAsync(codeAddressValue);
#endif
            if (!TouchesTheTarget(callType)) return;

            precompileAccount.IsTouched = true;
            if (precompileAddress == RipemdPrecompileAddress)
            {
                program.ProgramContext.ExecutionStateService.MarkTxGloballyTouched(codeAddressValue);
            }
        }

        private static bool TouchesTheTarget(CallFrameType callType)
            => callType == CallFrameType.Call || callType == CallFrameType.StaticCall;


#if EVM_SYNC
        private static SubCallSetup DispatchPrecompileCall(
            Program program, Execution.Precompiles.PrecompileRegistry registry, int precompileAddress,
            CallFrameType callType, EvmAddress codeAddressValue, byte[] dataInput,
            long gasToForwardForTrace, int resultMemoryDataIndex, int resultMemoryDataLength,
            int snapshotId, EvmUInt256 value, bool newAccountStateGasCharged)
        {
            MaterialiseAndTouchPrecompileTarget(program, callType, codeAddressValue, precompileAddress);
#else
        private static async Task<SubCallSetup> DispatchPrecompileCallAsync(
            Program program, Execution.Precompiles.PrecompileRegistry registry, int precompileAddress,
            CallFrameType callType, EvmAddress codeAddressValue, byte[] dataInput,
            long gasToForwardForTrace, int resultMemoryDataIndex, int resultMemoryDataLength,
            int snapshotId, EvmUInt256 value, bool newAccountStateGasCharged)
        {
            await MaterialiseAndTouchPrecompileTargetAsync(program, callType, codeAddressValue, precompileAddress);
#endif
            var shouldTransferValue = TransfersValue(callType);

            long precompileGasCost = registry.GetGasCost(precompileAddress, dataInput);

            if (gasToForwardForTrace < precompileGasCost)
            {
                program.GasRemaining -= gasToForwardForTrace;
                program.TotalGasUsed += gasToForwardForTrace;
                return FailPrecompileCall(program, snapshotId, newAccountStateGasCharged, gasToForwardForTrace);
            }

            program.GasRemaining -= precompileGasCost;
            program.TotalGasUsed += precompileGasCost;

            try
            {
                byte[] precompiledResult = registry.Execute(precompileAddress, dataInput);
                var resultLength = Math.Min(resultMemoryDataLength, precompiledResult?.Length ?? 0);
                program.WriteToMemory(resultMemoryDataIndex, resultLength, precompiledResult);
                program.ProgramResult.LastCallReturnData = precompiledResult;
                program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                if (shouldTransferValue && value > 0)
                {
                    RestoreValueTransferStipend(program);
                }
            }
            catch (Exception ex) when (BlockchainState.EvmHostException.IsHostOrSystemFault(ex))
            {
                program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                throw;
            }
            catch
            {
                var remainingForwardedGas = gasToForwardForTrace - precompileGasCost;
                if (remainingForwardedGas > 0)
                {
                    program.GasRemaining -= remainingForwardedGas;
                    program.TotalGasUsed += remainingForwardedGas;
                }
                return FailPrecompileCall(program, snapshotId, newAccountStateGasCharged, gasToForwardForTrace);
            }

            return null;
        }


        private static void SettleNonPrecompileCallTarget(
            Program program, int snapshotId, bool shouldTransferValue, EvmUInt256 value)
        {
            program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
            program.ProgramResult.LastCallReturnData = null;

            if (shouldTransferValue && value > 0)
            {
                RestoreValueTransferStipend(program);
            }
        }


        private static SubCallSetup AbortCallBeforeChildFrame(
            Program program, CallFrameType callType, EvmUInt256 value, bool newAccountStateGasCharged)
        {
            if (TransfersValue(callType) && value > 0)
            {
                RestoreValueTransferStipend(program);
            }
            if (newAccountStateGasCharged)
            {
                StateGasMeter.CreditStateGasRefund(program, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
            }
            program.ProgramResult.LastCallReturnData = null;
            program.StackPush(0);
            program.Step();
            return new SubCallSetup { ShouldCreateSubCall = false };
        }


        private static SubCallSetup FailPrecompileCall(
            Program program, int snapshotId, bool newAccountStateGasCharged, long gasToForwardForTrace)
        {
            program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
            if (newAccountStateGasCharged)
            {
                StateGasMeter.CreditStateGasRefund(program, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
            }
            program.ProgramResult.LastCallReturnData = null;
            program.StackPush(0);
            program.Step();
            return new SubCallSetup { ShouldCreateSubCall = false, IsPrecompileHandled = true, GasForwarded = gasToForwardForTrace };
        }


        private static SubCallSetup SucceedCodelessCall(Program program, long gasToForwardForTrace)
        {
            program.StackPush(1);
            program.Step();
            return new SubCallSetup { ShouldCreateSubCall = false, IsPrecompileHandled = true, GasForwarded = gasToForwardForTrace };
        }


        private static void RestoreValueTransferStipend(Program program)
        {
            program.GasRemaining += GasConstants.CALL_STIPEND;
            program.TotalGasUsed -= GasConstants.CALL_STIPEND;
        }

        private static SubCallSetup ForfeitFrameOnForwardEverythingOverspend(Program program)
        {
            program.TotalGasUsed += program.GasRemaining;
            HaltFrameExceptionally(program);
            return new SubCallSetup { ShouldCreateSubCall = false };
        }


        private static bool ForwardEverythingExceedsRemaining(long gasToAllocate, Program program)
            => gasToAllocate > program.GasRemaining;

    }
}
