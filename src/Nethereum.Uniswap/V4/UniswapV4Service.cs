using System;
using Nethereum.Uniswap.Accounts;
using Nethereum.Uniswap.UniversalRouter;
using Nethereum.Uniswap.V4.Pools;
using Nethereum.Uniswap.V4.Positions;
using Nethereum.Uniswap.V4.Pricing;
using Nethereum.Uniswap.V4.Utils;
using Nethereum.Web3;

namespace Nethereum.Uniswap.V4
{
    /// <summary>
    /// Primary entry point for interacting with Uniswap V4 services.
    /// </summary>
    public class UniswapV4Service
    {
        private readonly IWeb3 _web3;
        private readonly UniswapV4Addresses _addresses;

        public PoolServices Pools { get; }
        public PricingServices Pricing { get; }

        public PositionServices Positions { get; } 

        public AccountServices Accounts { get; }

        public MathServices Math { get; } 

        public UniversalRouterService UniversalRouter { get; }

        public UniswapV4Service(
            IWeb3 web3,
            UniswapV4Addresses addresses,
            IPoolCacheRepository poolCacheRepository = null)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));

            if(poolCacheRepository == null)
            {
                poolCacheRepository = new InMemoryPoolCacheRepository();
            }

            Pools = new PoolServices(_web3, _addresses, poolCacheRepository);
            Pricing = new PricingServices(_web3, _addresses, poolCacheRepository);
            Positions = new PositionServices(_web3, _addresses);
            Accounts = new AccountServices(_web3);
            Math = new MathServices();
            UniversalRouter = new UniversalRouterService(_web3, _addresses.UniversalRouterV4);
        }

        public UniversalRouterV4ActionsBuilder GetUniversalRouterV4ActionsBuilder()
        {
            return new UniversalRouterV4ActionsBuilder();
        }


    }
}
