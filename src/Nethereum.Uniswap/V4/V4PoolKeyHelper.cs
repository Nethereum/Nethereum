using System;
using Nethereum.Hex.HexConvertors.Extensions;
using PositionPoolKey = Nethereum.Uniswap.V4.PositionManager.ContractDefinition.PoolKey;
using QuoterPoolKey = Nethereum.Uniswap.V4.V4Quoter.ContractDefinition.PoolKey;
using Nethereum.Util;

namespace Nethereum.Uniswap.V4
{
    public static class V4PoolKeyHelper
    {

        private class OrderPairResult
        {
            public OrderPairResult(string currency0, string currency1)
            {
                Currency0 = currency0;
                Currency1 = currency1;
            }

            public string Currency0 { get; set; }
            public string Currency1 { get; set; }
        }

        public static PositionPoolKey CreateNormalized(string currencyA, string currencyB, int fee, int tickSpacing, string hooks = null)
        {
            if (string.IsNullOrWhiteSpace(currencyA)) throw new ArgumentNullException(nameof(currencyA));
            if (string.IsNullOrWhiteSpace(currencyB)) throw new ArgumentNullException(nameof(currencyB));

            var addressUtil = AddressUtil.Current;

            var checksumA = addressUtil.ConvertToChecksumAddress(currencyA);
            var checksumB = addressUtil.ConvertToChecksumAddress(currencyB);

            var orderPairResult = OrderPair(checksumA, checksumB);

            var normalizedHooks = string.IsNullOrEmpty(hooks)
                ? AddressUtil.ZERO_ADDRESS
                : addressUtil.ConvertToChecksumAddress(hooks);

            return new PositionPoolKey
            {
                Currency0 = orderPairResult.Currency0,
                Currency1 = orderPairResult.Currency1,
                Fee = (uint)fee,
                TickSpacing = tickSpacing,
                Hooks = normalizedHooks
            };
        }

        public static QuoterPoolKey CreateNormalizedForQuoter(string currencyA, string currencyB, int fee, int tickSpacing, string hooks = null)
        {
            var normalized = CreateNormalized(currencyA, currencyB, fee, tickSpacing, hooks);
            return ToQuoterPoolKey(normalized);
        }

        public static PositionPoolKey Normalize(PositionPoolKey poolKey)
        {
            if (poolKey == null) throw new ArgumentNullException(nameof(poolKey));
            return CreateNormalized(poolKey.Currency0, poolKey.Currency1, (int)poolKey.Fee, poolKey.TickSpacing, poolKey.Hooks);
        }

        public static QuoterPoolKey ToQuoterPoolKey(PositionPoolKey poolKey)
        {
            var normalized = Normalize(poolKey);
            return new QuoterPoolKey
            {
                Currency0 = normalized.Currency0,
                Currency1 = normalized.Currency1,
                Fee = normalized.Fee,
                TickSpacing = normalized.TickSpacing,
                Hooks = normalized.Hooks
            };
        }

        private static OrderPairResult OrderPair(string currencyA, string currencyB)
        {
            var bytesA = currencyA.HexToByteArray();
            var bytesB = currencyB.HexToByteArray();

            var length = Math.Min(bytesA.Length, bytesB.Length);
            for (var i = 0; i < length; i++)
            {
                if (bytesA[i] == bytesB[i])
                {
                    continue;
                }

                return bytesA[i] > bytesB[i]
                    ? new OrderPairResult(currencyB, currencyA)
                    : new OrderPairResult(currencyA, currencyB);
            }

            return string.CompareOrdinal(currencyA, currencyB) <= 0
                ? 
                  new OrderPairResult(currencyA, currencyB)
                : new OrderPairResult(currencyB, currencyA);
        }
    }
}
