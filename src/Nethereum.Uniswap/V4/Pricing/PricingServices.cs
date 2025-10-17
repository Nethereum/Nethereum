using System;
using Nethereum.Uniswap.V4.Mappers;
using Nethereum.Uniswap.V4.Pools;
using Nethereum.Uniswap.V4.V4Quoter;
using Nethereum.Web3;

namespace Nethereum.Uniswap.V4.Pricing
{
    /// <summary>
    /// Lightweight container for pricing and quoting services.
    /// </summary>
    public class PricingServices
    {
        public PricingServices(
            IWeb3 web3,
            UniswapV4Addresses addresses,
            IPoolCacheRepository poolCacheRepository
           )
        {
            if (web3 == null) throw new ArgumentNullException(nameof(web3));
            if (addresses == null) throw new ArgumentNullException(nameof(addresses));


            if (string.IsNullOrWhiteSpace(addresses.StateView))
            {
                throw new ArgumentException("State address is required for pricing services", nameof(addresses));
            }

            if (string.IsNullOrWhiteSpace(addresses.PoolManager))
            {
                throw new ArgumentException("PoolManager address is required for pricing services", nameof(addresses));
            }

            var poolCache = new PoolCacheService(web3, addresses.StateView, addresses.PoolManager, poolCacheRepository);

            if (string.IsNullOrWhiteSpace(addresses.Quoter))
            {
                throw new ArgumentException("Quoter address is required for pricing services", nameof(addresses));
            }

            

            Quoter = new V4QuoterService(web3, addresses.Quoter);
            QuotePricePathFinder = new QuotePricePathFinder(web3, addresses.Quoter, poolCache);
            PathKeyMapper = PathKeyMapper.Current;
        }

        public PriceCalculator PriceCalculator => PriceCalculator.Current;
        public FeeCalculator FeeCalculator => FeeCalculator.Current;
        public SlippageCalculator SlippageCalculator => SlippageCalculator.Current;
        public PriceImpactCalculator PriceImpactCalculator => PriceImpactCalculator.Current;

        public V4QuoterService Quoter { get; }
        public QuotePricePathFinder QuotePricePathFinder { get; }
        public PoolKeyMapper PoolKeyMapper { get; }
        public PathKeyMapper PathKeyMapper { get; }
    }
}
