using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Tracing;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        public virtual async Task<CallResult> CallAsync(string to, byte[] data, string from = null,
            BigInteger? value = null, BigInteger? gasLimit = null, Dictionary<string, StateOverride> stateOverrides = null,
            List<Authorisation7702Signed> authorisationList = null)
        {
            var blockContext = await GetBlockContextForCallAsync();
            return await CallCoreAsync(_nodeDataService, blockContext, to, data, from, value, gasLimit, stateOverrides, authorisationList);
        }

        public virtual async Task<CallResult> CallAsync(string to, byte[] data, BigInteger blockNumber,
            string from = null, BigInteger? value = null, BigInteger? gasLimit = null, Dictionary<string, StateOverride> stateOverrides = null,
            List<Authorisation7702Signed> authorisationList = null)
        {
            var dataService = await GetNodeDataServiceAtBlockAsync(blockNumber);
            var blockContext = await GetBlockContextAtBlockAsync(blockNumber);
            return await CallCoreAsync(dataService, blockContext, to, data, from, value, gasLimit, stateOverrides, authorisationList);
        }

        internal async Task<CallResult> CallWithFeePolicyAsync(string to, byte[] data, BigInteger blockNumber,
            string from, BigInteger? value, BigInteger? gasLimit, Dictionary<string, StateOverride> stateOverrides,
            List<Authorisation7702Signed> authorisationList,
            BigInteger? gasPrice, BigInteger? maxFeePerGas, BigInteger? maxPriorityFeePerGas)
        {
            var dataService = await GetNodeDataServiceAtBlockAsync(blockNumber);
            var blockContext = await GetBlockContextAtBlockAsync(blockNumber);
            return await CallCoreAsync(dataService, blockContext, to, data, from, value, gasLimit, stateOverrides, authorisationList,
                gasPrice, maxFeePerGas, maxPriorityFeePerGas);
        }

        private async Task<CallResult> CallCoreAsync(
            IStateReader dataService, BlockContext blockContext, string to, byte[] data,
            string from, BigInteger? value, BigInteger? gasLimit,
            Dictionary<string, StateOverride> stateOverrides, List<Authorisation7702Signed> authorisationList,
            BigInteger? gasPrice = null, BigInteger? maxFeePerGas = null, BigInteger? maxPriorityFeePerGas = null)
        {
            from = from ?? AddressUtil.ZERO_ADDRESS;
            var callValue = value ?? BigInteger.Zero;
            var callGasLimit = ResolveCallGas(gasLimit);
            var applyBlockBaseFee = CallAppliesBlockBaseFee(gasPrice, maxFeePerGas, maxPriorityFeePerGas);
            var contextBaseFee = applyBlockBaseFee ? blockContext.BaseFee : BigInteger.Zero;

            var executionStateService = new ExecutionStateService(dataService);

            var isContractCreation = string.IsNullOrEmpty(to);

            var callerBalance = await dataService.GetBalanceAsync(from);
            executionStateService.SetInitialChainBalance(from, callerBalance);

            if (stateOverrides != null) ApplyStateOverrides(executionStateService, stateOverrides);

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
                BaseFee = contextBaseFee,
                Difficulty = blockContext.Difficulty,
                BlockGasLimit = blockContext.GasLimit,
                ChainId = blockContext.ChainId,
                SlotNumber = blockContext.SlotNumber.HasValue
                    ? new EvmUInt256(blockContext.SlotNumber.Value)
                    : EvmUInt256.Zero,
                ExecutionState = executionStateService,
                TraceEnabled = false,
                AuthorisationList = authorisationList,
                PreserveZeroBaseFee = !applyBlockBaseFee
            };

            var result = await ResolveExecutor((long)blockContext.BlockNumber, (ulong)blockContext.Timestamp).ExecuteAsync(ctx);

            return new CallResult
            {
                Success = result.Success,
                ReturnData = result.ReturnData ?? Array.Empty<byte>(),
                RevertReason = result.RevertReason ?? result.Error,
                GasUsed = result.GasUsed,
                ExecutionGasUsed = result.ExecutionGasUsed,
                StateGasUsed = result.StateGasUsed,
                IntrinsicGasUsed = result.IntrinsicGasUsed
            };
        }

        private static bool CallAppliesBlockBaseFee(
            BigInteger? gasPrice, BigInteger? maxFeePerGas, BigInteger? maxPriorityFeePerGas)
        {
            var feeCap = gasPrice ?? maxFeePerGas ?? BigInteger.Zero;
            var tipCap = gasPrice ?? maxPriorityFeePerGas ?? BigInteger.Zero;
            return feeCap > 0 || tipCap > 0;
        }
    }
}
