using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.Chain.TestData
{
    public sealed class RosterAccount
    {
        public EthECKey Key { get; }
        public string PrivateKey { get; }
        public string Address { get; }

        public RosterAccount(string privateKey)
        {
            PrivateKey = privateKey;
            Key = new EthECKey(privateKey);
            Address = Key.GetPublicAddress();
        }
    }

    public static class ChainRoster
    {
        public static readonly RosterAccount First = new("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        public static readonly RosterAccount Second = new("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d");
        public static readonly RosterAccount Third = new("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a");
        public static readonly RosterAccount Fourth = new("0x7c852118294e51e653712a81e05800f419141751be58f605c371e15141b007a6");
        public static readonly RosterAccount Fifth = new("0x47e179ec197488593b187f80a00eb0da91f1b9d0b13f8733639f19c30a34926a");
        public static readonly RosterAccount Sixth = new("0x8b3a350cf5c34c9194ca85829a2df0ec3153be0318b5e2d3348e872092edffba");
        public static readonly RosterAccount Seventh = new("0x92db14e403b83dfe3df233f83dfa3a0d7096f21ca9b0d6d6b8d88b2b4ec1564e");
        public static readonly RosterAccount Eighth = new("0x4bbbf85ce3377467afe5d46f804f221813b2bb87f24d81f60f1fcdbf7cbf4356");

        public static readonly RosterAccount NeverFunded =
            new("0xdbda1821b80551c9d65939329250298aa3472ba22feea921c0cf5d620ea67b97");

        public static readonly IReadOnlyList<RosterAccount> Named =
            new[] { First, Second, Third, Fourth, Fifth, Sixth, Seventh, Eighth };

        public static RosterAccount Generate(int index) =>
            new(new Sha3Keccack()
                .CalculateHash(System.Text.Encoding.UTF8.GetBytes("nethereum-sync-workload-account/" + index))
                .ToHex(true));

        public static IReadOnlyList<RosterAccount> Take(int generatedCount)
        {
            var list = new List<RosterAccount>(Named);
            for (var i = 0; i < generatedCount; i++) list.Add(Generate(i));
            return list;
        }
    }
}
