using System.Collections.Generic;
using System.Text.RegularExpressions;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class ForkScheduleResolver
    {
        private static readonly Regex Transition = new Regex(
            @"^(?<a>.+?)To(?<b>.+?)At(?<time>Time)?(?<n>\d+)(?<k>k)?$",
            RegexOptions.Compiled);

        private static readonly Regex Bpo = new Regex(@"^BPO(?<n>\d+)$", RegexOptions.Compiled);

        public static ChainForkSchedule Resolve(
            string network, Dictionary<string, BlockchainTestLoader.BlobScheduleEntry>? blobSchedule = null)
        {
            if (string.IsNullOrEmpty(network))
                throw new System.ArgumentException("empty Network value", nameof(network));

            var match = Transition.Match(network);
            if (!match.Success)
            {
                var single = ParseSide(network);
                var running = new ChainForkSchedule { Hardfork = single.Fork.ToString() };
                AddBlobSchedule(running, blobSchedule, single.Bpo, null, 0);
                return running;
            }

            var genesis = ParseSide(match.Groups["a"].Value);
            var post = ParseSide(match.Groups["b"].Value);

            var trigger = long.Parse(match.Groups["n"].Value);
            if (match.Groups["k"].Success) trigger *= 1000;
            var byTime = match.Groups["time"].Success;

            if (post.Bpo.HasValue)
            {
                if (!byTime || post.Fork != genesis.Fork)
                    throw new System.ArgumentException(
                        $"a BPO transition activates by timestamp on the same protocol fork: '{network}'", nameof(network));

                var bpoTransition = new ChainForkSchedule { Hardfork = genesis.Fork.ToString() };
                AddBlobSchedule(bpoTransition, blobSchedule, genesis.Bpo, post.Bpo, (ulong)trigger);
                return bpoTransition;
            }

            var entry = new ForkActivationEntry { Fork = post.Fork.ToString() };
            if (byTime) entry.Timestamp = (ulong)trigger;
            else entry.Block = trigger;

            var schedule = new ChainForkSchedule
            {
                GenesisFork = genesis.Fork.ToString(),
                Schedule = { entry },
            };
            AddBlobSchedule(schedule, blobSchedule, genesis.Bpo, null, 0);
            return schedule;
        }

        private static (HardforkName Fork, int? Bpo) ParseSide(string name)
        {
            var bpo = Bpo.Match(name);
            return bpo.Success
                ? (HardforkName.Osaka, int.Parse(bpo.Groups["n"].Value))
                : (HardforkNames.Parse(name), (int?)null);
        }

        private static void AddBlobSchedule(
            ChainForkSchedule schedule,
            Dictionary<string, BlockchainTestLoader.BlobScheduleEntry>? blobSchedule,
            int? activeAtGenesis,
            int? activatedLater,
            ulong activationTimestamp)
        {
            if (blobSchedule == null || blobSchedule.Count == 0)
            {
                if (activeAtGenesis.HasValue || activatedLater.HasValue)
                    throw new System.ArgumentException("a BPO network needs the fixture's blobSchedule");
                return;
            }

            foreach (var item in blobSchedule)
            {
                if (item.Value == null) continue;

                ulong? timestamp = null;
                var bpo = Bpo.Match(item.Key);
                if (bpo.Success)
                {
                    var n = int.Parse(bpo.Groups["n"].Value);
                    if (activeAtGenesis.HasValue && n <= activeAtGenesis.Value) timestamp = 0;
                    else if (activatedLater.HasValue && n == activatedLater.Value) timestamp = activationTimestamp;
                    else continue;
                }

                schedule.BlobSchedule.Add(new BlobScheduleActivationEntry
                {
                    Name = item.Key,
                    Timestamp = timestamp,
                    Target = ParseInt(item.Value.Target, item.Key),
                    Max = ParseInt(item.Value.Max, item.Key),
                    BaseFeeUpdateFraction = ParseInt(item.Value.BaseFeeUpdateFraction, item.Key),
                });
            }
        }

        private static int ParseInt(string? hex, string entry)
        {
            if (string.IsNullOrEmpty(hex))
                throw new System.ArgumentException($"blobSchedule entry '{entry}' is missing a value");
            var value = hex.HexToBigInteger(false);
            if (value < 0 || value > int.MaxValue)
                throw new System.ArgumentException($"blobSchedule entry '{entry}' value {hex} is out of range");
            return (int)value;
        }
    }
}
