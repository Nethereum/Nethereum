using Nethereum.Uniswap.V4.V4Quoter;
using Nethereum.Uniswap.V4.V4Quoter.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PoolKey = Nethereum.Uniswap.V4.V4Quoter.ContractDefinition.PoolKey;

namespace Nethereum.Uniswap.V4
{
    public class SwapPathResult
    {
        public List<PoolKey> Path { get; set; }
        public BigInteger AmountOut { get; set; }
        public BigInteger GasEstimate { get; set; }
        public decimal PriceImpact { get; set; }
        public int[] Fees { get; set; }
    }

    public class V4BestPathFinder
    {
        private readonly IWeb3 _web3;
        private readonly string _quoterAddress;
        private readonly V4PoolCache _poolCache;

        public V4BestPathFinder(
            IWeb3 web3,
            string quoterAddress,
            V4PoolCache poolCache)
        {
            _web3 = web3;
            _quoterAddress = quoterAddress;
            _poolCache = poolCache;
        }

        public async Task<SwapPathResult> FindBestDirectPathAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            int[] feeTiers = null,
            int[] tickSpacings = null,
            IEnumerable<PoolCacheEntry> candidatePools = null)
        {
            tokenIn = NormalizeAddress(tokenIn);
            tokenOut = NormalizeAddress(tokenOut);

            var quoter = new V4QuoterService(_web3, _quoterAddress);
            SwapPathResult bestPath = null;
            BigInteger bestAmountOut = 0;

            if (candidatePools != null)
            {
                var lookup = BuildCandidateLookup(candidatePools);
                var key = CreateTokenPairKey(tokenIn, tokenOut);

                if (!lookup.TryGetValue(key, out var poolsForPair))
                {
                    return null;
                }

                foreach (var candidate in poolsForPair)
                {
                    if (!candidate.Exists)
                    {
                        continue;
                    }

                    var poolKey = CreateQuoterPoolKeyFromEntry(candidate);
                    var pathKeys = V4PathEncoder.EncodeMultihopExactInPath(
                        new List<PoolKey> { poolKey },
                        tokenIn);

                    var quoteParams = new QuoteExactParams
                    {
                        Path = pathKeys,
                        ExactAmount = amountIn,
                        ExactCurrency = tokenIn
                    };

                    try
                    {
                        var quote = await quoter.QuoteExactInputQueryAsync(quoteParams).ConfigureAwait(false);
                        if (quote.AmountOut > bestAmountOut)
                        {
                            bestAmountOut = quote.AmountOut;
                            bestPath = new SwapPathResult
                            {
                                Path = new List<PoolKey> { poolKey },
                                AmountOut = quote.AmountOut,
                                GasEstimate = quote.GasEstimate,
                                Fees = new int[] { candidate.Fee }
                            };
                        }
                    }
                    catch
                    {
                        // Skip transient failures and continue evaluating the remaining candidates.
                    }
                }

                return bestPath;
            }

            if (feeTiers == null)
            {
                feeTiers = new int[] { 100, 500, 3000, 10000 };
            }
            if (tickSpacings == null)
            {
                tickSpacings = new int[] { 1, 10, 60, 200 };
            }

            foreach (var fee in feeTiers)
            {
                foreach (var tickSpacing in tickSpacings)
                {
                    try
                    {
                        var pool = await _poolCache.GetOrFetchPoolAsync(
                            tokenIn,
                            tokenOut,
                            fee,
                            tickSpacing).ConfigureAwait(false);

                        if (!pool.Exists)
                        {
                            continue;
                        }

                        var poolKey = CreateQuoterPoolKeyFromEntry(pool);

                        var pathKeys = V4PathEncoder.EncodeMultihopExactInPath(
                            new List<PoolKey> { poolKey },
                            tokenIn);

                        var quoteParams = new QuoteExactParams
                        {
                            Path = pathKeys,
                            ExactAmount = amountIn,
                            ExactCurrency = tokenIn
                        };

                        var quote = await quoter.QuoteExactInputQueryAsync(quoteParams).ConfigureAwait(false);

                        if (quote.AmountOut > bestAmountOut)
                        {
                            bestAmountOut = quote.AmountOut;
                            bestPath = new SwapPathResult
                            {
                                Path = new List<PoolKey> { poolKey },
                                AmountOut = quote.AmountOut,
                                GasEstimate = quote.GasEstimate,
                                Fees = new int[] { fee }
                            };
                        }
                    }
                    catch
                    {
                        // Ignore failed combinations and continue searching.
                    }
                }
            }

            return bestPath;
        }

