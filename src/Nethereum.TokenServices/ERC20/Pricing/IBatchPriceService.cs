using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.ERC20.Pricing
{
    public interface IBatchPriceService
    {
        Task<BatchPriceResult> GetPricesAsync(
            IEnumerable<ChainTokenRequest> requests,
            string vsCurrency = "usd",
            CancellationToken cancellationToken = default);

        Task<BatchPriceResult> GetPricesAsync(
            BatchPriceRequest request,
            CancellationToken cancellationToken = default);
    }

    public class ChainTokenRequest
    {
        public long ChainId { get; set; }
        public List<string> ContractAddresses { get; set; } = new List<string>();
        public bool IncludeNative { get; set; }

        public ChainTokenRequest() { }

        public ChainTokenRequest(long chainId, IEnumerable<string> addresses, bool includeNative = false)
        {
            ChainId = chainId;
            ContractAddresses = new List<string>(addresses ?? new List<string>());
            IncludeNative = includeNative;
        }
    }

    public class BatchPriceRequest
    {
        public List<ChainTokenRequest> ChainRequests { get; set; } = new List<ChainTokenRequest>();
        public string Currency { get; set; } = "usd";

        public BatchPriceRequest() { }

        public BatchPriceRequest(string currency)
        {
            Currency = currency;
        }

        public BatchPriceRequest AddChain(long chainId, IEnumerable<string> addresses, bool includeNative = false)
        {
            ChainRequests.Add(new ChainTokenRequest(chainId, addresses, includeNative));
            return this;
        }
    }

    public class BatchPriceResult
    {
        public bool Success { get; set; }
        public string Currency { get; set; }
        public Dictionary<long, Dictionary<string, TokenPrice>> PricesByChain { get; set; }
            = new Dictionary<long, Dictionary<string, TokenPrice>>();
        public Dictionary<long, TokenPrice> NativePrices { get; set; }
            = new Dictionary<long, TokenPrice>();
        public List<string> Errors { get; set; } = new List<string>();
        public int TotalTokensQueried { get; set; }
        public int TotalPricesFound { get; set; }

        public bool TryGetPrice(long chainId, string contractAddress, out TokenPrice price)
        {
            price = null;
            if (PricesByChain.TryGetValue(chainId, out var chainPrices))
            {
                var normalizedAddress = contractAddress?.ToLowerInvariant();
                if (normalizedAddress != null && chainPrices.TryGetValue(normalizedAddress, out price))
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryGetNativePrice(long chainId, out TokenPrice price)
        {
            return NativePrices.TryGetValue(chainId, out price);
        }

        public decimal? GetPrice(long chainId, string contractAddress)
        {
            return TryGetPrice(chainId, contractAddress, out var price) ? price.Price : (decimal?)null;
        }

        public decimal? GetNativePrice(long chainId)
        {
            return TryGetNativePrice(chainId, out var price) ? price.Price : (decimal?)null;
        }

        public static BatchPriceResult Empty(string currency) => new BatchPriceResult
        {
            Success = true,
            Currency = currency
        };

        public static BatchPriceResult Failed(string error) => new BatchPriceResult
        {
            Success = false,
            Errors = new List<string> { error }
        };
    }
}
