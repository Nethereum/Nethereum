using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Wallet.Services.Network
{
    public static class BuiltInDefaultChainIds
    {
        public static readonly IReadOnlyList<BigInteger> All = new List<BigInteger>
        {
            new BigInteger(1),
            new BigInteger(10),
            new BigInteger(56),
            new BigInteger(137),
            new BigInteger(8453),
            new BigInteger(42161),
            new BigInteger(324),
            new BigInteger(59144),
            new BigInteger(43114),
            new BigInteger(100),
            new BigInteger(42220),
            new BigInteger(11155111),
            new BigInteger(11155420),
            new BigInteger(84532),
            new BigInteger(421614),
        };
    }
}