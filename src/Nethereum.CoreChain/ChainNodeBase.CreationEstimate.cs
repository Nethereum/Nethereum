using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        public virtual async Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode,
            string from = null, BigInteger? value = null, BigInteger? gasLimit = null)
        {
            if (initCode == null || initCode.Length == 0)
            {
                return new CallResult { Success = true, ReturnData = Array.Empty<byte>(), GasUsed = 0 };
            }

            return await EstimateCreationAsync(
                initCode,
                await GetBlockContextForCallAsync(),
                _nodeDataService,
                from, value, gasLimit);
        }

        public virtual async Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode, BigInteger blockNumber,
            string from = null, BigInteger? value = null, BigInteger? gasLimit = null)
        {
            if (initCode == null || initCode.Length == 0)
            {
                return new CallResult { Success = true, ReturnData = Array.Empty<byte>(), GasUsed = 0 };
            }

            var dataService = await GetNodeDataServiceAtBlockAsync(blockNumber);
            var blockContext = await GetBlockContextAtBlockAsync(blockNumber);

            return await EstimateCreationAsync(
                initCode, blockContext, dataService, from, value, gasLimit);
        }

        private async Task<CallResult> EstimateCreationAsync(
            byte[] initCode,
            BlockContext blockContext,
            IStateReader stateReader,
            string from,
            BigInteger? value,
            BigInteger? gasLimit)
        {
            var result = await CallCoreAsync(
                stateReader, blockContext, to: null, data: initCode,
                from: from, value: value, gasLimit: gasLimit,
                stateOverrides: null, authorisationList: null);

            result.RevertReason = DescribeDeployabilityFailure(
                result.RevertReason, result.ReturnData, MaxCodeSizeAt(blockContext));

            return result;
        }

        private long MaxCodeSizeAt(BlockContext blockContext)
            => ResolveHardforkConfig((long)blockContext.BlockNumber, (ulong)blockContext.Timestamp).MaxCodeSize;

        private static string DescribeDeployabilityFailure(string reason, byte[] deployedCode, long maxCodeSize)
        {
            switch (reason)
            {
                case "INVALID_EF_PREFIX":
                    return "invalid code: must not begin with 0xEF";
                case "MAX_CODE_SIZE_EXCEEDED":
                    return $"max code size exceeded: {deployedCode?.Length ?? 0} > {maxCodeSize}";
                case "CODE_DEPOSIT_OUT_OF_GAS":
                    return "out of gas: contract code deposit";
                default:
                    return reason;
            }
        }
    }
}
