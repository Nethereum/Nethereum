using System;
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
    public class SuggestTipUnityRequestStrategy : UnityRequest<BigInteger>
    {
        private readonly EthFeeHistoryUnityRequest _ethFeeHistory;
        private readonly TimePreferenceFeeSuggestionStrategy _timePreferenceFeeSuggestionStrategy;
        public BigInteger FallbackTip { get; set; } = 2000000000;


        public SuggestTipUnityRequestStrategy(string url, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(url, jsonSerializerSettings, requestHeaders);
            _timePreferenceFeeSuggestionStrategy = new TimePreferenceFeeSuggestionStrategy();
        }

        public SuggestTipUnityRequestStrategy(IUnityRpcRequestClientFactory unityRpcClientFactory)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(unityRpcClientFactory);
            _timePreferenceFeeSuggestionStrategy = new TimePreferenceFeeSuggestionStrategy();
        }

        public IEnumerator SuggestTip(BigInteger firstBlock, decimal[] gasUsedRatio)
        {
            var ptr = gasUsedRatio.Length - 1;
            var needBlocks = 5;
            var rewards = new List<BigInteger>();
            while (needBlocks > 0 && ptr >= 0)
            {
                var blockCount = _timePreferenceFeeSuggestionStrategy.MaxBlockCount(gasUsedRatio, ptr, needBlocks);
                if (blockCount > 0)
                {
                    // feeHistory API call with reward percentile specified is expensive and therefore is only requested for a few non-full recent blocks.
                    yield return _ethFeeHistory.SendRequest(blockCount.ToHexBigInteger(), new BlockParameter(new HexBigInteger(firstBlock + ptr)), new double[] { 0 });

                    if (_ethFeeHistory.Exception == null)
                    {

                        for (var i = 0; i < _ethFeeHistory.Result.Reward.Length; i++)
                        {
                            rewards.Add(_ethFeeHistory.Result.Reward[i][0]);
                        }

                        if (_ethFeeHistory.Result.Reward.Length < blockCount)
                        {
                            break;
                        }
                    }
                    else
                    {
                        Exception = _ethFeeHistory.Exception;
                        yield break;
                    }

                    needBlocks -= blockCount;
                }
                ptr -= blockCount + 1;
            }

            if (rewards.Count == 0)
            {
                Result = FallbackTip;
            }
            else
            {
                rewards.Sort();
                Result = rewards[(int)Math.Truncate((double)(rewards.Count / 2))];
            }
        }
    }
}