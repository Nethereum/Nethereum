using System;

namespace Nethereum.TokenServices.ERC20.Pricing.Resilience
{
    public class RetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
        public double BackoffMultiplier { get; set; } = 2.0;

        public static RetryPolicy Default => new RetryPolicy();

        public static RetryPolicy None => new RetryPolicy { MaxRetries = 0 };

        public TimeSpan GetDelay(int attempt)
        {
            if (attempt < 0) return InitialDelay;
            return TimeSpan.FromMilliseconds(InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attempt));
        }
    }
}
