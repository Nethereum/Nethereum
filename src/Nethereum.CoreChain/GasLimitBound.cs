namespace Nethereum.CoreChain
{
    public static class GasLimitBound
    {
        public const long MinimumGasLimit = 5000;

        public const long BoundDivisor = 1024;

        public const long ElasticityMultiplier = 2;

        public static long StepToward(long parentGasLimit, long desiredGasLimit)
        {
            var delta = parentGasLimit / BoundDivisor - 1;
            if (desiredGasLimit < MinimumGasLimit) desiredGasLimit = MinimumGasLimit;

            if (parentGasLimit < desiredGasLimit)
            {
                var raised = parentGasLimit + delta;
                return raised > desiredGasLimit ? desiredGasLimit : raised;
            }

            if (parentGasLimit > desiredGasLimit)
            {
                var lowered = parentGasLimit - delta;
                return lowered < desiredGasLimit ? desiredGasLimit : lowered;
            }

            return parentGasLimit;
        }
    }
}
