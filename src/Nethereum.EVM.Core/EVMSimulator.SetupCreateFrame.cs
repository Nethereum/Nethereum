using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Execution.Create;
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
            var value = program.StackPopU256();
            var memoryIndexBig = program.StackPopU256();
            var memoryLengthBig = program.StackPopU256();

            byte[] salt = null;
            if (createType == CallFrameType.Create2)
            {
                salt = program.StackPop();
            }

            if (InputRegionOverflows(memoryIndexBig, memoryLengthBig)) return ReportCreateFailedToCaller(program);

            var memoryIndex = memoryLengthBig > 0 ? memoryIndexBig.ToInt() : 0;
            var memoryLength = memoryLengthBig.ToInt();

            if (ExceedsMaxInitcodeSize(memoryLength)) return ForfeitFrameOnOversizedInitcode(program);

            if (ExceedsMaxCallDepth(parentFrame)) return ReportCreateFailedToCaller(program);

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

            var byteCode = ReadInitCodeFromMemory(program, memoryIndex, memoryLength);

            var newContractAddress = ResolveNewContractAddress(createType, contractAddress, salt, byteCode, nonce);


#if EVM_SYNC
            var senderBalance = program.ProgramContext.ExecutionStateService.GetTotalBalance(contractAddress);
#else
            var senderBalance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(contractAddress);
#endif
            if (senderBalance < value) return ReportCreateFailedToCaller(program);

            if (NonceWouldOverflow(nonce)) return ReportCreateFailedToCaller(program);

            program.ProgramContext.ExecutionStateService.MarkAddressAsWarm(newContractAddress);

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
            program.ProgramContext.ExecutionStateService.LoadBalanceNonceAndCodeFromStorage(newContractAddress);
            var targetIsDeployable = AccountDeployability.IsDeployable(
                program.ProgramContext.ExecutionStateService, newContractAddress);
#else
            await program.ProgramContext.ExecutionStateService.LoadBalanceNonceAndCodeFromStorageAsync(newContractAddress);
            var targetIsDeployable = await AccountDeployability.IsDeployableAsync(
                program.ProgramContext.ExecutionStateService, newContractAddress);
#endif

#if EVM_SYNC
            var targetIsAlive = program.ProgramContext.ExecutionStateService.AccountExists(newContractAddress);
#else
            var targetIsAlive = await program.ProgramContext.ExecutionStateService.AccountExistsAsync(newContractAddress);
