using System;

namespace Nethereum.Consensus.LightClient
{
    public class StaleHeaderException : Exception
    {
        public TimeSpan Age { get; }
        public TimeSpan Threshold { get; }

        public StaleHeaderException(TimeSpan age, TimeSpan threshold)
            : base($"Light client header is stale. Age: {age.TotalMinutes:F1} minutes, Threshold: {threshold.TotalMinutes:F1} minutes. Call UpdateAsync() or UpdateFinalityAsync() to refresh.")
        {
            Age = age;
            Threshold = threshold;
        }

        public StaleHeaderException(string message, TimeSpan age, TimeSpan threshold)
            : base(message)
        {
            Age = age;
            Threshold = threshold;
        }
    }
}
