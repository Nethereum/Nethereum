using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Fee1559Suggestions;

namespace Nethereum.JsonRpc.UnityClient
{
    public class SimpleFeeSuggestionUnityRequestStrategy : UnityRequest<Fee1559>, IFee1559SuggestionUnityRequestStrategy
    {
        private EthGetBlockWithTransactionsHashesByNumberUnityRequest _ethGetBlockWithTransactionsHashes;
        public static BigInteger DEFAULT_MAX_PRIORITY_FEE_PER_GAS = 2000000000;

        public SimpleFeeSuggestionUnityRequestStrategy(string url, string account, Dictionary<string, string> requestHeaders = null)
        {
            _ethGetBlockWithTransactionsHashes = new EthGetBlockWithTransactionsHashesByNumberUnityRequest(url);
            _ethGetBlockWithTransactionsHashes.RequestHeaders = requestHeaders;
        }

        public IEnumerator SuggestFee(BigInteger? maxPriorityFeePerGas = null)
        {
            if (maxPriorityFeePerGas == null) maxPriorityFeePerGas = DEFAULT_MAX_PRIORITY_FEE_PER_GAS;

            yield return _ethGetBlockWithTransactionsHashes.SendRequest(BlockParameter.CreateLatest());

            if (_ethGetBlockWithTransactionsHashes.Exception == null)
            {
                var lastBlock = _ethGetBlockWithTransactionsHashes.Result;
                var baseFee = lastBlock.BaseFeePerGas;
                var maxFeePerGas = baseFee.Value * 2 + maxPriorityFeePerGas;
                this.Result =  new Fee1559()
                {
                    BaseFee = baseFee,
                    MaxPriorityFeePerGas = maxPriorityFeePerGas,
                    MaxFeePerGas = maxFeePerGas
                };
            }
            else
            {
                this.Exception = _ethGetBlockWithTransactionsHashes.Exception;
                yield break;
            }
        }
    }


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