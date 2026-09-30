using System.Collections.Generic;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class SuiteRegistry
    {
        public static readonly IReadOnlyList<IConformanceDriver> All = new IConformanceDriver[]
        {
            EestBlockchainTestsRlpDriver.Instance,
            EestBlockchainTestsEngineDriver.Instance,
            EestStateTestsDriver.Instance,
            EestTransactionTestsDriver.Instance,
            RpcCompatDriver.Instance,
        };
    }
}
