using Nethereum.Documentation;
using System.Collections.Generic;

namespace Nethereum.EVM.Execution
{
    [NethereumDocExample(DocSection.EvmSimulator, "system-calls", "The system-call predeploy addresses and which are active per fork")]
    public static class SystemCallContracts
    {
        public const string BeaconRoots = "0x000f3df6d732807ef1319fb7b8bb8522d0beac02";

        public const string HistoryStorage = "0x0000F90827F1C53a10cb7A02335B175320002935";

        public const string WithdrawalRequests = "0x00000961Ef480Eb55e80D19ad83579A64c007002";

        public const string ConsolidationRequests = "0x0000BBdDc7CE488642fb579F8B00f3a590007251";

        public const string BuilderDeposit = "0x0000BFF46984E3725691FA540A8C7589300D8282";

        public const string BuilderExit = "0x000064D678505AD48F8CCB093BC65613800E8282";

        public const string SystemCaller = Nethereum.Util.AddressUtil.SYSTEM_ADDRESS;

        public static IReadOnlyList<string> RequestContractsFor(HardforkName fork)
        {
            if (fork < HardforkName.Prague) return EmptyRequestContracts;
            if (fork < HardforkName.Amsterdam) return PragueRequestContracts;
            return AllRequestContracts;
        }

        private static readonly IReadOnlyList<string> EmptyRequestContracts = new string[0];

        private static readonly IReadOnlyList<string> PragueRequestContracts = new[]
        {
            WithdrawalRequests,
            ConsolidationRequests
        };

        public static readonly IReadOnlyList<string> AllRequestContracts = new[]
        {
            WithdrawalRequests,
            ConsolidationRequests,
            BuilderDeposit,
            BuilderExit
        };
    }
}
