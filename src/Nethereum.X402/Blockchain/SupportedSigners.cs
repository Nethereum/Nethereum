using System.Collections.Generic;

namespace Nethereum.X402.Blockchain;

internal static class SupportedSigners
{
    public const string EvmNetworks = "eip155:*";

    public static Dictionary<string, List<string>> ForAccount(string address) =>
        new() { [EvmNetworks] = new List<string> { address } };
}
