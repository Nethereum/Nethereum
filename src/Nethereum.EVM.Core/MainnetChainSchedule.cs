using System.Collections.Generic;

namespace Nethereum.EVM
{
    public static class MainnetChainSchedule
    {
        public static ChainForkSchedule Instance => new ChainForkSchedule
        {
            ChainId = MainnetGenesisConstants.ChainId,
            GenesisFork = HardforkName.Frontier.ToString(),
            Schedule = Activations()
        };

        public static readonly ForkIdentityThresholds ForkIdentity = Instance.ForkThresholds();

        private static List<ForkActivationEntry> Activations()
        {
            var schedule = new List<ForkActivationEntry>
            {
                AtBlock(HardforkName.FrontierThawing, MainnetChainActivations.FrontierThawingBlock, countsTowardForkIdentity: false),
                AtBlock(HardforkName.Homestead, MainnetChainActivations.HomesteadBlock),
                AtBlock(HardforkName.DaoFork, MainnetChainActivations.DaoForkBlock),
                AtBlock(HardforkName.TangerineWhistle, MainnetChainActivations.TangerineWhistleBlock),
                AtBlock(HardforkName.SpuriousDragon, MainnetChainActivations.SpuriousDragonBlock),
                AtBlock(HardforkName.Byzantium, MainnetChainActivations.ByzantiumBlock),
                AtBlock(HardforkName.Constantinople, MainnetChainActivations.ConstantinopleBlock),
                AtBlock(HardforkName.Petersburg, MainnetChainActivations.PetersburgBlock),
                AtBlock(HardforkName.Istanbul, MainnetChainActivations.IstanbulBlock),
                AtBlock(HardforkName.MuirGlacier, MainnetChainActivations.MuirGlacierBlock),
                AtBlock(HardforkName.Berlin, MainnetChainActivations.BerlinBlock),
                AtBlock(HardforkName.London, MainnetChainActivations.LondonBlock),
                AtBlock(HardforkName.ArrowGlacier, MainnetChainActivations.ArrowGlacierBlock),
                AtBlock(HardforkName.GrayGlacier, MainnetChainActivations.GrayGlacierBlock),

                AtBlock(HardforkName.Paris, MainnetChainActivations.ParisBlock, countsTowardForkIdentity: false),

                AtTimestamp(HardforkName.Shanghai, MainnetChainActivations.ShanghaiTimestamp),
                AtTimestamp(HardforkName.Cancun, MainnetChainActivations.CancunTimestamp),
                AtTimestamp(HardforkName.Prague, MainnetChainActivations.PragueTimestamp)
            };

            AddIfScheduled(schedule, HardforkName.Osaka, MainnetChainActivations.OsakaTimestamp);
            AddIfScheduled(schedule, HardforkName.OsakaBpo1, MainnetChainActivations.OsakaBpo1Timestamp);
            AddIfScheduled(schedule, HardforkName.OsakaBpo2, MainnetChainActivations.OsakaBpo2Timestamp);
            AddIfScheduled(schedule, HardforkName.Amsterdam, null);

            return schedule;
        }

        private static void AddIfScheduled(List<ForkActivationEntry> schedule, HardforkName fork, ulong? timestamp)
        {
            schedule.Add(timestamp.HasValue
                ? AtTimestamp(fork, timestamp.Value)
                : new ForkActivationEntry { Fork = fork.ToString() });
        }

        private static ForkActivationEntry AtBlock(HardforkName fork, long block, bool countsTowardForkIdentity = true) =>
            new ForkActivationEntry
            {
                Fork = fork.ToString(),
                Block = block,
                CountsTowardForkIdentity = countsTowardForkIdentity
            };

        private static ForkActivationEntry AtTimestamp(HardforkName fork, ulong timestamp) =>
            new ForkActivationEntry { Fork = fork.ToString(), Timestamp = timestamp };
    }
}
