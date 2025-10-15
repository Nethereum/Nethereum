using Nethereum.Uniswap.UniversalRouter.V4Actions;
using Nethereum.Uniswap.V4.V4Quoter.ContractDefinition;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Uniswap.V4.Mappers
{
    public static class PathKeyMapper
    {
        public static UniversalRouter.V4Actions.PathKey MapToActionV4(this V4Quoter.ContractDefinition.PathKey pathKey)
        {
            return new UniversalRouter.V4Actions.PathKey
            {
                Fee = pathKey.Fee,
                Hooks = pathKey.Hooks,
                HookData = pathKey.HookData,
                IntermediateCurrency = pathKey.IntermediateCurrency,
                TickSpacing = pathKey.TickSpacing
            };
        }
        public static V4Quoter.ContractDefinition.PathKey MapToV4Quoter(this UniversalRouter.V4Actions.PathKey pathKey)
        {
            return new V4Quoter.ContractDefinition.PathKey
            {
                Fee = (uint)pathKey.Fee,
                Hooks = pathKey.Hooks,
                HookData = pathKey.HookData,
                IntermediateCurrency = pathKey.IntermediateCurrency,
                TickSpacing = pathKey.TickSpacing
            };
        }

        public static List<UniversalRouter.V4Actions.PathKey> MapToActionV4(this List<V4Quoter.ContractDefinition.PathKey> pathKeys)
        {
            return pathKeys.Select(MapToActionV4).ToList();
        }

        public static List<V4Quoter.ContractDefinition.PathKey> MapToV4Quoter(this List<UniversalRouter.V4Actions.PathKey> pathKeys)
        {
            return pathKeys.Select(MapToV4Quoter).ToList();
        }


    }
}