#endif
            var newAccountCharged = ChargesNewAccountStateGas(program, targetIsAlive);

            if (newAccountCharged)
            {
                StateGasMeter.ChargeStateGas(program, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
#if EVM_SYNC
                if (program.HasExecutionError) return null;
#endif
            }

            if (!targetIsDeployable) return AbortCreateOnCollision(program, snapshotId, newAccountCharged);

            program.ProgramContext.ExecutionStateService.DebitBalance(program.ProgramContext.AddressContract, value);

            var programContext = BuildInitCodeProgramContext(program, parentFrame, callInput);

            var callProgram = new Program(byteCode, programContext);

            var gasToAllocate = GrantExecutionGasToChild(program, callProgram);

            DrainStateGasReservoirIntoChild(program, callProgram);

            program.ProgramContext.ExecutionStateService.CreditBalance(newContractAddress, value);

            EmitEndowmentTransferLog(program, callProgram, newContractAddress, value);

            program.ProgramContext.ExecutionStateService.SetNonce(newContractAddress, Config.ContractInitialNonce);

            MarkCreatedInThisTransaction(program, newContractAddress);

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
                SnapshotId = snapshotId,
                NewAccountStateGasCharged = newAccountCharged
            };

            return new SubCallSetup { ShouldCreateSubCall = true, NewFrame = newFrame, GasForwarded = gasToAllocate };
        }

        private static SubCallSetup ReportCreateFailedToCaller(Program program)
        {
            program.ProgramResult.LastCallReturnData = null;
            program.StackPush(0);
            program.Step();
            return new SubCallSetup { ShouldCreateSubCall = false };
        }

        private bool ExceedsMaxInitcodeSize(int memoryLength)
            => Config.MaxInitcodeSize > 0 && memoryLength > Config.MaxInitcodeSize;

        /// <summary>
        /// EIP-3860 init-code-size violation is an EXCEPTIONAL HALT
        /// of the frame executing CREATE/CREATE2 (full gas
        /// forfeiture, frame stops), not a soft "push 0 and
        /// continue" CREATE failure — unlike every other
        /// early-return in this method (balance, depth, collision),
        /// which ARE genuine soft failures per spec.
        ///
        /// <para>It does NOT accumulate <c>TotalGasUsed</c>, which its call-side near-twin
        /// <see cref="ForfeitFrameOnForwardEverythingOverspend"/> does before zeroing. That figure
        /// reaches the receipt, so the two forfeits stay two methods.</para>
        /// </summary>
        private static SubCallSetup ForfeitFrameOnOversizedInitcode(Program program)
        {
            HaltFrameExceptionally(program);
            return new SubCallSetup { ShouldCreateSubCall = false };
        }

        private static byte[] ReadInitCodeFromMemory(Program program, int memoryIndex, int memoryLength)
        {
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
            return byteCode;
        }

        private static string ResolveNewContractAddress(
            CallFrameType createType, string contractAddress, byte[] salt, byte[] byteCode, EvmUInt256 nonce)
        {
            string newContractAddress;
            if (createType == CallFrameType.Create2)
            {
                newContractAddress = ContractUtils.CalculateCreate2Address(contractAddress, salt.ToHex(), byteCode.ToHex());
            }
            else
            {
                newContractAddress = ContractUtils.CalculateContractAddress(contractAddress, (long)nonce);
            }
            return newContractAddress;
        }

        private static bool NonceWouldOverflow(EvmUInt256 nonce)
        {
            var maxNonce = ulong.MaxValue;
            return nonce >= maxNonce;
        }

        private static bool ChargesNewAccountStateGas(Program program, bool targetIsAlive)
            => program.ProgramContext.StateGasActive && !targetIsAlive;

        /// <summary>
        /// Builds the initcode frame's <see cref="ProgramContext"/> — everything the CREATE/CREATE2
        /// child inherits from its parent.
        ///
        /// <para>It is inert: no gas accounting, no state read or write, no early return. Its whole
        /// content is "copy what the child inherits from its parent", and the reason it is worth a
        /// name is that FORGETTING one of these is a live defect class — the EIP-4844 note records
        /// BLOBHASH returning zero inside every inner frame until the blob context was
        /// propagated.</para>
        /// </summary>
        private static ProgramContext BuildInitCodeProgramContext(
            Program program, CallFrame parentFrame, EvmCallContext callInput)
        {
            var programContext = new ProgramContext(
                callInput,
                program.ProgramContext.ExecutionStateService,
                program.ProgramContext.AddressOrigin,
                null,
                program.ProgramContext.BlockNumber,
                program.ProgramContext.Timestamp,
                program.ProgramContext.Coinbase,
                program.ProgramContext.BaseFee,
                preserveZeroBaseFee: program.ProgramContext.PreserveZeroBaseFee);
            programContext.BlockHashRule = program.ProgramContext.BlockHashRule;
            programContext.PrecompileRelocations = program.ProgramContext.PrecompileRelocations;
            programContext.Difficulty = program.ProgramContext.Difficulty;
            programContext.GasLimit = program.ProgramContext.GasLimit;
            programContext.GasPrice = program.ProgramContext.GasPrice;
            programContext.SlotNumber = program.ProgramContext.SlotNumber;
            programContext.Depth = parentFrame.Depth + 1;
            programContext.EnforceSstoreGasStipend = program.ProgramContext.EnforceSstoreGasStipend;
            programContext.SstoreClearsSchedule = program.ProgramContext.SstoreClearsSchedule;
            programContext.SstoreSetRefund = program.ProgramContext.SstoreSetRefund;
            programContext.SstoreResetRefund = program.ProgramContext.SstoreResetRefund;
            programContext.SstoreRefundRule = program.ProgramContext.SstoreRefundRule;
            programContext.EthTransferLogRule = program.ProgramContext.EthTransferLogRule;
            programContext.StateGasActive = program.ProgramContext.StateGasActive;
            programContext.TransientStorage = program.ProgramContext.TransientStorage;
            programContext.SetAccessListTracker(program.ProgramContext.AccessListTracker);
            programContext.BlobHashes = program.ProgramContext.BlobHashes;
            programContext.BlobBaseFee = program.ProgramContext.BlobBaseFee;
            return programContext;
        }

        private long GrantExecutionGasToChild(Program program, Program callProgram)
        {
            var gasToAllocate = Config.GasForwarding.CalculateMaxGasToForward(program.GasRemaining);
            if (gasToAllocate < 0) gasToAllocate = 0;
            callProgram.GasRemaining = gasToAllocate;
            program.GasRemaining -= gasToAllocate;
            return gasToAllocate;
        }

        private static void DrainStateGasReservoirIntoChild(Program program, Program callProgram)
        {
            callProgram.StateGasLeft = StateGasMeter.DrainReservoir(program);
            callProgram.StateGasBaseline = callProgram.StateGasLeft;
        }

        /// <summary>
        /// EIP-7708: the endowment is an ETH transfer, and it belongs to
        /// the initcode frame about to run — so it is that frame's FIRST
        /// log and it dies with the frame if creation fails. EELS reaches
        /// it through the same process_call every other frame entry uses
        /// (forks/amsterdam/vm/interpreter.py:365 -> :423-437).
        ///
        /// <para>The log goes to <paramref name="callProgram"/>, the CHILD — the two parameters
        /// are both <see cref="Program"/> and transposing them compiles.</para>
        ///
        /// <para>It carries no guard of its own because the rule holds both: EIP-7708 logs
        /// only a "nonzero-value-transferring <c>CREATE</c> or <c>CREATE2</c>", and
        /// <see cref="Rules.Eip7708EthTransferLogRule.Emit"/> drops a zero endowment and a
        /// self-transfer before it writes anything.</para>
        /// </summary>
        private static void EmitEndowmentTransferLog(
            Program program, Program callProgram, string newContractAddress, EvmUInt256 value)
        {
            program.ProgramContext.EthTransferLogRule.Emit(
                callProgram.ProgramResult.Logs,
                program.ProgramContext.AddressContract,
                newContractAddress,
                value);
        }

        private static void MarkCreatedInThisTransaction(Program program, string newContractAddress)
        {
            var newAcct = program.ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(newContractAddress);
            newAcct.IsNewContract = true;
        }

        /// <summary>
        /// EIP-8037: the account-creation charge is made "before 63/64 of the remaining gas is
        /// forwarded", the collision "fails as an exceptional halt of the create frame,
        /// consuming the forwarded gas", and only then "the charge is refilled by the failure
        /// rule". Refilling first would compute the 63/64 forward over gas the frame does not
        /// have at that point.
        /// </summary>
        private SubCallSetup AbortCreateOnCollision(Program program, int snapshotId, bool newAccountCharged)
        {
            program.ProgramContext.ExecutionStateService.CommitSnapshot(snapshotId);

            var collisionGas = Config.GasForwarding.CalculateMaxGasToForward(program.GasRemaining);
            program.GasRemaining -= collisionGas;
            program.TotalGasUsed += collisionGas;

            if (newAccountCharged)
            {
                StateGasMeter.CreditStateGasRefund(program, GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS);
            }

            return ReportCreateFailedToCaller(program);
        }
    }
}
