using System.Text.RegularExpressions;
using Nethereum.EVM;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class ForkScheduleResolver
    {
        private static readonly Regex Transition = new Regex(
            @"^(?<a>.+?)To(?<b>.+?)At(?<time>Time)?(?<n>\d+)(?<k>k)?$",
            RegexOptions.Compiled);

        public static ChainForkSchedule Resolve(string network)
        {
            if (string.IsNullOrEmpty(network))
                throw new System.ArgumentException("empty Network value", nameof(network));

            var match = Transition.Match(network);
            if (!match.Success)
                return new ChainForkSchedule { Hardfork = HardforkNames.Parse(network).ToString() };

            var genesisFork = HardforkNames.Parse(match.Groups["a"].Value);
            var postFork = HardforkNames.Parse(match.Groups["b"].Value);

            var trigger = long.Parse(match.Groups["n"].Value);
            if (match.Groups["k"].Success) trigger *= 1000;

            var entry = new ForkActivationEntry { Fork = postFork.ToString() };
            if (match.Groups["time"].Success) entry.Timestamp = (ulong)trigger;
            else entry.Block = trigger;

            return new ChainForkSchedule
            {
                GenesisFork = genesisFork.ToString(),
                Schedule = { entry },
            };
        }
    }
}
