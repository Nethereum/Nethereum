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
            var gasU256 = program.StackPopU256();
            var gas = gasU256.ToLongSafe();
            var codeAddress = program.StackPop();
            EvmUInt256 value = EvmUInt256.Zero;
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
                    value = program.StackPopU256();
                    from = program.ProgramContext.AddressContract;
                    to = program.ProgramContext.AddressContract;
                    break;
                default:
                    value = program.StackPopU256();
                    from = program.ProgramContext.AddressContract;
                    to = codeAddress.ConvertToEthereumChecksumAddress();
                    break;
            }

            if (program.ProgramContext.IsStatic && value > 0 && callType == CallFrameType.Call)
            {
#if EVM_SYNC
                program.SetExecutionError(); return null;
#else
                throw new Exceptions.StaticCallViolationException(callType.ToString());
#endif
            }

            var dataInputIndexBig = program.StackPopU256();
            var dataInputLengthBig = program.StackPopU256();
            var resultMemoryDataIndexBig = program.StackPopU256();
            var resultMemoryDataLengthBig = program.StackPopU256();

            // Only reject for input data overflow — output region handled via gas cost
            if ((!dataInputLengthBig.IsZero && !dataInputIndexBig.FitsInInt) ||
                !dataInputLengthBig.FitsInInt)
            {
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            var dataInputIndex = !dataInputIndexBig.FitsInInt ? int.MaxValue : dataInputIndexBig.ToInt();
            var dataInputLength = dataInputLengthBig.ToInt();
            var resultMemoryDataIndex = resultMemoryDataIndexBig.FitsInInt ? resultMemoryDataIndexBig.ToInt() : int.MaxValue;
            var resultMemoryDataLength = resultMemoryDataLengthBig.FitsInInt ? resultMemoryDataLengthBig.ToInt() : int.MaxValue;

            // Fetch the target's code and resolve EIP-7702 delegation BEFORE the
            // max-depth / balance checks. Per the python execution-specs
            // (eoa_delegation.access_delegation) the delegate access gas is
            // charged unconditionally whenever a CALL targets a delegated account
            // — even when the child frame fails to enter because of the stack
            // depth limit. Warming is applied to the PARENT's accessed_addresses,
            // so a child revert still leaves the delegate warm for subsequent
            // calls.
            var codeAddressAsChecksum = codeAddress.ConvertToEthereumChecksumAddress();
            program.ProgramContext.RecordAddressAccess(codeAddressAsChecksum);

            var registry = Config.Precompiles;
            int precompileAddress = -1;
            bool isKnownPrecompile = registry != null
                && TryParsePrecompileAddress(codeAddressAsChecksum, out precompileAddress)
                && registry.CanHandle(precompileAddress);

            byte[] byteCode;
            if (isKnownPrecompile)
            {
                byteCode = null;
            }
            else if (callType == CallFrameType.Call)
            {
                // CALL materialises the target account in state (Geth
                // core/vm/evm.go Call: explicit StateDB.CreateAccount(addr)
                // for the value-transfer / non-precompile case). Use the
                // cached path which goes through
                // CreateOrGetAccountExecutionState as a deliberate side-effect.
#if EVM_SYNC
                byteCode = program.ProgramContext.ExecutionStateService.GetCode(codeAddressAsChecksum);
#else
                byteCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(codeAddressAsChecksum);
#endif
                // Mark this entry as CALL-frame-materialised so the
                // Frontier–Tangerine G_NEWACCOUNT check distinguishes
                // structural creation from incidental BALANCE/EXTCODE*
                // read materialisation. See
                // <see cref="AccountExecutionState.WasMaterialisedByCallFrame"/>.
                var matAcct = program.ProgramContext.ExecutionStateService
                    .CreateOrGetAccountExecutionState(codeAddressAsChecksum);
                matAcct.WasMaterialisedByCallFrame = true;
            }
            else
            {
                // CALLCODE / DELEGATECALL / STATICCALL do NOT touch the target
                // address (Geth core/vm/evm.go: CallCode / DelegateCall /
                // StaticCall only resolveCode(addr); no
                // StateDB.CreateAccount). GetCodeReadOnly keeps visibility of
                // in-tx newly-created contracts (they live in AccountsState
                // already), but for a non-existent target it falls through
                // to the chain reader without materialising an
                // AccountsState entry — preventing an extra empty account
                // from leaking into pre-EIP-158 post-state.
#if EVM_SYNC
                byteCode = program.ProgramContext.ExecutionStateService.GetCodeReadOnly(codeAddressAsChecksum);
#else
                byteCode = await program.ProgramContext.ExecutionStateService.GetCodeReadOnlyAsync(codeAddressAsChecksum);
#endif
            }

            var callFrameContext = new Execution.CallFrame.CallFrameSetupContext
            {
                Program = program,
                CodeAddress = codeAddressAsChecksum,
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

            var maxAllowedGasFromRules = Config.GasForwarding.CalculateMaxGasToForward(program.GasRemaining);

            // Pre-EIP-150 (Frontier/Homestead) forward-everything OOG check.
            // Geth's callGas runs INSIDE contract.UseGas in core/vm/gas_table.go;
            // when the total exceeds remaining (or when the requested gas
            // doesn't fit in uint64) the call OOGs the entire current frame
            // and returns ErrOutOfGas/ErrGasUintOverflow. Gas accounting
            // pre-empts both the max-depth check (in evm.Call ErrDepth) and
            // the insufficient-balance check (also in evm.Call). At EIP-150+
            // Config.GasForwarding caps gasToAllocate to remaining-remaining/64
            // so this branch can't fire — behaviour unchanged at every fork
            // from Tangerine Whistle onward.
            // Exposed by CallRecursiveBomb2 at Frontier/Homestead (the leaf
            // frame at depth 1025 has GAS=14999 < 15000, so GAS-15000 underflows
            // to 2^256-1 — geth OOGs the leaf via uint64-overflow; we used to
            // hit the depth check first and continue with the leaf's SSTORE
            // committed). Also covers stCallCreateCallCodeTest high-value+OOG
            // (3 fixtures at Frontier) and the stZeroKnowledge bn256 cluster
            // (~134 sub-tests at Frontier+Homestead).
            // No snapshot has been taken yet, so no revert is needed.
            var preForwardGas = Config.GasForwarding.CalculateGasForCall(program.GasRemaining, gas);
            if (preForwardGas < 0) preForwardGas = 0;
            if (preForwardGas > program.GasRemaining)
            {
                program.TotalGasUsed += program.GasRemaining;
                program.GasRemaining = 0;
                program.ProgramResult.IsRevert = true;
                program.Stop();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            if (parentFrame.Depth + 1 > GasConstants.MAX_CALL_DEPTH)
            {
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

            var inputEnd = dataInputLength > 0 ? dataInputIndex + dataInputLength : 0;
            var outputEnd = resultMemoryDataLength > 0 && resultMemoryDataIndex < int.MaxValue ? resultMemoryDataIndex + resultMemoryDataLength : 0;
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

            var callInput = new EvmCallContext
            {
                From = from,
                Value = value,
                To = to,
                Data = dataInput,
                Gas = gas,
                ChainId = program.ProgramContext.ChainId
            };

            var shouldTransferValue = callType != CallFrameType.DelegateCall && callType != CallFrameType.StaticCall;

            if (shouldTransferValue && value > 0)
            {
#if EVM_SYNC
                var callerBalance = program.ProgramContext.ExecutionStateService.GetTotalBalance(program.ProgramContext.AddressContract);
#else
                var callerBalance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(program.ProgramContext.AddressContract);
#endif
                if (callerBalance < value)
                {
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
                program.ProgramContext.ExecutionStateService.DebitBalance(program.ProgramContext.AddressContract, value);
            }

            if (byteCode == null || byteCode.Length == 0)
            {
                if (shouldTransferValue)
                {
                    program.ProgramContext.ExecutionStateService.CreditBalance(to, value);
                }

                var maxAllowedGasForEmpty = maxAllowedGasFromRules;
                var gasToForwardForTrace = gas > maxAllowedGasForEmpty ? maxAllowedGasForEmpty : gas;
                if (gasToForwardForTrace < 0) gasToForwardForTrace = 0;

                if (dataInput != null)
                {
                    if (isKnownPrecompile)
                    {
#if EVM_SYNC
                        var precompileAccount = program.ProgramContext.ExecutionStateService.LoadBalanceNonceAndCodeFromStorage(codeAddressAsChecksum);
#else
                        var precompileAccount = await program.ProgramContext.ExecutionStateService.LoadBalanceNonceAndCodeFromStorageAsync(codeAddressAsChecksum);
#endif
                        // Geth touches the target on CALL and STATICCALL only:
                        // core/vm/evm.go Call() does StateDB.AddBalance(addr, value)
                        // even when value == 0, and StaticCall() does an explicit
                        // AddBalance(addr, 0) "just for the sake of triggering a
                        // touch." CallCode and DelegateCall do not touch the
                        // target. At EIP-161+ this drives whether the (always
                        // empty) precompile is swept from post-state.
                        //
                        // RIPEMD-160 (0x...003) special case: geth's state_object.touch
                        // calls journal.dirty(ripemd) OUTSIDE the journaled
                        // touchChange — the dirty mark survives snapshot revert
                        // while the touchChange itself can be rolled back. As a
                        // result, when a sub-call that touched RIPEMD reverts, the
                        // other 7 precompile touches get journal-rolled back but
                        // RIPEMD stays "dirty" and is deleted by EIP-161 end-of-tx
                        // cleanup. RevertPrecompiledTouch.json d0/d3 canonical
                        // hashes encode exactly this asymmetry.
                        if (callType == CallFrameType.Call || callType == CallFrameType.StaticCall)
                        {
                            precompileAccount.IsTouched = true;
                            if (precompileAddress == RipemdPrecompileAddress)
                            {
                                program.ProgramContext.ExecutionStateService.MarkTxGloballyTouched(codeAddressAsChecksum);
                            }
                        }
                        long precompileGasCost = registry.GetGasCost(precompileAddress, dataInput);

                        if (gasToForwardForTrace < precompileGasCost)
                        {
                            program.GasRemaining -= gasToForwardForTrace;
                            program.TotalGasUsed += gasToForwardForTrace;
                            program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                            program.StackPush(0);
                            program.ProgramResult.LastCallReturnData = null;
                            program.Step();
                            return new SubCallSetup { ShouldCreateSubCall = false, IsPrecompileHandled = true, GasForwarded = gasToForwardForTrace };
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
                                program.GasRemaining += GasConstants.CALL_STIPEND;
                                program.TotalGasUsed -= GasConstants.CALL_STIPEND;
                            }
                        }
                        catch
                        {
                            var remainingForwardedGas = gasToForwardForTrace - precompileGasCost;
                            if (remainingForwardedGas > 0)
                            {
                                program.GasRemaining -= remainingForwardedGas;
                                program.TotalGasUsed += remainingForwardedGas;
                            }
                            program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                            program.ProgramResult.LastCallReturnData = null;
                            program.StackPush(0);
                            program.Step();
                            return new SubCallSetup { ShouldCreateSubCall = false, IsPrecompileHandled = true, GasForwarded = gasToForwardForTrace };
                        }
                    }
                    else
                    {
                        program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                        program.ProgramResult.LastCallReturnData = null;

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
                    program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                    program.ProgramResult.LastCallReturnData = null;

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
                blockNumber: program.ProgramContext.BlockNumber,
                timestamp: program.ProgramContext.Timestamp,
                coinbase: program.ProgramContext.Coinbase,
                baseFee: program.ProgramContext.BaseFee);
            programContext.Difficulty = program.ProgramContext.Difficulty;
            programContext.GasLimit = program.ProgramContext.GasLimit;
            programContext.GasPrice = program.ProgramContext.GasPrice;
            programContext.Depth = parentFrame.Depth + 1;
            programContext.IsStatic = isStatic || program.ProgramContext.IsStatic;
            programContext.EnforceGasSentry = program.ProgramContext.EnforceGasSentry;
            programContext.SstoreClearsSchedule = program.ProgramContext.SstoreClearsSchedule;
            programContext.SstoreSetRefund = program.ProgramContext.SstoreSetRefund;
            programContext.SstoreResetRefund = program.ProgramContext.SstoreResetRefund;
            programContext.SstoreRefundRule = program.ProgramContext.SstoreRefundRule;
            programContext.TransientStorage = program.ProgramContext.TransientStorage;
            programContext.SetAccessListTracker(program.ProgramContext.AccessListTracker);
            // EIP-4844: BLOBHASH / BLOBBASEFEE read from the inner frame's
            // ProgramContext, so blob context must propagate across every
            // CALL/CALLCODE/DELEGATECALL/STATICCALL boundary. Missing this
            // makes BLOBHASH return zero inside any inner frame even on a
            // blob tx (first sighting: mainnet block 20,000,000 tx[21]
            // DELEGATECALL into the implementation at depth 2).
            programContext.BlobHashes = program.ProgramContext.BlobHashes;
            programContext.BlobBaseFee = program.ProgramContext.BlobBaseFee;

            var callProgram = new Program(byteCode, programContext);

            // Pre-EIP-150 (Frontier/Homestead): forward exactly what the
            // user requested even if it exceeds remaining gas (geth
            // core/vm/gas.go callGas with isEip150=false returns callCost
            // unmodified). If it does exceed, the CALL itself OOGs and the
            // entire remaining gas is consumed.
            // EIP-150+: cap at remaining - remaining/64.
            var gasToAllocate = Config.GasForwarding.CalculateGasForCall(program.GasRemaining, gas);
            if (gasToAllocate < 0) gasToAllocate = 0;

            if (gasToAllocate > program.GasRemaining)
            {
                // OOG at the CALL site itself (pre-EIP-150 path with a
                // user-requested gas larger than what's available). Geth:
                // contract.UseGas(gas) fails, the entire frame consumes
                // remaining gas and returns ErrOutOfGas to its caller.
                program.TotalGasUsed += program.GasRemaining;
                program.GasRemaining = 0;
                program.ProgramResult.IsRevert = true;
                program.Stop();
                program.ProgramContext.ExecutionStateService.RevertToSnapshot(snapshotId);
                program.StackPush(0);
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

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
                program.ProgramContext.ExecutionStateService.CreditBalance(to, value);
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

        // Geth's state_object.touch hard-codes the RIPEMD-160 precompile address
        // (0x...003) as a permanently-dirty account. The journal entry for
        // touching RIPEMD is journaled like any other touch, but a separate
        // non-journaled journal.dirty(ripemd) call ensures the dirty mark
        // survives even when the surrounding sub-call reverts. This avoids the
        // 2016-era consensus discrepancy where some clients would and others
        // wouldn't sweep RIPEMD on a touched-then-reverted path.
        private const int RipemdPrecompileAddress = 3;

        /// <summary>
        /// Parses the numeric value of a 20-byte address when it fits in an
        /// int32. Used to route CALLs to the new PrecompileRegistry whose
        /// dispatch key is an int (precompile addresses collapse to
        /// 1..17 + 256). Returns false for any non-precompile address,
        /// whereupon the caller falls back to legacy dispatch. Matches the
        /// existing <c>BuiltInPrecompileProvider.IsPrecompiledAddress</c>
        /// convention.
        /// </summary>
        private static bool TryParsePrecompileAddress(string checksumAddress, out int addressInt)
        {
            addressInt = -1;
            if (string.IsNullOrEmpty(checksumAddress)) return false;
            var compact = checksumAddress.ToHexCompact();
            return int.TryParse(compact, System.Globalization.NumberStyles.HexNumber, null, out addressInt);
        }
    }
}
