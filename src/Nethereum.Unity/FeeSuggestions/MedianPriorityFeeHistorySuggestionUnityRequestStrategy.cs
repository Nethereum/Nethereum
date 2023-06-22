using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;
using Nethereum.Unity.Rpc;
using Newtonsoft.Json;

namespace Nethereum.Unity.FeeSuggestions
{
    public class MedianPriorityFeeHistorySuggestionUnityRequestStrategy : UnityRequest<Fee1559>, IFee1559SuggestionUnityRequestStrategy
    {
        private readonly MedianPriorityFeeHistorySuggestionStrategy _medianPriorityFeeHistorySuggestionStrategy;
        private readonly EthFeeHistoryUnityRequest _ethFeeHistory;
        private readonly EthGetBlockWithTransactionsHashesByNumberUnityRequest _ethGetBlockWithTransactionsHashes;

        public MedianPriorityFeeHistorySuggestionUnityRequestStrategy(string url, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(url, jsonSerializerSettings, requestHeaders);
            _ethGetBlockWithTransactionsHashes = new EthGetBlockWithTransactionsHashesByNumberUnityRequest(url, jsonSerializerSettings, requestHeaders);
            _medianPriorityFeeHistorySuggestionStrategy = new MedianPriorityFeeHistorySuggestionStrategy();
        }

        public MedianPriorityFeeHistorySuggestionUnityRequestStrategy(IUnityRpcRequestClientFactory unityRpcClientFactory)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(unityRpcClientFactory);
            _ethGetBlockWithTransactionsHashes = new EthGetBlockWithTransactionsHashesByNumberUnityRequest(unityRpcClientFactory);
            _medianPriorityFeeHistorySuggestionStrategy = new MedianPriorityFeeHistorySuggestionStrategy();
        }
        public IEnumerator SuggestFee(BigInteger? maxPriorityFeePerGas = null)
        {
            yield return _ethGetBlockWithTransactionsHashes.SendRequest(BlockParameter.CreateLatest());

            if (_ethGetBlockWithTransactionsHashes.Exception == null)
            {
                var lastBlock = _ethGetBlockWithTransactionsHashes.Result;
                if (lastBlock.BaseFeePerGas == null)
                {
                    Result = MedianPriorityFeeHistorySuggestionStrategy.FallbackFeeSuggestion;
                    yield break;
                }
                else
                {
                    var baseFee = lastBlock.BaseFeePerGas;

                    if (maxPriorityFeePerGas == null)
                    {
                        BigInteger? estimatedPriorityFee;
                        if (baseFee.Value < MedianPriorityFeeHistorySuggestionStrategy.PRIORITY_FEE_ESTIMATION_TRIGGER)
                        {
                            estimatedPriorityFee = MedianPriorityFeeHistorySuggestionStrategy.DefaultPriorityFee;
                        }
                        else
                        {
                            yield return _ethFeeHistory.SendRequest(new HexBigInteger(MedianPriorityFeeHistorySuggestionStrategy.FeeHistoryNumberOfBlocks), new BlockParameter(lastBlock.Number), new decimal[] {
                                                                        MedianPriorityFeeHistorySuggestionStrategy.FEE_HISTORY_PERCENTILE });
                            if (_ethFeeHistory.Exception != null)
                            {
                                Exception = _ethFeeHistory.Exception;
                                yield break;
                            }
                            else
                            {
                                estimatedPriorityFee = _medianPriorityFeeHistorySuggestionStrategy.EstimatePriorityFee(_ethFeeHistory.Result);
                            }
                        }

                        if (estimatedPriorityFee == null)
                        {
                            Result = MedianPriorityFeeHistorySuggestionStrategy.FallbackFeeSuggestion;
                            yield break;
                        }

                        maxPriorityFeePerGas = BigInteger.Max(estimatedPriorityFee.Value, MedianPriorityFeeHistorySuggestionStrategy.DefaultPriorityFee);
                    }

                    Result = _medianPriorityFeeHistorySuggestionStrategy.SuggestMaxFeeUsingMultiplier(maxPriorityFeePerGas, baseFee);
                    yield break;
                }
            }
            else
            {
                Exception = _ethGetBlockWithTransactionsHashes.Exception;
                yield break;
            }
        }

    }
}