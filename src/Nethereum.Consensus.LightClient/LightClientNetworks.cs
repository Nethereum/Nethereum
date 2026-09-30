using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Consensus.LightClient
{
    public static class LightClientNetworks
    {
        public const string MainnetGenesisValidatorsRoot = "0x4b363db94e286120d76eb905340fdd4e54bfe9f06bf33ff6cf5ad27f511bfe95";
        public const string SepoliaGenesisValidatorsRoot = "0xd8ea171f3c94aea21ebc42a1ed61052acf3f9209c00e4efbaaddac09ed9b8078";
        public const string HoleskyGenesisValidatorsRoot = "0x9143aa7c615a7f7115e2b6aac319c03529df8242ae705fba9df39b79c59fa8b1";

        public static bool TryGetGenesisValidatorsRoot(BigInteger chainId, out byte[] root)
        {
            string hex =
                chainId == 1 ? MainnetGenesisValidatorsRoot :
                chainId == 11155111 ? SepoliaGenesisValidatorsRoot :
                chainId == 17000 ? HoleskyGenesisValidatorsRoot :
                null;

            if (hex == null)
            {
                root = null;
                return false;
            }

            root = hex.HexToByteArray();
            return true;
        }

        public static LightClientConfig CreateConfig(BigInteger chainId, byte[] weakSubjectivityRoot = null)
        {
            var config = new LightClientConfig { SecondsPerSlot = 12 };

            if (TryGetGenesisValidatorsRoot(chainId, out var gvr))
                config.GenesisValidatorsRoot = gvr;

            if (weakSubjectivityRoot != null)
                config.WeakSubjectivityRoot = weakSubjectivityRoot;

            return config;
        }
    }
}