        public async Task<SwapPathResult> FindBestMultihopPathAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            string[] intermediateTokens,
            int maxHops = 3,
            IEnumerable<PoolCacheEntry> candidatePools = null)
        {
            tokenIn = NormalizeAddress(tokenIn);
            tokenOut = NormalizeAddress(tokenOut);

            if (intermediateTokens == null || intermediateTokens.Length == 0 || maxHops <= 1)
            {
                return null;
            }

            var normalizedIntermediates = NormalizeTokenArray(intermediateTokens);

            if (candidatePools != null)
            {
                var lookup = BuildCandidateLookup(candidatePools);
                return await FindBestMultihopPathFromCandidates(
                    tokenIn,
                    tokenOut,
                    amountIn,
                    normalizedIntermediates,
                    maxHops,
                    lookup).ConfigureAwait(false);
            }

            var routes = EnumerateTokenRoutes(tokenIn, tokenOut, normalizedIntermediates, maxHops)
                .Where(route => route.Count > 2)
                .ToList();

            if (!routes.Any())
            {
                return null;
            }

            var quoter = new V4QuoterService(_web3, _quoterAddress);
            SwapPathResult bestPath = null;
            BigInteger bestAmountOut = 0;

            var commonFees = new int[] { 500, 3000, 10000 };
            var commonTickSpacings = new int[] { 10, 60, 200 };

            foreach (var route in routes)
            {
                var hopCount = route.Count - 1;

                async Task TraverseAsync(int hopIndex, List<PoolKey> currentPools, List<int> currentFees)
                {
                    if (hopIndex == hopCount)
                    {
                        try
                        {
                            var pathKeys = V4PathEncoder.EncodeMultihopExactInPath(new List<PoolKey>(currentPools), tokenIn);
                            var quoteParams = new QuoteExactParams
                            {
                                Path = pathKeys,
                                ExactAmount = amountIn,
                                ExactCurrency = tokenIn
                            };

                            var quote = await quoter.QuoteExactInputQueryAsync(quoteParams).ConfigureAwait(false);

                            if (quote.AmountOut > bestAmountOut)
                            {
                                bestAmountOut = quote.AmountOut;
                                bestPath = new SwapPathResult
                                {
                                    Path = new List<PoolKey>(currentPools),
                                    AmountOut = quote.AmountOut,
                                    GasEstimate = quote.GasEstimate,
                                    Fees = currentFees.ToArray()
                                };
                            }
                        }
                        catch
                        {
                            // Ignore this attempted path – continue searching.
                        }

                        return;
                    }

                    var fromToken = route[hopIndex];
                    var toToken = route[hopIndex + 1];

                    foreach (var fee in commonFees)
                    {
                        foreach (var tickSpacing in commonTickSpacings)
                        {
                            try
                            {
                                var pool = await _poolCache.GetOrFetchPoolAsync(
                                    fromToken,
                                    toToken,
                                    fee,
                                    tickSpacing).ConfigureAwait(false);

                                if (!pool.Exists)
                                {
                                    continue;
                                }

                                var poolKey = CreateQuoterPoolKeyFromEntry(pool);

                                currentPools.Add(poolKey);
                                currentFees.Add(pool.Fee);

                                await TraverseAsync(hopIndex + 1, currentPools, currentFees).ConfigureAwait(false);

                                currentPools.RemoveAt(currentPools.Count - 1);
                                currentFees.RemoveAt(currentFees.Count - 1);
                            }
                            catch
                            {
                                // Skip this pool combination and continue exploring others.
                            }
                        }
                    }
                }

                await TraverseAsync(0, new List<PoolKey>(), new List<int>()).ConfigureAwait(false);
            }

            return bestPath;
        }

        public async Task<SwapPathResult> FindBestPathAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            string[] intermediateTokens = null,
            int maxHops = 3,
            IEnumerable<PoolCacheEntry> candidatePools = null)
        {
            var directPath = await FindBestDirectPathAsync(
                tokenIn,
                tokenOut,
                amountIn,
                candidatePools: candidatePools).ConfigureAwait(false);

            if (intermediateTokens == null || intermediateTokens.Length == 0)
            {
                return directPath;
            }

            var multihopPath = await FindBestMultihopPathAsync(
                tokenIn,
                tokenOut,
                amountIn,
                intermediateTokens,
                maxHops,
                candidatePools).ConfigureAwait(false);

            if (directPath == null)
            {
                return multihopPath;
            }

            if (multihopPath == null)
            {
                return directPath;
            }

            return multihopPath.AmountOut > directPath.AmountOut ? multihopPath : directPath;
        }

        private async Task<SwapPathResult> FindBestMultihopPathFromCandidates(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            string[] intermediateTokens,
            int maxHops,
            Dictionary<string, List<PoolCacheEntry>> candidateLookup)
        {
            var routes = EnumerateTokenRoutes(tokenIn, tokenOut, intermediateTokens, maxHops)
                .Where(route => route.Count > 2)
                .ToList();

            if (!routes.Any())
            {
                return null;
            }

            var quoter = new V4QuoterService(_web3, _quoterAddress);
            SwapPathResult bestPath = null;
            BigInteger bestAmountOut = 0;

            foreach (var route in routes)
            {
                var hopCount = route.Count - 1;

                async Task TraverseAsync(int hopIndex, List<PoolKey> currentPools, List<int> currentFees)
                {
                    if (hopIndex == hopCount)
                    {
                        try
                        {
                            var pathKeys = V4PathEncoder.EncodeMultihopExactInPath(new List<PoolKey>(currentPools), tokenIn);
                            var quoteParams = new QuoteExactParams
                            {
                                Path = pathKeys,
                                ExactAmount = amountIn,
                                ExactCurrency = tokenIn
                            };

                            var quote = await quoter.QuoteExactInputQueryAsync(quoteParams).ConfigureAwait(false);

                            if (quote.AmountOut > bestAmountOut)
                            {
                                bestAmountOut = quote.AmountOut;
                                bestPath = new SwapPathResult
                                {
                                    Path = new List<PoolKey>(currentPools),
                                    AmountOut = quote.AmountOut,
                                    GasEstimate = quote.GasEstimate,
                                    Fees = currentFees.ToArray()
                                };
                            }
                        }
                        catch
                        {
                            // Ignore this attempted path – continue searching.
                        }

                        return;
                    }

                    var fromToken = route[hopIndex];
                    var toToken = route[hopIndex + 1];
                    var edgeKey = CreateTokenPairKey(fromToken, toToken);

                    if (!candidateLookup.TryGetValue(edgeKey, out var poolsForEdge))
                    {
                        return;
                    }

                    foreach (var candidate in poolsForEdge)
                    {
                        if (!candidate.Exists)
                        {
                            continue;
                        }

                        var poolKey = CreateQuoterPoolKeyFromEntry(candidate);

                        currentPools.Add(poolKey);
                        currentFees.Add(candidate.Fee);

                        await TraverseAsync(hopIndex + 1, currentPools, currentFees).ConfigureAwait(false);

                        currentPools.RemoveAt(currentPools.Count - 1);
                        currentFees.RemoveAt(currentFees.Count - 1);
                    }
                }

                await TraverseAsync(0, new List<PoolKey>(), new List<int>()).ConfigureAwait(false);
            }

            return bestPath;
        }

        private static IEnumerable<List<string>> EnumerateTokenRoutes(
            string tokenIn,
            string tokenOut,
            string[] intermediateTokens,
            int maxHops)
        {
            if (maxHops < 1)
            {
                yield break;
            }

            var uniqueIntermediates = (intermediateTokens ?? Array.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(t => !t.Equals(tokenIn, StringComparison.OrdinalIgnoreCase) && !t.Equals(tokenOut, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var maxIntermediateDepth = Math.Max(0, maxHops - 1);

            IEnumerable<List<string>> Build(List<string> current, HashSet<string> used, int remainingDepth)
            {
                yield return new List<string>(current);

                if (remainingDepth == 0)
                {
                    yield break;
                }

                foreach (var candidate in uniqueIntermediates)
                {
                    if (used.Contains(candidate))
                    {
                        continue;
                    }

                    used.Add(candidate);
                    current.Add(candidate);

                    foreach (var result in Build(current, used, remainingDepth - 1))
                    {
                        yield return result;
                    }

                    current.RemoveAt(current.Count - 1);
                    used.Remove(candidate);
                }
            }

            var usedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tokenIn };

            foreach (var prefix in Build(new List<string> { tokenIn }, usedTokens, maxIntermediateDepth))
            {
                var route = new List<string>(prefix) { tokenOut };

                if (route.Count >= 2 && route.Count - 1 <= maxHops)
                {
                    yield return route;
                }
            }
        }

        private static string NormalizeAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return address;
            }

            if (address.Equals(AddressUtil.ZERO_ADDRESS, StringComparison.OrdinalIgnoreCase))
            {
                return AddressUtil.ZERO_ADDRESS;
            }

            return AddressUtil.Current.ConvertToChecksumAddress(address);
        }

        private static string[] NormalizeTokenArray(IEnumerable<string> tokens)
        {
            return tokens?.Select(NormalizeAddress).ToArray();
        }

        private static PoolKey CreateQuoterPoolKeyFromEntry(PoolCacheEntry entry)
        {
            return V4PoolKeyHelper.CreateNormalizedForQuoter(
                entry.Currency0,
                entry.Currency1,
                entry.Fee,
                entry.TickSpacing,
                entry.Hooks);
        }

        private static string CreateTokenPairKey(string tokenA, string tokenB)
        {
            tokenA = NormalizeAddress(tokenA);
            tokenB = NormalizeAddress(tokenB);

            return string.CompareOrdinal(tokenA, tokenB) <= 0
                ? $"{tokenA}|{tokenB}"
                : $"{tokenB}|{tokenA}";
        }

        private static Dictionary<string, List<PoolCacheEntry>> BuildCandidateLookup(IEnumerable<PoolCacheEntry> candidatePools)
        {
            var lookup = new Dictionary<string, List<PoolCacheEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (var pool in candidatePools)
            {
                if (pool == null)
                {
                    continue;
                }

                var key = CreateTokenPairKey(pool.Currency0, pool.Currency1);

                if (!lookup.TryGetValue(key, out var list))
                {
                    list = new List<PoolCacheEntry>();
                    lookup[key] = list;
                }

                list.Add(pool);
            }

            return lookup;
        }
    }
}
