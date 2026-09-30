using System.Collections.Generic;

namespace Nethereum.EVM.Precompiles
{
    public static class PrecompileNames
    {
        private static readonly Dictionary<int, string> ByAddress = new Dictionary<int, string>
        {
            [0x01] = "ECREC",
            [0x02] = "SHA256",
            [0x03] = "RIPEMD160",
            [0x04] = "ID",
            [0x05] = "MODEXP",
            [0x06] = "BN254_ADD",
            [0x07] = "BN254_MUL",
            [0x08] = "BN254_PAIRING",
            [0x09] = "BLAKE2F",
            [0x0a] = "KZG_POINT_EVALUATION",
            [0x0b] = "BLS12_G1ADD",
            [0x0c] = "BLS12_G1MSM",
            [0x0d] = "BLS12_G2ADD",
            [0x0e] = "BLS12_G2MSM",
            [0x0f] = "BLS12_PAIRING_CHECK",
            [0x10] = "BLS12_MAP_FP_TO_G1",
            [0x11] = "BLS12_MAP_FP2_TO_G2",
            [0x0100] = "P256VERIFY"
        };

        private static readonly Dictionary<string, int> ByName = BuildByName();

        private static Dictionary<string, int> BuildByName()
        {
            var map = new Dictionary<string, int>();
            foreach (var pair in ByAddress) map[pair.Value] = pair.Key;
            return map;
        }

        public static bool TryGetName(int address, out string name) => ByAddress.TryGetValue(address, out name);

        public static bool TryGetAddress(string name, out int address) => ByName.TryGetValue(name, out address);

        public static string AddressHex(int address) => "0x" + address.ToString("x40");
    }
}
