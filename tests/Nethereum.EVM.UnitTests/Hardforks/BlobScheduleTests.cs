using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class BlobScheduleTests
    {
        private static readonly BlobParameters OsakaParameters = new BlobParameters(6, 9, 5_007_716);
        private static readonly BlobParameters Bpo2Parameters = new BlobParameters(14, 21, 11_684_671);
        private static readonly BlobParameters Bpo3Parameters = new BlobParameters(21, 32, 20_609_697);
        private static readonly BlobParameters Bpo4Parameters = new BlobParameters(14, 21, 13_739_630);
        private static readonly BlobParameters AmsterdamParameters = new BlobParameters(14, 21, 11_684_671);

        [Fact]
        public void Given_ABpoEntryAtT_When_ABlockAtTMinus1AndAtT_Then_TheyUseThePreviousAndTheBpoParameters()
        {
            var schedule = new BlobSchedule(new[]
            {
                BlobScheduleEntry.ForFork(HardforkName.Osaka, OsakaParameters),
                BlobScheduleEntry.AtTimestamp("bpo3", 15_000, Bpo3Parameters),
            });

            Assert.Equal(OsakaParameters, schedule.At(HardforkName.Osaka, 0, 14_999));
            Assert.Equal(Bpo3Parameters, schedule.At(HardforkName.Osaka, 0, 15_000));
        }

        [Fact]
        public void Given_TwoBpoEntriesStatedOutOfOrder_When_BothHaveActivated_Then_TheLaterActivationApplies()
        {
            var schedule = new BlobSchedule(new[]
            {
                BlobScheduleEntry.AtTimestamp("bpo4", 30_000, Bpo4Parameters),
                BlobScheduleEntry.AtTimestamp("bpo3", 15_000, Bpo3Parameters),
            });

            Assert.Equal(Bpo3Parameters, schedule.At(HardforkName.Osaka, 0, 29_999));
            Assert.Equal(Bpo4Parameters, schedule.At(HardforkName.Osaka, 0, 30_000));
        }

        [Fact]
        public void Given_ABpoActiveFromTheSameTimestampAsItsFork_When_Resolved_Then_TheBpoApplies()
        {
            var schedule = new BlobSchedule(new[]
            {
                BlobScheduleEntry.ForFork(HardforkName.Osaka, OsakaParameters),
                BlobScheduleEntry.AtTimestamp("bpo2", 0, Bpo2Parameters),
                BlobScheduleEntry.AtTimestamp("bpo3", 15_000, Bpo3Parameters),
            });

            Assert.Equal(Bpo2Parameters, schedule.At(HardforkName.Osaka, 0, 0));
        }

        [Fact]
        public void Given_ANewBpoAfterBpo2_Then_AmsterdamParametersDoNotMove()
        {
            const ulong amsterdamActivation = 50_000;
            var schedule = new BlobSchedule(new[]
            {
                BlobScheduleEntry.AtTimestamp("bpo2", 10_000, Bpo2Parameters),
                BlobScheduleEntry.AtTimestamp("bpo3", 20_000, Bpo3Parameters),
                BlobScheduleEntry.ForFork(HardforkName.Amsterdam, AmsterdamParameters),
            });

            Assert.Equal(AmsterdamParameters, schedule.At(HardforkName.Amsterdam, amsterdamActivation, amsterdamActivation));
        }

        [Fact]
        public void Given_ABpoScheduledAfterTheForkActivated_When_Reached_Then_ItReplacesTheForkParameters()
        {
            const ulong amsterdamActivation = 50_000;
            var schedule = new BlobSchedule(new[]
            {
                BlobScheduleEntry.ForFork(HardforkName.Amsterdam, AmsterdamParameters),
                BlobScheduleEntry.AtTimestamp("bpo6", 60_000, Bpo3Parameters),
            });

            Assert.Equal(AmsterdamParameters, schedule.At(HardforkName.Amsterdam, amsterdamActivation, 59_999));
            Assert.Equal(Bpo3Parameters, schedule.At(HardforkName.Amsterdam, amsterdamActivation, 60_000));
        }

        [Fact]
        public void Given_NoEntryForTheForkAndNoActiveBpo_When_Resolved_Then_NothingIsStatedSoTheForkDefaultsApply()
        {
            var schedule = new BlobSchedule(new[]
            {
                BlobScheduleEntry.ForFork(HardforkName.Cancun, new BlobParameters(3, 6, 3_338_477)),
                BlobScheduleEntry.AtTimestamp("bpo3", 15_000, Bpo3Parameters),
            });

            Assert.Null(schedule.At(HardforkName.Prague, 0, 14_999));
        }
    }
}
