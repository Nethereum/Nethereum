using Nethereum.Uniswap.UniversalRouter.V4Actions;
using Nethereum.Uniswap.V4.V4Quoter.ContractDefinition;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Uniswap.V4.Mappers
{
    public static class PoolKeyMapper
    {
        public static UniversalRouter.V4Actions.PoolKey MapToV4Action(this V4Quoter.ContractDefinition.PoolKey poolKey)
        {
            return new UniversalRouter.V4Actions.PoolKey
            {
                Currency0 = poolKey.Currency0,
                Currency1 = poolKey.Currency1,
                Fee = poolKey.Fee,
                Hooks = poolKey.Hooks,
                TickSpacing = poolKey.TickSpacing
            };
        }
        public static V4Quoter.ContractDefinition.PoolKey MapToV4Quoter(this UniversalRouter.V4Actions.PoolKey poolKey)
        {
            return new V4Quoter.ContractDefinition.PoolKey
            {
                Currency0 = poolKey.Currency0,
                Currency1 = poolKey.Currency1,
                Fee = poolKey.Fee,
                Hooks = poolKey.Hooks,
                TickSpacing = poolKey.TickSpacing
            };
        }

        public static List<UniversalRouter.V4Actions.PoolKey> MapToV4Action(this List<V4Quoter.ContractDefinition.PoolKey> poolKeys)
        {
            return poolKeys.Select(MapToV4Action).ToList();
        }

        public static List<V4Quoter.ContractDefinition.PoolKey> MapToV4Quoters(this List<UniversalRouter.V4Actions.PoolKey> poolKeys)
        {
            return poolKeys.Select(MapToV4Quoter).ToList();
        }
    }
}
