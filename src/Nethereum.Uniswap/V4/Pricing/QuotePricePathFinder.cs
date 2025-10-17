using Nethereum.RPC.Eth.DTOs;
using Nethereum.Uniswap.V4.Pools;
using Nethereum.Uniswap.V4.Pricing.V4Quoter.ContractDefinition;
using Nethereum.Uniswap.V4.Utils;
using Nethereum.Uniswap.V4.V4Quoter;
using Nethereum.Util;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PoolKey = Nethereum.Uniswap.V4.Pricing.V4Quoter.ContractDefinition.PoolKey;

namespace Nethereum.Uniswap.V4.Pricing
{
    public class SwapPathResult
    {
        public List<PoolKey> Path { get; set; }
        public BigInteger AmountOut { get; set; }
        public BigInteger GasEstimate { get; set; }
        public decimal PriceImpact { get; set; }
        public int[] Fees { get; set; }
    }

    // Replaces tuple usage for complete paths in multihop discovery
    internal class CompleteSwapPath
    {
        public List<PoolKey> Pools { get; set; }
        public List<int> Fees { get; set; }
    }

    public class QuotePricePathFinder
    {
        private readonly IWeb3 _web3;
        private readonly string _quoterAddress;
        private readonly PoolCacheService _poolCache;

        public QuotePricePathFinder(
            IWeb3 web3,
            string quoterAddress,
            PoolCacheService poolCache)
        {
            _web3 = web3;
            _quoterAddress = quoterAddress;
            _poolCache = poolCache;
        }

        public Task<SwapPathResult> FindBestDirectPathAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            IEnumerable<PoolCacheEntry> candidatePools)
        {
            return FindBestDirectPathUsingMultiCallAsync(tokenIn, tokenOut, amountIn, candidatePools);
        }

