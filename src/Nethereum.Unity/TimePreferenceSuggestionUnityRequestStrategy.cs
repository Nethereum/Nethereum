using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;

namespace Nethereum.JsonRpc.UnityClient
{
    public class MedianPriorityFeeHistorySuggestionUnityRequestStrategy: UnityRequest<Fee1559>
    {
        private readonly MedianPriorityFeeHistorySuggestionStrategy _medianPriorityFeeHistorySuggestionStrategy;
        private readonly EthFeeHistoryUnityRequest _ethFeeHistory;
        private readonly EthGetBlockWithTransactionsHashesByNumberUnityRequest _ethGetBlockWithTransactionsHashes;

        public MedianPriorityFeeHistorySuggestionUnityRequestStrategy(string url, Dictionary<string, string> requestHeaders = null)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(url);
            _ethFeeHistory.RequestHeaders = requestHeaders;
            _ethGetBlockWithTransactionsHashes = new EthGetBlockWithTransactionsHashesByNumberUnityRequest(url);
            _ethGetBlockWithTransactionsHashes.RequestHeaders = requestHeaders;
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
                    this.Result = MedianPriorityFeeHistorySuggestionStrategy.FallbackFeeSuggestion;
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
                            yield return _ethFeeHistory.SendRequest(new HexBigInteger(MedianPriorityFeeHistorySuggestionStrategy.FeeHistoryNumberOfBlocks), new BlockParameter(lastBlock.Number), new double[] {
                                                                        MedianPriorityFeeHistorySuggestionStrategy.FEE_HISTORY_PERCENTILE });
                            if (_ethFeeHistory.Exception != null)
                            {
                                this.Exception = _ethFeeHistory.Exception;
                                yield break;
                            }
                            else
                            {
                                estimatedPriorityFee = _medianPriorityFeeHistorySuggestionStrategy.EstimatePriorityFee(_ethFeeHistory.Result);
                            }
                        }

                        if (estimatedPriorityFee == null)
                        {
                            this.Result = MedianPriorityFeeHistorySuggestionStrategy.FallbackFeeSuggestion;
                            yield break;
                        }

                        maxPriorityFeePerGas = BigInteger.Max(estimatedPriorityFee.Value, MedianPriorityFeeHistorySuggestionStrategy.DefaultPriorityFee);
                    }

                    this.Result = _medianPriorityFeeHistorySuggestionStrategy.SuggestMaxFeeUsingMultiplier(maxPriorityFeePerGas, baseFee);
                    yield break;
                }
            }
            else
            {
                this.Exception = _ethGetBlockWithTransactionsHashes.Exception;
                yield break;
            }
        }

    }

    public class TimePreferenceFeeSuggestionUnityRequestStrategy : UnityRequest<Fee1559[]>
    {
        private readonly EthFeeHistoryUnityRequest _ethFeeHistory;
        private readonly TimePreferenceFeeSuggestionStrategy _timePreferenceFeeSuggestionStrategy;
        private readonly SuggestTipUnityRequestStrategy _suggestTipUnityRequest;
        public double SampleMin
        {
            get => _timePreferenceFeeSuggestionStrategy.SampleMin;
            set => _timePreferenceFeeSuggestionStrategy.SampleMin = value;
        }

        public double SampleMax
        {
            get => _timePreferenceFeeSuggestionStrategy.SampleMax;
            set => _timePreferenceFeeSuggestionStrategy.SampleMax = value;
        }

        public int MaxTimeFactor
        {
            get => _timePreferenceFeeSuggestionStrategy.MaxTimeFactor;
            set => _timePreferenceFeeSuggestionStrategy.MaxTimeFactor = value;
        }

        public double ExtraTipRatio
        {
            get => _timePreferenceFeeSuggestionStrategy.ExtraTipRatio;
            set => _timePreferenceFeeSuggestionStrategy.ExtraTipRatio = value;
        }

        public BigInteger FallbackTip
        {
            get => _suggestTipUnityRequest.FallbackTip;
            set => _suggestTipUnityRequest.FallbackTip = value;
        }

        public TimePreferenceFeeSuggestionUnityRequestStrategy(string url, Dictionary<string, string> requestHeaders = null)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(url);
            _ethFeeHistory.RequestHeaders = requestHeaders;
            _timePreferenceFeeSuggestionStrategy = new TimePreferenceFeeSuggestionStrategy();
            _suggestTipUnityRequest = new SuggestTipUnityRequestStrategy(url, requestHeaders);
        }

        public IEnumerator SuggestFees()
        {
            // feeHistory API call without a reward percentile specified is cheap even with a light client backend because it only needs block headers.
            // Therefore we can afford to fetch a hundred blocks of base fee history in order to make meaningful estimates on variable time scales.
            yield return _ethFeeHistory.SendRequest(100.ToHexBigInteger(), BlockParameter.CreateLatest());
            if (_ethFeeHistory.Exception != null)
            {
                this.Exception = _ethFeeHistory.Exception;
                yield break;
            }
            else
            {
                var gasUsedRatio = _ethFeeHistory.Result.GasUsedRatio;
                yield return _suggestTipUnityRequest.SuggestTip(_ethFeeHistory.Result.OldestBlock, gasUsedRatio);

                if (_suggestTipUnityRequest.Exception != null)
                {
                    this.Exception = _suggestTipUnityRequest.Exception;
                }
                else
                {
                    this.Result =
                       _timePreferenceFeeSuggestionStrategy.SuggestFees(_ethFeeHistory.Result,
                           _suggestTipUnityRequest.Result);
                }
                yield break; 
            }

        }
    }
}