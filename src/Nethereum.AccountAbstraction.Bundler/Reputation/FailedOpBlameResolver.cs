namespace Nethereum.AccountAbstraction.Bundler.Reputation
{
    public readonly struct BlameResult
    {
        public BlameResult(string? blamedAddress, bool isStakedAccountabilityPenalty)
        {
            BlamedAddress = blamedAddress;
            IsStakedAccountabilityPenalty = isStakedAccountabilityPenalty;
        }

        public string? BlamedAddress { get; }

        public bool IsStakedAccountabilityPenalty { get; }
    }

    public static class FailedOpBlameResolver
    {
        public static BlameResult Resolve(
            string reason,
            string? sender,
            string? factory,
            string? paymaster,
            bool isSenderStaked,
            bool isFactoryStaked)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return new BlameResult(sender, false);
            }

            if (reason.StartsWith("AA25"))
            {
                return new BlameResult(null, false);
            }

            if (reason.StartsWith("AA3"))
            {
                return new BlameResult(isSenderStaked ? sender : paymaster, false);
            }

            if (reason.StartsWith("AA2"))
            {
                var factoryAccountable = isFactoryStaked && !string.IsNullOrEmpty(factory);
                return new BlameResult(factoryAccountable ? factory : sender, factoryAccountable);
            }

            if (reason.StartsWith("AA1"))
            {
                return new BlameResult(!string.IsNullOrEmpty(factory) ? factory : sender, false);
            }

            return new BlameResult(sender, false);
        }
    }
}
