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

            var execution = await ExecuteForEstimateAsync(
                context, callInput, blockNumber, value, EstimationCeiling(gasRules), isContractCreation);

            if (!execution.Success) return RevertedError(request.Id, execution);

            var floorGas = (BigInteger)gasRules.CalculateFloorGasLimit(dataBytes, isContractCreation, isSelfTransfer, hasValue, accessList: null);

            var estimate = TotalGasTheCallerMustFund(execution, floorGas)
                * (100 + context.Node.Config.EstimateGasPaddingPercent) / 100;

            return Success(request.Id, new HexBigInteger(estimate));
        }

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
