using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Tracing;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        public virtual async Task<OpcodeTraceResult> TraceCallAsync(
            CallInput callInput,
            OpcodeTraceConfig config = null,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            return BuildCallOpcodeResult(await ExecuteTraceCallAsync(callInput, stateOverrides), config);
        }

        public virtual async Task<OpcodeTraceResult> TraceCallAsync(
            CallInput callInput,
            BigInteger blockNumber,
            OpcodeTraceConfig config = null,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            return BuildCallOpcodeResult(await ExecuteTraceCallAsync(callInput, blockNumber, stateOverrides), config);
        }

        private static OpcodeTraceResult BuildCallOpcodeResult(
            TraceExecutionResult result, OpcodeTraceConfig config)
        {
            if (result == null)
            {
                return new OpcodeTraceResult
                {
                    Gas = 0,
                    Failed = false,
                    ReturnValue = "0x",
                    StructLogs = new List<OpcodeTraceStep>()
                };
            }

            return TraceConverter.ConvertToOpcodeResult(result.Program, config);
        }

        public virtual async Task<CallTraceResult> TraceCallCallTracerAsync(
            CallInput callInput,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            return BuildCallTraceResult(await ExecuteTraceCallAsync(callInput, stateOverrides), callInput);
        }

        public virtual async Task<CallTraceResult> TraceCallCallTracerAsync(
            CallInput callInput,
            BigInteger blockNumber,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            return BuildCallTraceResult(await ExecuteTraceCallAsync(callInput, blockNumber, stateOverrides), callInput);
        }

        private static CallTraceResult BuildCallTraceResult(
            TraceExecutionResult result, CallInput callInput)
        {
            if (result == null)
            {
                return new CallTraceResult
                {
                    Type = "CALL",
                    From = callInput.From,
                    To = callInput.To,
                    Value = callInput.Value ?? new HexBigInteger(0),
                    Gas = callInput.Gas ?? new HexBigInteger(0),
                    GasUsed = new HexBigInteger(0),
                    Input = callInput.Data ?? "0x",
                    Output = "0x"
                };
            }

            return TraceConverter.ConvertToCallTraceResult(result.Program, result.CallInput, result.IsContractCreation);
        }

        public virtual async Task<PrestateTraceResult> TraceCallPrestateAsync(
            CallInput callInput,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            return BuildCallPrestateResult(await ExecuteTraceCallAsync(callInput, stateOverrides));
        }

        public virtual async Task<PrestateTraceResult> TraceCallPrestateAsync(
            CallInput callInput,
            BigInteger blockNumber,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            return BuildCallPrestateResult(await ExecuteTraceCallAsync(callInput, blockNumber, stateOverrides));
        }

        private static PrestateTraceResult BuildCallPrestateResult(TraceExecutionResult result)
        {
            if (result == null)
            {
                return new PrestateTraceResult
                {
                    Pre = new Dictionary<string, PrestateAccountInfo>(),
                    Post = new Dictionary<string, PrestateAccountInfo>()
                };
            }

            return TraceConverter.ConvertToPrestateResult(
                result.Program.ProgramContext.ExecutionStateService);
        }

        private async Task<TraceExecutionResult> ExecuteTraceCallAsync(
            CallInput callInput,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var data = callInput.Data?.HexToByteArray();
            var callValue = callInput.Value?.Value ?? BigInteger.Zero;
            var callGasLimit = ResolveCallGas(callInput.Gas?.Value);

            var blockContext = await GetBlockContextForCallAsync();
            return await ExecuteTraceCallCoreAsync(
                _nodeDataService, blockContext, callInput, data, callValue, callGasLimit, stateOverrides);
        }

        private async Task<TraceExecutionResult> ExecuteTraceCallAsync(
            CallInput callInput,
            BigInteger blockNumber,
            Dictionary<string, StateOverride> stateOverrides = null)
        {
            var data = callInput.Data?.HexToByteArray();
            var callValue = callInput.Value?.Value ?? BigInteger.Zero;
            var callGasLimit = ResolveCallGas(callInput.Gas?.Value);

            var dataService = await GetNodeDataServiceAtBlockAsync(blockNumber);
            var blockContext = await GetBlockContextAtBlockAsync(blockNumber);
            return await ExecuteTraceCallCoreAsync(
                dataService, blockContext, callInput, data, callValue, callGasLimit, stateOverrides);
        }

        private async Task<TraceExecutionResult> ExecuteTraceCallCoreAsync(
            IStateReader dataService, BlockContext blockContext, CallInput callInput,
            byte[] data, BigInteger callValue, BigInteger callGasLimit,
            Dictionary<string, StateOverride> stateOverrides)
        {
            var from = callInput.From ?? AddressUtil.ZERO_ADDRESS;
            var to = callInput.To;
            var isContractCreation = string.IsNullOrEmpty(to);

            var executionStateService = new ExecutionStateService(dataService);

            var callerBalance = await dataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            if (stateOverrides != null)
            {
                ApplyStateOverrides(executionStateService, stateOverrides);
            }

            var ctx = new TransactionExecutionContext
            {
                Mode = ExecutionMode.Call,
                Sender = from,
                To = isContractCreation ? null : to,
                Data = data,
                Value = callValue,
                GasLimit = callGasLimit,
                GasPrice = 0,
                MaxFeePerGas = 0,
                MaxPriorityFeePerGas = 0,
                Nonce = 0,
                IsEip1559 = false,
                IsContractCreation = isContractCreation,
                BlockNumber = (long)blockContext.BlockNumber,
                Timestamp = blockContext.Timestamp,
                Coinbase = blockContext.Coinbase,
                BaseFee = blockContext.BaseFee,
                Difficulty = blockContext.Difficulty,
                BlockGasLimit = blockContext.GasLimit,
                ChainId = blockContext.ChainId,
                SlotNumber = blockContext.SlotNumber.HasValue
                    ? new EvmUInt256(blockContext.SlotNumber.Value)
                    : EvmUInt256.Zero,
                ExecutionState = executionStateService,
                TraceEnabled = true,
                EnforceSenderBalance = false,
                AdvanceSenderNonce = isContractCreation
            };

            var result = await ResolveExecutor((long)blockContext.BlockNumber, (ulong)blockContext.Timestamp).ExecuteAsync(ctx);

            if (result.Program == null)
                return null;

            var traceCallInput = new CallInput
            {
                From = from,
                To = isContractCreation ? null : to,
                Value = new HexBigInteger(callValue),
                Data = data?.ToHex(true) ?? "0x",
                Gas = new HexBigInteger(callGasLimit),
                GasPrice = new HexBigInteger(0),
                ChainId = new HexBigInteger(Config.ChainId)
            };

            return new TraceExecutionResult
            {
                Program = result.Program,
                CallInput = traceCallInput,
                StateService = executionStateService,
                IsContractCreation = isContractCreation
            };
        }
    }
}
