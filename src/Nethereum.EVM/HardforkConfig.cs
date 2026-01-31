using Nethereum.EVM.Execution;

namespace Nethereum.EVM
{
    public class HardforkConfig
    {
        public bool EnableEIP4844 { get; set; }
        public bool EnableEIP7623 { get; set; }
        public int MaxBlobsPerBlock { get; set; }
        public IPrecompileProvider PrecompileProvider { get; set; }

        private static readonly HardforkConfig _cancun = new HardforkConfig
        {
            EnableEIP4844 = true,
            EnableEIP7623 = false,
            MaxBlobsPerBlock = 6,
            PrecompileProvider = BuiltInPrecompileProvider.Cancun(),
        };

        private static readonly HardforkConfig _prague = new HardforkConfig
        {
            EnableEIP4844 = true,
            EnableEIP7623 = true,
            MaxBlobsPerBlock = 9,
            PrecompileProvider = BuiltInPrecompileProvider.Prague(),
        };

        public static HardforkConfig Cancun => _cancun;
        public static HardforkConfig Prague => _prague;
        public static HardforkConfig Default => _prague;

        public static HardforkConfig FromName(string hardfork)
        {
            if (string.IsNullOrEmpty(hardfork))
                return Default;

            return hardfork.ToLowerInvariant() switch
            {
                "cancun" => Cancun,
                "prague" => Prague,
                _ => Default
            };
        }
    }
}
