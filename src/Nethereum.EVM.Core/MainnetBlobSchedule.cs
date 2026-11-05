using Nethereum.Documentation;
namespace Nethereum.EVM
{
    [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "The mainnet blob schedule with the BPO1 and BPO2 timestamps")]
    public static class MainnetBlobSchedule
    {
        public const ulong Bpo1Timestamp = 1_765_290_071;

        public const ulong Bpo2Timestamp = 1_767_747_671;

        public static readonly BlobParameters Cancun = new BlobParameters(3, 6, 3_338_477);

        public static readonly BlobParameters Prague = new BlobParameters(6, 9, 5_007_716);

        public static readonly BlobParameters Osaka = new BlobParameters(6, 9, 5_007_716);

        public static readonly BlobParameters Bpo1 = new BlobParameters(10, 15, 8_346_193);

        public static readonly BlobParameters Bpo2 = new BlobParameters(14, 21, 11_684_671);

        public static readonly BlobParameters Amsterdam = new BlobParameters(14, 21, 11_684_671);

        public static readonly BlobSchedule Instance = new BlobSchedule(new[]
        {
            BlobScheduleEntry.ForFork(HardforkName.Cancun, Cancun),
            BlobScheduleEntry.ForFork(HardforkName.Prague, Prague),
            BlobScheduleEntry.ForFork(HardforkName.Osaka, Osaka),
            BlobScheduleEntry.AtTimestamp("bpo1", Bpo1Timestamp, Bpo1),
            BlobScheduleEntry.AtTimestamp("bpo2", Bpo2Timestamp, Bpo2),
            BlobScheduleEntry.ForFork(HardforkName.Amsterdam, Amsterdam),
        });
    }
}
