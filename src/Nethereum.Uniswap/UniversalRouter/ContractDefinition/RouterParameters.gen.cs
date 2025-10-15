using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.UniversalRouter.ContractDefinition
{
    public partial class RouterParameters : RouterParametersBase { }

    public class RouterParametersBase 
    {
        [Parameter("address", "permit2", 1)]
        public virtual string Permit2 { get; set; }
        [Parameter("address", "weth9", 2)]
        public virtual string Weth9 { get; set; }
        [Parameter("address", "v2Factory", 3)]
        public virtual string V2Factory { get; set; }
        [Parameter("address", "v3Factory", 4)]
        public virtual string V3Factory { get; set; }
        [Parameter("bytes32", "pairInitCodeHash", 5)]
        public virtual byte[] PairInitCodeHash { get; set; }
        [Parameter("bytes32", "poolInitCodeHash", 6)]
        public virtual byte[] PoolInitCodeHash { get; set; }
        [Parameter("address", "v4PoolManager", 7)]
        public virtual string V4PoolManager { get; set; }
        [Parameter("address", "v3NFTPositionManager", 8)]
        public virtual string V3NFTPositionManager { get; set; }
        [Parameter("address", "v4PositionManager", 9)]
        public virtual string V4PositionManager { get; set; }
    }
}
