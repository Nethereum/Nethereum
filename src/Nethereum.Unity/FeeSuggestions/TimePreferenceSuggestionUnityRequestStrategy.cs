using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.UnityClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;
using Newtonsoft.Json;

namespace Nethereum.Unity.FeeSuggestions
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

        public TimePreferenceFeeSuggestionUnityRequestStrategy(string url, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(url, jsonSerializerSettings, requestHeaders);
            _timePreferenceFeeSuggestionStrategy = new TimePreferenceFeeSuggestionStrategy();
            _suggestTipUnityRequest = new SuggestTipUnityRequestStrategy(url, jsonSerializerSettings, requestHeaders);
        }

        public IEnumerator SuggestFees()
        {
            // feeHistory API call without a reward percentile specified is cheap even with a light client backend because it only needs block headers.
            // Therefore we can afford to fetch a hundred blocks of base fee history in order to make meaningful estimates on variable time scales.
            yield return _ethFeeHistory.SendRequest(100.ToHexBigInteger(), BlockParameter.CreateLatest());
            if (_ethFeeHistory.Exception != null)
            {
                Exception = _ethFeeHistory.Exception;
                yield break;
            }
            else
            {
                var gasUsedRatio = _ethFeeHistory.Result.GasUsedRatio;
                yield return _suggestTipUnityRequest.SuggestTip(_ethFeeHistory.Result.OldestBlock, gasUsedRatio);

                if (_suggestTipUnityRequest.Exception != null)
                {
                    Exception = _suggestTipUnityRequest.Exception;
                }
                else
                {
                    Result =
                       _timePreferenceFeeSuggestionStrategy.SuggestFees(_ethFeeHistory.Result,
                           _suggestTipUnityRequest.Result);
                }
                yield break;
            }

        }
    }
}