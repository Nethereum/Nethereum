namespace Nethereum.AccountAbstraction.Bundler.Reputation
{
    public static class ReputationStatusCalculator
    {
        public static ReputationStatus Compute(int opsSeen, int opsIncluded, ReputationConfig config)
        {
            var minExpectedIncluded = opsSeen / config.MinInclusionDenominator;

            if (minExpectedIncluded <= opsIncluded + config.ThrottlingSlack)
            {
                return ReputationStatus.Ok;
            }

            if (minExpectedIncluded <= opsIncluded + config.BanSlack)
            {
                return ReputationStatus.Throttled;
            }

            return ReputationStatus.Banned;
        }
    }
}
