using System;

namespace Nethereum.Freezer
{
    public static class FrozenParallelism
    {
        public const int CoResidentCoreDivisor = 3;

        public static int DefaultDegreeOfParallelism => Math.Max(1, Environment.ProcessorCount / CoResidentCoreDivisor);

        public static int Resolve(int? requested) =>
            requested is int dop && dop > 0 ? dop : DefaultDegreeOfParallelism;
    }
}
