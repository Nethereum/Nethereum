using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;

namespace Nethereum.JsonRpc.UnityClient
{
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

        public TimePreferenceFeeSuggestionUnityRequestStrategy(string url, string account, Dictionary<string, string> requestHeaders = null)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(url);
            _ethFeeHistory.RequestHeaders = requestHeaders;
            _timePreferenceFeeSuggestionStrategy = new TimePreferenceFeeSuggestionStrategy();
            _suggestTipUnityRequest = new SuggestTipUnityRequestStrategy(url, account, requestHeaders);
        }

        public IEnumerator SuggestFees()
        {
            // feeHistory API call without a reward percentile specified is cheap even with a light client backend because it only needs block headers.
            // Therefore we can afford to fetch a hundred blocks of base fee history in order to make meaningful estimates on variable time scales.
            yield return _ethFeeHistory.SendRequest(100.ToHexBigInteger(), BlockParameter.CreateLatest());
            if (_ethFeeHistory.Exception != null)
            {
                var gasUsedRatio = _ethFeeHistory.Result.GasUsedRatio;
                yield return _suggestTipUnityRequest.SuggestTip(_ethFeeHistory.Result.OldestBlock, gasUsedRatio);

                if (_suggestTipUnityRequest.Exception != null)
                {
                    this.Result =
                        _timePreferenceFeeSuggestionStrategy.SuggestFees(_ethFeeHistory.Result,
                            _suggestTipUnityRequest.Result);
                }
                else
                {
                    this.Exception = _suggestTipUnityRequest.Exception;
                    yield break;
                }
            }
            else
            {
                this.Exception = _ethFeeHistory.Exception;
                yield break;
            }

        }
    }
}