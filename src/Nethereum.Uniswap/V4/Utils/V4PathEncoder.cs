using Nethereum.Uniswap.V4.Pricing.V4Quoter.ContractDefinition;
using System;
using System.Collections.Generic;

namespace Nethereum.Uniswap.V4.Utils
{
    public class V4PathEncoder
    {
        public static List<PathKey> EncodeMultihopExactInPath(List<PoolKey> poolKeys, string currencyIn)
        {
            var pathKeys = new List<PathKey>();

            foreach (var poolKey in poolKeys)
            {
                var currencyOut = currencyIn.Equals(poolKey.Currency0, StringComparison.OrdinalIgnoreCase)
                    ? poolKey.Currency1
                    : poolKey.Currency0;

                pathKeys.Add(new PathKey
                {
                    IntermediateCurrency = currencyOut,
                    Fee = poolKey.Fee,
                    TickSpacing = poolKey.TickSpacing,
                    Hooks = poolKey.Hooks,
                    HookData = new byte[] { }
                });

                currencyIn = currencyOut;
            }

            return pathKeys;
        }

        public static List<PathKey> EncodeMultihopExactOutPath(List<PoolKey> poolKeys, string currencyOut)
        {
            var pathKeys = new List<PathKey>();

            for (var i = poolKeys.Count - 1; i >= 0; i--)
            {
                var poolKey = poolKeys[i];
                var currencyIn = currencyOut.Equals(poolKey.Currency0, StringComparison.OrdinalIgnoreCase)
                    ? poolKey.Currency1
                    : poolKey.Currency0;

                pathKeys.Insert(0, new PathKey
                {
                    IntermediateCurrency = currencyIn,
                    Fee = poolKey.Fee,
                    TickSpacing = poolKey.TickSpacing,
                    Hooks = poolKey.Hooks,
                    HookData = new byte[] { }
                });

                currencyOut = currencyIn;
            }

            return pathKeys;
        }
    }
}
