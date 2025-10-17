namespace Nethereum.Uniswap.V4
{
    /// <summary>
    /// Contract addresses for Uniswap V4 protocol
    /// </summary>
    public class UniswapV4Addresses
    {
        public string PoolManager { get; set; }
        public string PositionManager { get; set; }
        public string Quoter { get; set; }
        public string StateView { get; set; }
        public string PositionDescriptor { get; set; }

        public string UniversalRouterV4 { get; set; }

        /// <summary>
        /// Ethereum Mainnet addresses
        /// </summary>
        public static UniswapV4Addresses Mainnet => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.MainnetPoolManagerV4,
            PositionManager = UniswapAddresses.MainnetPositionManagerV4,
            Quoter = UniswapAddresses.MainnetQuoterV4,
            StateView = UniswapAddresses.MainnetStateViewV4,
            PositionDescriptor = UniswapAddresses.MainnetPositionDescriptorV4,
            UniversalRouterV4 = UniswapAddresses.MainnetUniversalRouter
        };

        /// <summary>
        /// Base network addresses
        /// </summary>
        public static UniswapV4Addresses Base => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.BasePoolManagerV4,
            PositionManager = UniswapAddresses.BasePositionManagerV4,
            Quoter = UniswapAddresses.BaseQuoterV4,
            StateView = UniswapAddresses.BaseStateViewV4,
            PositionDescriptor = UniswapAddresses.BasePositionDescriptorV4,
            UniversalRouterV4 = UniswapAddresses.BaseUniversalRouter

        };

        /// <summary>
        /// Optimism network addresses
        /// </summary>
        public static UniswapV4Addresses Optimism => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.OptimismPoolManagerV4,
            PositionManager = UniswapAddresses.OptimismPositionManagerV4,
            Quoter = UniswapAddresses.OptimismQuoterV4,
            StateView = UniswapAddresses.OptimismStateViewV4,
            PositionDescriptor = UniswapAddresses.OptimismPositionDescriptorV4,
            UniversalRouterV4 = UniswapAddresses.OptimismUniversalRouter
        };

        /// <summary>
        /// Arbitrum One network addresses
        /// </summary>
        public static UniswapV4Addresses ArbitrumOne => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.ArbitrumOnePoolManagerV4,
            PositionManager = UniswapAddresses.ArbitrumOnePositionManagerV4,
            Quoter = UniswapAddresses.ArbitrumOneQuoterV4,
            StateView = UniswapAddresses.ArbitrumOneStateViewV4,
            PositionDescriptor = UniswapAddresses.ArbitrumOnePositionDescriptorV4,
            UniversalRouterV4 = UniswapAddresses.ArbitrumOneUniversalRouter
        };

        /// <summary>
        /// Polygon network addresses
        /// </summary>
        public static UniswapV4Addresses Polygon => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.PolygonPoolManagerV4,
            PositionManager = UniswapAddresses.PolygonPositionManagerV4,
            Quoter = UniswapAddresses.PolygonQuoterV4,
            StateView = UniswapAddresses.PolygonStateViewV4,
            PositionDescriptor = UniswapAddresses.PolygonPositionDescriptorV4,
            UniversalRouterV4 = UniswapAddresses.PolygonUniversalRouter
        };

        /// <summary>
        /// Avalanche network addresses
        /// </summary>
        public static UniswapV4Addresses Avalanche => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.AvalanchePoolManagerV4,
            PositionManager = UniswapAddresses.AvalanchePositionManagerV4,
            Quoter = UniswapAddresses.AvalancheQuoterV4,
            StateView = UniswapAddresses.AvalancheStateViewV4,
            PositionDescriptor = UniswapAddresses.AvalanchePositionDescriptorV4,
            UniversalRouterV4 = UniswapAddresses.AvalancheUniversalRouter
        };

        /// <summary>
        /// BNB Chain network addresses
        /// </summary>
        public static UniswapV4Addresses BNB => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.BNBPoolManagerV4,
            PositionManager = UniswapAddresses.BNBPositionManagerV4,
            Quoter = UniswapAddresses.BNBQuoterV4,
            StateView = UniswapAddresses.BNBStateViewV4,
            PositionDescriptor = UniswapAddresses.BNBPositionDescriptorV4,
            UniversalRouterV4 = UniswapAddresses.BNBUniversalRouter
        };

        /// <summary>
        /// Ethereum Sepolia testnet addresses
        /// </summary>
        public static UniswapV4Addresses Sepolia => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.SepoliaPoolManagerV4,
            PositionManager = UniswapAddresses.SepoliaPositionManagerV4,
            Quoter = UniswapAddresses.SepoliaQuoterV4,
            StateView = UniswapAddresses.SepoliaStateViewV4,
            PositionDescriptor = null,
            UniversalRouterV4 = UniswapAddresses.SepoliaUniversalRouterV4

        };

        /// <summary>
        /// Base Sepolia testnet addresses
        /// </summary>
        public static UniswapV4Addresses BaseSepolia => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.BaseSepoliaPoolManagerV4,
            PositionManager = UniswapAddresses.BaseSepoliaPositionManagerV4,
            Quoter = UniswapAddresses.BaseSepoliaQuoterV4,
            StateView = UniswapAddresses.BaseSepoliaStateViewV4,
            PositionDescriptor = null,
            UniversalRouterV4 = UniswapAddresses.BaseSepoliaUniversalRouterV4
        };

        /// <summary>
        /// Arbitrum Sepolia testnet addresses
        /// </summary>
        public static UniswapV4Addresses ArbitrumSepolia => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.ArbitrumSepoliaPoolManagerV4,
            PositionManager = UniswapAddresses.ArbitrumSepoliaPositionManagerV4,
            Quoter = UniswapAddresses.ArbitrumSepoliaQuoterV4,
            StateView = UniswapAddresses.ArbitrumSepoliaStateViewV4,
            PositionDescriptor = null,
            UniversalRouterV4 = UniswapAddresses.ArbitrumSepoliaUniversalRouterV4
        };

        /// <summary>
        /// Unichain Sepolia testnet addresses
        /// </summary>
        public static UniswapV4Addresses UnichainSepolia => new UniswapV4Addresses
        {
            PoolManager = UniswapAddresses.UnichainSepoliaPoolManagerV4,
            PositionManager = UniswapAddresses.UnichainSepoliaPositionManagerV4,
            Quoter = UniswapAddresses.UnichainSepoliaQuoterV4,
            StateView = UniswapAddresses.UnichainSepoliaStateViewV4,
            PositionDescriptor = null,
            UniversalRouterV4 = UniswapAddresses.UnichainSepoliaUniversalRouterV4  
        };
    }
}
