using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Types;
using Nethereum.EVM.Witness;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution
{
    public static class SystemCallExecution
    {
        public static void Prepare(
            ExecutionStateService executionState,
            BlockAccessListCollector balCollector,
            string contractAddress,
            ulong blockAccessIndex)
        {
            if (balCollector != null)
            {
                balCollector.BeginUnit(blockAccessIndex);
                executionState.AccessRecorder = balCollector;
            }

        }

        public static bool IsAbsent(byte[] contractCode)
            => contractCode == null || contractCode.Length == 0;

        public static int TakeCallSnapshot(ExecutionStateService executionState)
            => executionState.TakeSnapshot();

        public static void SettleCallState(
            ExecutionStateService executionState, Program program, int snapshotId)
        {
            if (program.ProgramResult.IsRevert)
                executionState.RevertToSnapshot(snapshotId);
            else
                executionState.CommitSnapshot(snapshotId);
        }

#if EVM_SYNC
        public static Program BuildProgram(
            BlockWitnessData block,
            ExecutionStateService executionState,
            HardforkConfig config,
            string contractAddress,
            byte[] callData,
            byte[] contractCode)
#else
        public static async Task<Program> BuildProgramAsync(
            BlockWitnessData block,
            ExecutionStateService executionState,
            HardforkConfig config,
            string contractAddress,
            byte[] callData,
            byte[] contractCode)
#endif
        {
            AccessSetWarmUp.WarmOriginPrecompilesAndCoinbase(
                executionState, config, SystemCallContracts.SystemCaller, block.Coinbase);
            executionState.MarkAddressAsWarm(contractAddress);

#if EVM_SYNC
            var dispatch = ResolveDispatchTarget(executionState, config, contractAddress, contractCode);
#else
            var dispatch = await ResolveDispatchTargetAsync(executionState, config, contractAddress, contractCode);
#endif

            if (dispatch.ResolutionFailed)
                throw new System.InvalidOperationException(
                    "System call to " + contractAddress + ": the fork could not afford the " +
                    "EIP-7702 delegated-code access charge out of the system-call execution grant. " +
                    "Unreachable while the grant (SystemCallGasBudget.ExecutionGas) exceeds that " +
                    "charge by orders of magnitude; if this fires, that relationship changed and " +
                    "the failure MODE is an open decision - a failed 4788/2935 call must not " +
                    "invalidate the block, but a failed request-contract call must.");

            var program = new Program(
                dispatch.Code,
                BuildProgramContext(block, executionState, config, contractAddress, callData, dispatch.ExecutionGas));
            OpenStateGasReservoir(program, config.SystemCallGas.StateGasReservoir);
            return program;
        }

        private static ProgramContext BuildProgramContext(
            BlockWitnessData block,
            ExecutionStateService executionState,
            HardforkConfig config,
            string contractAddress,
            byte[] callData,
            long executionGas)
        {
            var callContext = new EvmCallContext
            {
                From = SystemCallContracts.SystemCaller,
                To = contractAddress,
                Data = callData ?? new byte[0],
                Gas = executionGas,
                Value = EvmUInt256.Zero,
                GasPrice = EvmUInt256.Zero,
                ChainId = block.ChainId
            };

            return new ProgramContext(
                callContext,
                executionState,
                null,
                blockNumber: EvmUInt256.FromHeaderScalar(block.BlockNumber),
                timestamp: EvmUInt256.FromHeaderScalar(block.Timestamp),
                coinbase: block.Coinbase,
                baseFee: block.BaseFee)
            {
                Difficulty = block.Difficulty != null
                    ? EvmUInt256.FromBigEndian(block.Difficulty)
                    : EvmUInt256.Zero,
                GasLimit = block.BlockGasLimit,
                StateGasActive = config.IntrinsicGasRules.StateGasActive,

                EnforceSstoreGasStipend = config.EnforceSstoreGasStipend
            };
        }

#if EVM_SYNC
        private static DispatchTarget ResolveDispatchTarget(
            ExecutionStateService executionState,
            HardforkConfig config,
            string contractAddress,
            byte[] contractCode)
        {
            var dispatchContext = BuildDispatchContext(executionState, config, contractAddress, contractCode);
            if (config.TransactionSetupRules != null)
                config.TransactionSetupRules.ApplyCodeResolution(dispatchContext, null);
            return DispatchTarget.From(dispatchContext);
        }
#else
        private static async Task<DispatchTarget> ResolveDispatchTargetAsync(
            ExecutionStateService executionState,
            HardforkConfig config,
            string contractAddress,
            byte[] contractCode)
        {
            var dispatchContext = BuildDispatchContext(executionState, config, contractAddress, contractCode);
            if (config.TransactionSetupRules != null)
                await config.TransactionSetupRules.ApplyCodeResolutionAsync(dispatchContext, null);
            return DispatchTarget.From(dispatchContext);
        }
#endif

        private static TransactionExecutionContext BuildDispatchContext(
            ExecutionStateService executionState,
            HardforkConfig config,
            string contractAddress,
            byte[] contractCode)
        {
            var context = new TransactionExecutionContext
            {
                PrepPhaseSnapshotId = executionState.TakeSnapshot(),
                Mode = ExecutionMode.SystemCall,
                Sender = SystemCallContracts.SystemCaller,
                To = contractAddress,
                Code = contractCode,
                Value = EvmUInt256.Zero,
                ExecutionState = executionState,
                ExecutionGasGrant = config.SystemCallGas.ExecutionGas,
                StateGasReservoir = config.SystemCallGas.StateGasReservoir
            };
            context.StateGas.ReservoirRemaining = config.SystemCallGas.StateGasReservoir;
            return context;
        }

        private readonly struct DispatchTarget
        {
            private DispatchTarget(byte[] code, long executionGas, bool resolutionFailed)
            {
                Code = code;
                ExecutionGas = executionGas;
                ResolutionFailed = resolutionFailed;
            }

            public byte[] Code { get; }

            public long ExecutionGas { get; }

            public bool ResolutionFailed { get; }

            public static DispatchTarget From(TransactionExecutionContext dispatchContext)
                => new DispatchTarget(
                    dispatchContext.Code,
                    dispatchContext.PreDispatchExecutionGasAvailable,
                    dispatchContext.CodeResolutionFailed);
        }

        private static void OpenStateGasReservoir(Program program, long reservoir)
        {
            program.StateGasLeft = reservoir;
            program.StateGasBaseline = reservoir;
        }

        public static void RefuseBlockOnAbsentRequestPredeploy(string contractAddress)
        {
            if (!SystemCallFailurePolicy.AbsenceInvalidatesBlock(contractAddress)) return;

            throw new SystemCallPredeployMissingException(contractAddress);
        }

        public static void RefuseBlockOnFatalCallFailure(string contractAddress, Program program)
        {
            if (!program.ProgramResult.IsRevert) return;
            if (!SystemCallFailurePolicy.FailureInvalidatesBlock(contractAddress)) return;

            throw new SystemCallFailedException(
                contractAddress, program.IsExceptionalHalt ? "execution_error" : "revert");
        }

        public static void Commit(
            InMemoryStateReader stateReader,
            ExecutionStateService executionState,
            BlockAccessListCollector balCollector,
            ulong blockAccessIndex)
        {
            stateReader.CommitChanges(executionState);
            balCollector?.Record(blockAccessIndex, executionState, stateReader);
        }
    }
}
