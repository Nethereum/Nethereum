using System.Collections.Generic;
using Nethereum.Chain.TestData.Vectors;

namespace Nethereum.Chain.TestData
{
    public static class SyncVectorCatalog
    {
        public static IReadOnlyList<ISyncVector> All { get; } = new ISyncVector[]
        {
            new WorkloadV1()
        };
    }
}
