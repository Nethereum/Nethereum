using System;
using Nethereum.Uniswap.V4.Contracts.PoolManager;
using Nethereum.Uniswap.V4.Mappers;
using Nethereum.Web3;

namespace Nethereum.Uniswap.V4.Pools
{

        public class PoolServices
        {
            public PoolServices(IWeb3 web3, UniswapV4Addresses addresses, IPoolCacheRepository repository = null)
            {
             
                if (string.IsNullOrWhiteSpace(addresses.StateView))
                {
                    throw new ArgumentException("StateView address is required for pool services", nameof(addresses));
                }

                PoolKeyHelper = PoolKeyUtils.Current;

                Manager = new PoolManagerService(web3, addresses.PoolManager);
                
                Cache = new PoolCacheService(
                    web3,
                    addresses.StateView,
                    addresses.PoolManager,
                    repository: repository
                   );


                PoolKeyMapper = PoolKeyMapper.Current;
            }

            public PoolManagerService Manager { get; }
            public PoolCacheService Cache { get; }
            public PoolKeyUtils PoolKeyHelper { get; }
            public PoolKeyMapper PoolKeyMapper { get; }  
        }
    
}











