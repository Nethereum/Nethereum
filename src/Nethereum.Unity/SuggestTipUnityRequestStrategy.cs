using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;

namespace Nethereum.JsonRpc.UnityClient
{
    public class SuggestTipUnityRequestStrategy : UnityRequest<BigInteger>
    {
        private readonly EthFeeHistoryUnityRequest _ethFeeHistory;
        private readonly TimePreferenceSuggestionStrategy _timePreferenceSuggestionStrategy;
        public BigInteger FallbackTip { get; set; }
        
        public SuggestTipUnityRequestStrategy(string url, string account, Dictionary<string, string> requestHeaders = null)
        {
            _ethFeeHistory = new EthFeeHistoryUnityRequest(url);
            _ethFeeHistory.RequestHeaders = requestHeaders;
            _timePreferenceSuggestionStrategy = new TimePreferenceSuggestionStrategy();
        }

        public IEnumerator SuggestTip(BigInteger firstBlock, decimal[] gasUsedRatio)
        {
            var ptr = gasUsedRatio.Length - 1;
            var needBlocks = 5;
            var rewards = new List<BigInteger>();
            while (needBlocks > 0 && ptr >= 0)
            {
                var blockCount = _timePreferenceSuggestionStrategy.MaxBlockCount(gasUsedRatio, ptr, needBlocks);
                if (blockCount > 0)
                {
                    // feeHistory API call with reward percentile specified is expensive and therefore is only requested for a few non-full recent blocks.
                    yield return _ethFeeHistory.SendRequest((uint)blockCount, new BlockParameter(new HexBigInteger(firstBlock + ptr)), new int[] { 10 });

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
                        this.Exception = _ethFeeHistory.Exception;
                        yield break;
                    }

                    needBlocks -= blockCount;
                }
                ptr -= blockCount + 1;
            }

            if (rewards.Count == 0)
            {
                this.Result = FallbackTip;
            }
            rewards.Sort();
            this.Result = rewards[(int)Math.Truncate((double)(rewards.Count / 2))];
        }
    }
}