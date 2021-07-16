using System.Collections;
using System.Collections.Generic;
using System.Numerics;
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
}