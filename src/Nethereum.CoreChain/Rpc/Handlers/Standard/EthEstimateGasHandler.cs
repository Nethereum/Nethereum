using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthEstimateGasHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_estimateGas.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var callInput = GetParam<CallInput>(request, 0);
            var blockTag = GetOptionalParam<string>(request, 1, "latest");

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            BigInteger? value = callInput.Value?.Value;

            var isContractCreation = SignedTransactionExtensions.IsContractCreationRecipient(callInput.To);

            var gasRules = await context.ResolveGasRulesAtBlockOrHeadAsync(blockNumber);
            var dataBytes = callInput.Data?.HexToByteArray();

            var isSelfTransfer = !isContractCreation && callInput.From.IsTheSameAddress(callInput.To);
            var hasValue = value.HasValue && value.Value > 0;

            var ceiling = EstimationCeiling(gasRules);
            var execution = await ExecuteForEstimateAsync(
                context, callInput, blockNumber, value, ceiling, isContractCreation);

            if (!execution.Success) return RevertedError(request.Id, execution);

            var floorGas = (BigInteger)gasRules.CalculateFloorGasLimit(dataBytes, isContractCreation, isSelfTransfer, hasValue, accessList: null);

            var lowestSuccessfulLimit = await LowestSuccessfulGasLimitAsync(
                gasLimit => SucceedsWithGasLimitAsync(context, callInput, blockNumber, value, gasLimit, isContractCreation),
                TotalGasTheCallerMustFund(execution, floorGas), execution.GasUsed, ceiling);

            var estimate = lowestSuccessfulLimit
                * (100 + context.Node.Config.EstimateGasPaddingPercent) / 100;

            return Success(request.Id, new HexBigInteger(estimate));
        }

        private const double EstimateGasErrorRatio = 0.015;
        private const long CallStipend = 2300;

        private static async Task<BigInteger> LowestSuccessfulGasLimitAsync(
            System.Func<BigInteger, Task<bool>> succeedsWithGasLimit,
            BigInteger gasTheCallerMustFund, BigInteger gasUsed, BigInteger ceiling)
        {
            if (await succeedsWithGasLimit(gasTheCallerMustFund)) return gasTheCallerMustFund;

            var lo = gasTheCallerMustFund;
            var hi = ceiling;

            var optimisticGasLimit = (gasUsed + CallStipend) * 64 / 63;
            if (optimisticGasLimit > lo && optimisticGasLimit < hi)
            {
                if (await succeedsWithGasLimit(optimisticGasLimit)) hi = optimisticGasLimit;
                else lo = optimisticGasLimit;
            }

            while (lo + 1 < hi)
            {
                if ((double)(hi - lo) / (double)hi < EstimateGasErrorRatio) break;

                var mid = lo + (hi - lo) / 2;
                if (mid > lo * 2) mid = lo * 2;

                if (await succeedsWithGasLimit(mid)) hi = mid;
                else lo = mid;
            }

            return hi;
        }

        private static async Task<bool> SucceedsWithGasLimitAsync(
            RpcContext context, CallInput callInput, BigInteger blockNumber,
            BigInteger? value, BigInteger gasLimit, bool isContractCreation)
            => (await ExecuteForEstimateAsync(context, callInput, blockNumber, value, gasLimit, isContractCreation)).Success;

        private static BigInteger TotalGasTheCallerMustFund(CallResult execution, BigInteger floorGas)
            => BigInteger.Max(execution.GasUsed, floorGas + execution.StateGasUsed);

        private static BigInteger EstimationCeiling(IntrinsicGasRules gasRules)
        {
            const long ExecutionGasCeiling = 30_000_000;
            if (!gasRules.StateGasActive) return ExecutionGasCeiling;

            return ExecutionGasCeiling + LargestCodeDepositTheForkAllows;
        }

        private static BigInteger LargestCodeDepositTheForkAllows
            => (BigInteger)GasConstants.EIP7954_MAX_CODE_SIZE * GasConstants.EIP8037_COST_PER_STATE_BYTE;

        private static Task<CallResult> ExecuteForEstimateAsync(
            RpcContext context, CallInput callInput, BigInteger blockNumber,
            BigInteger? value, BigInteger gasBudget, bool isContractCreation)
        {
            if (isContractCreation)
                return context.Node.EstimateContractCreationGasAsync(
                    callInput.Data?.HexToByteArray(), blockNumber, callInput.From, value, gasBudget);

            var data = callInput.Data?.HexToByteArray();
            if (context.Node is ChainNodeBase node)
                return node.CallWithFeePolicyAsync(
                    callInput.To, data, blockNumber, callInput.From, value, gasBudget,
                    stateOverrides: null, authorisationList: null,
                    gasPrice: callInput.GasPrice?.Value,
                    maxFeePerGas: callInput.MaxFeePerGas?.Value,
                    maxPriorityFeePerGas: callInput.MaxPriorityFeePerGas?.Value);

            return context.Node.CallAsync(
                callInput.To, data, blockNumber, callInput.From, value, gasBudget);
        }
    }
}
