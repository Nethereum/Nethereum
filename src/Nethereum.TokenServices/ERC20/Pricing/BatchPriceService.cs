using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Pricing
{
    public class BatchPriceService : IBatchPriceService
    {
        private readonly ITokenPriceProvider _priceProvider;

        public BatchPriceService(ITokenPriceProvider priceProvider)
        {
            _priceProvider = priceProvider ?? throw new ArgumentNullException(nameof(priceProvider));
        }

        public Task<BatchPriceResult> GetPricesAsync(
            IEnumerable<ChainTokenRequest> requests,
            string vsCurrency = "usd",
            CancellationToken cancellationToken = default)
        {
            var batchRequest = new BatchPriceRequest(vsCurrency);
            batchRequest.ChainRequests.AddRange(requests);
            return GetPricesAsync(batchRequest, cancellationToken);
        }

        public async Task<BatchPriceResult> GetPricesAsync(
            BatchPriceRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request?.ChainRequests == null || !request.ChainRequests.Any())
            {
                return BatchPriceResult.Empty(request?.Currency ?? "usd");
            }

            var result = new BatchPriceResult
            {
                Success = true,
                Currency = request.Currency
            };

            var chainGroups = request.ChainRequests
                .GroupBy(r => r.ChainId)
                .ToList();

            var totalQueried = 0;
            var totalFound = 0;

            var tasks = chainGroups.Select(async chainGroup =>
            {
                var chainId = chainGroup.Key;
                var allAddresses = chainGroup
                    .SelectMany(r => r.ContractAddresses ?? new List<string>())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .Select(a => a.ToLowerInvariant())
                    .Distinct()
                    .ToList();

                var needsNative = chainGroup.Any(r => r.IncludeNative);

                return await FetchChainPricesAsync(chainId, allAddresses, needsNative, request.Currency, result, cancellationToken);
            });

            var chainResults = await Task.WhenAll(tasks);

            foreach (var (queried, found) in chainResults)
            {
                totalQueried += queried;
                totalFound += found;
            }

            result.TotalTokensQueried = totalQueried;
            result.TotalPricesFound = totalFound;

            return result;
        }

        private async Task<(int queried, int found)> FetchChainPricesAsync(
            long chainId,
            List<string> addresses,
            bool needsNative,
            string currency,
            BatchPriceResult result,
            CancellationToken cancellationToken)
        {
            var queried = addresses.Count + (needsNative ? 1 : 0);
            var found = 0;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (addresses.Any())
                {
                    var prices = await _priceProvider.GetPricesByContractAsync(chainId, addresses, currency);

                    lock (result)
                    {
                        if (!result.PricesByChain.ContainsKey(chainId))
                        {
                            result.PricesByChain[chainId] = new Dictionary<string, TokenPrice>(StringComparer.OrdinalIgnoreCase);
                        }

                        foreach (var kvp in prices)
                        {
                            result.PricesByChain[chainId][kvp.Key.ToLowerInvariant()] = kvp.Value;
                            found++;
                        }
                    }
                }

                if (needsNative)
                {
                    var nativePrice = await _priceProvider.GetNativeTokenPriceAsync(chainId, currency);
                    if (nativePrice != null)
                    {
                        lock (result)
                        {
                            result.NativePrices[chainId] = nativePrice;
                        }
                        found++;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lock (result)
                {
                    result.Errors.Add($"Chain {chainId}: {ex.Message}");
                }
            }

            return (queried, found);
        }
    }
}
