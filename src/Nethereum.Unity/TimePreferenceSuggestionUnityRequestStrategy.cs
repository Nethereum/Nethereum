using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;

namespace Nethereum.JsonRpc.UnityClient
{
    public class TimePreferenceSuggestionUnityRequestStrategy : UnityRequest<Fee1559[]>
    {
        private readonly EthFeeHistoryUnityRequest _ethFeeHistory;
        private readonly TimePreferenceSuggestionStrategy _timePreferenceSuggestionStrategy;
        private readonly SuggestTipUnityRequestStrategy _suggestTipUnityRequest;
        public double SampleMin
        {
            get => _timePreferenceSuggestionStrategy.SampleMin;
            set => _timePreferenceSuggestionStrategy.SampleMin = value;
        }

        public double SampleMax
        {
            get => _timePreferenceSuggestionStrategy.SampleMax;
            set => _timePreferenceSuggestionStrategy.SampleMax = value;
        }

        public int MaxTimeFactor
        {
            get => _timePreferenceSuggestionStrategy.MaxTimeFactor;
            set => _timePreferenceSuggestionStrategy.MaxTimeFactor = value;
        }

        public double ExtraTipRatio
        {
            get => _timePreferenceSuggestionStrategy.ExtraTipRatio;
            set => _timePreferenceSuggestionStrategy.ExtraTipRatio = value;
        }

        public BigInteger FallbackTip
        {
            get => _suggestTipUnityRequest.FallbackTip;
            set => _suggestTipUnityRequest.FallbackTip = value;
        }

        public TimePreferenceSuggestionUnityRequestStrategy(string url, string account, Dictionary<string, string> requestHeaders = null)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(url);
            _ethFeeHistory.RequestHeaders = requestHeaders;
            _timePreferenceSuggestionStrategy = new TimePreferenceSuggestionStrategy();
            _suggestTipUnityRequest = new SuggestTipUnityRequestStrategy(url, account, requestHeaders);
        }

        public IEnumerator SuggestFees()
        {
            // feeHistory API call without a reward percentile specified is cheap even with a light client backend because it only needs block headers.
            // Therefore we can afford to fetch a hundred blocks of base fee history in order to make meaningful estimates on variable time scales.
            yield return _ethFeeHistory.SendRequest(100, BlockParameter.CreateLatest());
            if (_ethFeeHistory.Exception != null)
            {
                var gasUsedRatio = _ethFeeHistory.Result.GasUsedRatio;
                yield return _suggestTipUnityRequest.SuggestTip(_ethFeeHistory.Result.OldestBlock, gasUsedRatio);

                if (_suggestTipUnityRequest.Exception != null)
                {
                    this.Result =
                        _timePreferenceSuggestionStrategy.SuggestFees(_ethFeeHistory.Result,
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