        public Task<SwapPathResult> FindBestDirectPathUsingMultiCallAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            IEnumerable<PoolCacheEntry> candidatePools)
        {
            return FindBestDirectPathCoreAsync(
                tokenIn,
                tokenOut,
                amountIn,
                candidatePools,
                (quoter, quoteParams) => quoter.GetQuotesUsingMultiCallAsync(quoteParams, BlockParameter.CreateLatest()));
        }

        public Task<SwapPathResult> FindBestDirectPathUsingRpcBatchAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            IEnumerable<PoolCacheEntry> candidatePools)
        {
            return FindBestDirectPathCoreAsync(
                tokenIn,
                tokenOut,
                amountIn,
                candidatePools,
                (quoter, quoteParams) => quoter.GetQuotesUsingRpcBatchAsync(quoteParams, BlockParameter.CreateLatest()));
        }

        private async Task<SwapPathResult> FindBestDirectPathCoreAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            IEnumerable<PoolCacheEntry> candidatePools,
            Func<V4QuoterService, IEnumerable<QuoteExactParams>, Task<List<QuoteResult>>> quoteFunc)
        {
            tokenIn = NormalizeAddress(tokenIn);
            tokenOut = NormalizeAddress(tokenOut);

            var quoter = new V4QuoterService(_web3, _quoterAddress);

            var lookup = BuildCandidateLookup(candidatePools);
            var key = CreateTokenPairKey(tokenIn, tokenOut);

            if (!lookup.TryGetValue(key, out var poolsForPair))
            {
                return null;
            }

            // Filter out non-existent pools
            var validPools = poolsForPair.Where(p => p.Exists).ToList();
            if (!validPools.Any())
            {
                return null;
            }

            // Build quote params for all valid pools
            var quoteParamsList = new List<QuoteExactParams>();
            var poolKeyList = new List<PoolKey>();
            var candidateList = new List<PoolCacheEntry>();

            foreach (var candidate in validPools)
            {
                var poolKey = CreateQuoterPoolKeyFromEntry(candidate);
                var pathKeys = V4PathEncoder.EncodeMultihopExactInPath(
                    new List<PoolKey> { poolKey },
                    tokenIn);

                quoteParamsList.Add(new QuoteExactParams
                {
                    Path = pathKeys,
                    ExactAmount = amountIn,
                    ExactCurrency = tokenIn
                });

                poolKeyList.Add(poolKey);
                candidateList.Add(candidate);
            }

            // Get all quotes using the provided quote function
            var quoteResults = await quoteFunc(quoter, quoteParamsList).ConfigureAwait(false);

            // Find best result
            SwapPathResult bestPath = null;
            BigInteger bestAmountOut = 0;

            for (int i = 0; i < quoteResults.Count; i++)
            {
                var result = quoteResults[i];
                if (result.Success && result.Output.AmountOut > bestAmountOut)
                {
                    bestAmountOut = result.Output.AmountOut;
                    bestPath = new SwapPathResult
                    {
                        Path = new List<PoolKey> { poolKeyList[i] },
                        AmountOut = result.Output.AmountOut,
                        GasEstimate = result.Output.GasEstimate,
                        Fees = new int[] { candidateList[i].Fee }
                    };
                }
            }

            return bestPath;
        }

        public Task<SwapPathResult> FindBestMultihopPathAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            string[] intermediateTokens,
            IEnumerable<PoolCacheEntry> candidatePools,
            int maxHops = 3)
        {
            return FindBestMultihopPathUsingMultiCallAsync(tokenIn, tokenOut, amountIn, intermediateTokens, candidatePools, maxHops);
        }

        public Task<SwapPathResult> FindBestMultihopPathUsingMultiCallAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            string[] intermediateTokens,
            IEnumerable<PoolCacheEntry> candidatePools,
            int maxHops = 3)
        {
            return FindBestMultihopPathCoreAsync(
                tokenIn,
                tokenOut,
                amountIn,
                intermediateTokens,
                candidatePools,
                maxHops,
                (quoter, quoteParams) => quoter.GetQuotesUsingMultiCallAsync(quoteParams, BlockParameter.CreateLatest()));
        }

        public Task<SwapPathResult> FindBestMultihopPathUsingRpcBatchAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            string[] intermediateTokens,
            IEnumerable<PoolCacheEntry> candidatePools,
            int maxHops = 3)
        {
            return FindBestMultihopPathCoreAsync(
                tokenIn,
                tokenOut,
                amountIn,
                intermediateTokens,
                candidatePools,
                maxHops,
                (quoter, quoteParams) => quoter.GetQuotesUsingRpcBatchAsync(quoteParams, BlockParameter.CreateLatest()));
        }

        private async Task<SwapPathResult> FindBestMultihopPathCoreAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            string[] intermediateTokens,
            IEnumerable<PoolCacheEntry> candidatePools,
            int maxHops,
            Func<V4QuoterService, IEnumerable<QuoteExactParams>, Task<List<QuoteResult>>> quoteFunc)
        {
            tokenIn = NormalizeAddress(tokenIn);
            tokenOut = NormalizeAddress(tokenOut);

            if (intermediateTokens == null || intermediateTokens.Length == 0 || maxHops <= 1)
            {
                return null;
            }

            var normalizedIntermediates = NormalizeTokenArray(intermediateTokens);
            var lookup = BuildCandidateLookup(candidatePools);

            return await FindBestMultihopPathFromCandidates(
                tokenIn,
                tokenOut,
                amountIn,
                normalizedIntermediates,
                maxHops,
                lookup,
                quoteFunc).ConfigureAwait(false);
        }

        public Task<SwapPathResult> FindBestPathAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            IEnumerable<PoolCacheEntry> candidatePools,
            string[] intermediateTokens = null,
            int maxHops = 3)
        {
            return FindBestPathUsingMultiCallAsync(tokenIn, tokenOut, amountIn, candidatePools, intermediateTokens, maxHops);
        }

        public Task<SwapPathResult> FindBestPathUsingMultiCallAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            IEnumerable<PoolCacheEntry> candidatePools,
            string[] intermediateTokens = null,
            int maxHops = 3)
        {
            return FindBestPathCoreAsync(
                tokenIn,
                tokenOut,
                amountIn,
                candidatePools,
                intermediateTokens,
                maxHops,
                (quoter, quoteParams) => quoter.GetQuotesUsingMultiCallAsync(quoteParams, BlockParameter.CreateLatest()));
        }

        public Task<SwapPathResult> FindBestPathUsingRpcBatchAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            IEnumerable<PoolCacheEntry> candidatePools,
            string[] intermediateTokens = null,
            int maxHops = 3)
        {
            return FindBestPathCoreAsync(
                tokenIn,
                tokenOut,
                amountIn,
                candidatePools,
                intermediateTokens,
                maxHops,
                (quoter, quoteParams) => quoter.GetQuotesUsingRpcBatchAsync(quoteParams, BlockParameter.CreateLatest()));
        }

        private async Task<SwapPathResult> FindBestPathCoreAsync(
            string tokenIn,
            string tokenOut,
            BigInteger amountIn,
            IEnumerable<PoolCacheEntry> candidatePools,
            string[] intermediateTokens,
            int maxHops,
            Func<V4QuoterService, IEnumerable<QuoteExactParams>, Task<List<QuoteResult>>> quoteFunc)
        {
            var directPath = await FindBestDirectPathCoreAsync(
                tokenIn,
                tokenOut,
                amountIn,
                candidatePools,
                quoteFunc).ConfigureAwait(false);

            if (intermediateTokens == null || intermediateTokens.Length == 0)
            {
                return directPath;
            }

            var multihopPath = await FindBestMultihopPathCoreAsync(
                tokenIn,
                tokenOut,
                amountIn,
                intermediateTokens,
                candidatePools,
                maxHops,
                quoteFunc).ConfigureAwait(false);

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
            Dictionary<string, List<PoolCacheEntry>> candidateLookup,
            Func<V4QuoterService, IEnumerable<QuoteExactParams>, Task<List<QuoteResult>>> quoteFunc)
        {
            var routes = EnumerateTokenRoutes(tokenIn, tokenOut, intermediateTokens, maxHops)
                .Where(route => route.Count > 2)
                .ToList();

            if (!routes.Any())
            {
                return null;
            }

            var quoter = new V4QuoterService(_web3, _quoterAddress);

            // Collect all possible complete paths
            var completePaths = new List<CompleteSwapPath>();

            foreach (var route in routes)
            {
                var hopCount = route.Count - 1;

                void TraverseSync(int hopIndex, List<PoolKey> currentPools, List<int> currentFees)
                {
                    if (hopIndex == hopCount)
                    {
                        // Complete path found - add to list
                        completePaths.Add(new CompleteSwapPath { Pools = new List<PoolKey>(currentPools), Fees = new List<int>(currentFees) });
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

                        TraverseSync(hopIndex + 1, currentPools, currentFees);

                        currentPools.RemoveAt(currentPools.Count - 1);
                        currentFees.RemoveAt(currentFees.Count - 1);
                    }
                }

                TraverseSync(0, new List<PoolKey>(), new List<int>());
            }

            if (!completePaths.Any())
            {
                return null;
            }

            // Build quote params for all complete paths
            var quoteParamsList = completePaths.Select(path =>
            {
                var pathKeys = V4PathEncoder.EncodeMultihopExactInPath(path.Pools, tokenIn);
                return new QuoteExactParams
                {
                    Path = pathKeys,
                    ExactAmount = amountIn,
                    ExactCurrency = tokenIn
                };
            }).ToList();

            // Get all quotes using the provided quote function
            var quoteResults = await quoteFunc(quoter, quoteParamsList).ConfigureAwait(false);

            // Find best result
            SwapPathResult bestPath = null;
            BigInteger bestAmountOut = 0;

            for (int i = 0; i < quoteResults.Count; i++)
            {
                var result = quoteResults[i];
                if (result.Success && result.Output.AmountOut > bestAmountOut)
                {
                    bestAmountOut = result.Output.AmountOut;
                    bestPath = new SwapPathResult
                    {
                        Path = completePaths[i].Pools,
                        AmountOut = result.Output.AmountOut,
                        GasEstimate = result.Output.GasEstimate,
                        Fees = completePaths[i].Fees.ToArray()
                    };
                }
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

#if NET451
            var uniqueIntermediates = (intermediateTokens ?? new string[0])
#else
            var uniqueIntermediates = (intermediateTokens ?? Array.Empty<string>())
#endif
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
            return PoolKeyUtils.Current.CreateNormalizedForQuoter(
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


