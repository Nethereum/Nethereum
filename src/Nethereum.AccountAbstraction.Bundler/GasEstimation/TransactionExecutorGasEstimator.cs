using System.Numerics;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.Bundler.GasEstimation
{
    public class TransactionExecutorGasEstimator : IEvmGasEstimator
    {
        private readonly INodeDataService _nodeDataService;
        private readonly TransactionExecutor _executor;
        private readonly HardforkConfig _hardforkConfig;
        private readonly BigInteger _chainId;

        public TransactionExecutorGasEstimator(
            INodeDataService nodeDataService,
            BigInteger chainId,
            HardforkConfig hardforkConfig = null)
        {
            _nodeDataService = nodeDataService ?? throw new ArgumentNullException(nameof(nodeDataService));
            _chainId = chainId;
            _hardforkConfig = hardforkConfig ?? HardforkConfig.Default;
            _executor = new TransactionExecutor(_hardforkConfig);
        }

        public async Task<EvmEstimationResult> EstimateGasAsync(
            string from,
            string to,
            byte[] data,
            BigInteger value,
            long gasLimit)
        {
            var result = new EvmEstimationResult();

            try
            {
                var executionState = new ExecutionStateService(_nodeDataService);

                // Set up sender balance if needed
                var senderBalance = await _nodeDataService.GetBalanceAsync(from);
                if (senderBalance < value)
                {
                    senderBalance = value + (BigInteger)gasLimit * 1_000_000_000;
                }
                executionState.SetInitialChainBalance(from, senderBalance);

                var txContext = new TransactionExecutionContext
                {
                    Sender = from,
                    To = to,
                    Data = data,
                    Value = value,
                    GasLimit = gasLimit,
                    GasPrice = 1,
                    MaxFeePerGas = 1,
                    MaxPriorityFeePerGas = 0,
                    Nonce = 0,
                    IsEip1559 = true,
                    IsContractCreation = string.IsNullOrEmpty(to),
                    BlockNumber = GetBlockNumber(),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Coinbase = "0x0000000000000000000000000000000000000000",
                    BaseFee = 1,
                    Difficulty = 0,
                    BlockGasLimit = 30_000_000,
                    ChainId = _chainId,
                    ExecutionState = executionState,
                    TraceEnabled = false // No tracing needed for gas estimation
                };

                var evmResult = await _executor.ExecuteAsync(txContext);

                result.Success = evmResult.Success;
                result.GasUsed = evmResult.GasUsed;

                if (!evmResult.Success)
                {
                    result.Error = !string.IsNullOrEmpty(evmResult.RevertReason)
                        ? evmResult.RevertReason
                        : !string.IsNullOrEmpty(evmResult.Error)
                            ? evmResult.Error
                            : evmResult.ReturnData != null
                                ? $"Reverted: {evmResult.ReturnData.ToHex()}"
                                : "Execution failed";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private long GetBlockNumber()
        {
            // Return a default block number - the actual block number isn't critical for gas estimation
            return 1;
        }
    }
}
