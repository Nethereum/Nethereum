using Nethereum.Uniswap.UniversalRouter.V4Actions;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Uniswap.V4.Mappers
{
    public class PathKeyMapper
    {
        public static PathKeyMapper Current { get; } = new PathKeyMapper();

        public UniversalRouter.V4Actions.PathKey MapToActionV4(Pricing.V4Quoter.ContractDefinition.PathKey pathKey)
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

        public Pricing.V4Quoter.ContractDefinition.PathKey MapToV4Quoter(UniversalRouter.V4Actions.PathKey pathKey)
        {
            return new Pricing.V4Quoter.ContractDefinition.PathKey
            {
                Fee = (uint)pathKey.Fee,
                Hooks = pathKey.Hooks,
                HookData = pathKey.HookData,
                IntermediateCurrency = pathKey.IntermediateCurrency,
                TickSpacing = pathKey.TickSpacing
            };
        }

        public List<UniversalRouter.V4Actions.PathKey> MapToActionV4(List<Pricing.V4Quoter.ContractDefinition.PathKey> pathKeys)
        {
            return pathKeys.Select(MapToActionV4).ToList();
        }

        public List<Pricing.V4Quoter.ContractDefinition.PathKey> MapToV4Quoter(List<UniversalRouter.V4Actions.PathKey> pathKeys)
        {
            return pathKeys.Select(MapToV4Quoter).ToList();
        }
    }

    public static class PathKeyMapperExtensions
    {
        public static List<UniversalRouter.V4Actions.PathKey> MapToActionV4(this List<Pricing.V4Quoter.ContractDefinition.PathKey> pathKeys)
        {
            return PathKeyMapper.Current.MapToActionV4(pathKeys);
        }

        public static List<Pricing.V4Quoter.ContractDefinition.PathKey> MapToV4Quoter(this List<UniversalRouter.V4Actions.PathKey> pathKeys)
        {
            return PathKeyMapper.Current.MapToV4Quoter(pathKeys);
        }
    }
}
