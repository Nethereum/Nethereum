using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Numerics;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM
{
    public partial class EVMSimulator
    {
#if EVM_SYNC
        private SubCallSetup SetupCreateFrame(Program program, CallFrame parentFrame, CallFrameType createType)
#else
        private async Task<SubCallSetup> SetupCreateFrameAsync(Program program, CallFrame parentFrame, CallFrameType createType)
#endif
        {
            if (program.ProgramContext.IsStatic)
            {
#if EVM_SYNC
                program.SetExecutionError(); return null;
#else
                throw new Exceptions.StaticCallViolationException(createType == CallFrameType.Create2 ? "CREATE2" : "CREATE");
#endif
            }

            var value = program.StackPopU256();
            var memoryIndexBig = program.StackPopU256();
            var memoryLengthBig = program.StackPopU256();

            byte[] salt = null;
            if (createType == CallFrameType.Create2)
            {
                salt = program.StackPop();
            }

            if (!memoryLengthBig.FitsInInt || (!memoryLengthBig.IsZero && !memoryIndexBig.FitsInInt))
            {
                program.ProgramResult.LastCallReturnData = null;
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            // Safe to cast: memoryLength is within int.MaxValue, and if memoryLength > 0, memoryIndex is too
            var memoryIndex = memoryLengthBig > 0 ? memoryIndexBig.ToInt() : 0;
            var memoryLength = memoryLengthBig.ToInt();

            // EIP-3860 initcode size limit (Shanghai+). HardforkConfig.MaxInitcodeSize
            // is 0 at every pre-Shanghai fork — gate the check so pre-Shanghai
            // CREATEs with init-code length > 49152 (e.g. stQuadraticComplexityTest
            // Create1000Byzantium [0,1,0] with length=0xc350=49664) actually enter
            // the inner frame instead of fail-closing here and draining parent gas.
            // Same conditional pattern is already used at TransactionExecutor.cs:67.
            if (Config.MaxInitcodeSize > 0 && memoryLength > Config.MaxInitcodeSize)
            {
                program.GasRemaining = 0;
                program.ProgramResult.LastCallReturnData = null;
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            if (parentFrame.Depth + 1 > GasConstants.MAX_CALL_DEPTH)
            {
                program.ProgramResult.LastCallReturnData = null;
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            if (memoryLength > 0 && memoryIndex + memoryLength > program.Memory.Count)
            {
                program.ExpandMemory(memoryIndex + memoryLength);
            }

            var contractAddress = program.ProgramContext.AddressContract;
#if EVM_SYNC
            var nonce = program.ProgramContext.ExecutionStateService.GetNonce(contractAddress);
#else
            var nonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(contractAddress);
#endif

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
                newContractAddress = ContractUtils.CalculateContractAddress(contractAddress, (long)nonce);
            }

            program.ProgramContext.ExecutionStateService.MarkAddressAsWarm(newContractAddress);

            // Note: EIP-3541 only rejects DEPLOYED code starting with 0xEF, not init code.
            // Init code starting with 0xEF is allowed; it will fail when the invalid opcode
            // executes (consuming all gas), matching python spec behavior.

#if EVM_SYNC
            var senderBalance = program.ProgramContext.ExecutionStateService.GetTotalBalance(contractAddress);
#else
            var senderBalance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(contractAddress);
#endif
            if (senderBalance < value)
            {
                program.ProgramResult.LastCallReturnData = null;
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            var maxNonce = ulong.MaxValue;
            if (nonce >= maxNonce)
            {
                program.ProgramResult.LastCallReturnData = null;
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            var callInput = new EvmCallContext
            {
                From = contractAddress,
                Value = value,
                To = newContractAddress,
                ChainId = program.ProgramContext.ChainId
            };

            program.ProgramContext.ExecutionStateService.SetNonce(contractAddress, nonce + 1);

            var snapshotId = program.ProgramContext.ExecutionStateService.TakeSnapshot();

#if EVM_SYNC
            var targetAccount = program.ProgramContext.ExecutionStateService.LoadBalanceNonceAndCodeFromStorage(newContractAddress);
#else
            var targetAccount = await program.ProgramContext.ExecutionStateService.LoadBalanceNonceAndCodeFromStorageAsync(newContractAddress);
#endif
            var targetHasCode = targetAccount.Code != null && targetAccount.Code.Length > 0;
            var targetHasNonce = targetAccount.Nonce.HasValue && targetAccount.Nonce.Value > 0;
            var targetHasStorage = targetAccount.Storage != null && targetAccount.Storage.Count > 0;
            if (targetHasCode || targetHasNonce || targetHasStorage)
            {
                program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);
                var collisionGas = Config.GasForwarding.CalculateMaxGasToForward(program.GasRemaining);
                program.GasRemaining -= collisionGas;
                program.TotalGasUsed += collisionGas;
                program.ProgramResult.LastCallReturnData = null;
                program.StackPush(0);
                program.Step();
                return new SubCallSetup { ShouldCreateSubCall = false };
            }

            program.ProgramContext.ExecutionStateService.DebitBalance(program.ProgramContext.AddressContract, value);

            var programContext = new ProgramContext(
                callInput,
                program.ProgramContext.ExecutionStateService,
                program.ProgramContext.AddressOrigin,
                null,
                program.ProgramContext.BlockNumber,
                program.ProgramContext.Timestamp,
                program.ProgramContext.Coinbase,
                program.ProgramContext.BaseFee);
            programContext.Difficulty = program.ProgramContext.Difficulty;
            programContext.GasLimit = program.ProgramContext.GasLimit;
            programContext.GasPrice = program.ProgramContext.GasPrice;
            programContext.Depth = parentFrame.Depth + 1;
            programContext.EnforceGasSentry = program.ProgramContext.EnforceGasSentry;
            programContext.SstoreClearsSchedule = program.ProgramContext.SstoreClearsSchedule;
            programContext.SstoreSetRefund = program.ProgramContext.SstoreSetRefund;
            programContext.SstoreResetRefund = program.ProgramContext.SstoreResetRefund;
            programContext.SstoreRefundRule = program.ProgramContext.SstoreRefundRule;
            programContext.TransientStorage = program.ProgramContext.TransientStorage;
            programContext.SetAccessListTracker(program.ProgramContext.AccessListTracker);
            // EIP-4844: propagate blob context across CREATE/CREATE2 frames
            // (symmetric with SetupCallFrame). BLOBHASH / BLOBBASEFEE must
            // read the same blob list inside any nested frame.
            programContext.BlobHashes = program.ProgramContext.BlobHashes;
            programContext.BlobBaseFee = program.ProgramContext.BlobBaseFee;

            var callProgram = new Program(byteCode, programContext);

            var gasToAllocate = Config.GasForwarding.CalculateMaxGasToForward(program.GasRemaining);
            if (gasToAllocate < 0) gasToAllocate = 0;
            callProgram.GasRemaining = gasToAllocate;
            program.GasRemaining -= gasToAllocate;

            program.ProgramContext.ExecutionStateService.CreditBalance(newContractAddress, value);

            program.ProgramContext.ExecutionStateService.SetNonce(newContractAddress, Config.ContractInitialNonce);

            // EIP-6780: Mark contract as created in current tx on the state object
            var newAcct = program.ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(newContractAddress);
            newAcct.IsNewContract = true;

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
    }
}
