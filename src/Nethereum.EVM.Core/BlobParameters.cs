using Nethereum.Documentation;
using System;

namespace Nethereum.EVM
{
    [NethereumDocExample(DocSection.EvmSimulator, "blob-schedule", "Blob target, max and base-fee update fraction for one schedule entry")]
    public readonly struct BlobParameters : IEquatable<BlobParameters>
    {
        public BlobParameters(int target, int max, int baseFeeUpdateFraction)
        {
            Target = target;
            Max = max;
            BaseFeeUpdateFraction = baseFeeUpdateFraction;
        }

        public int Target { get; }

        public int Max { get; }

        public int BaseFeeUpdateFraction { get; }

        public bool Equals(BlobParameters other) =>
            Target == other.Target && Max == other.Max && BaseFeeUpdateFraction == other.BaseFeeUpdateFraction;

        public override bool Equals(object obj) => obj is BlobParameters other && Equals(other);

        public override int GetHashCode() => unchecked((Target * 397 ^ Max) * 397 ^ BaseFeeUpdateFraction);

        public override string ToString() =>
            $"target {Target}, max {Max}, baseFeeUpdateFraction {BaseFeeUpdateFraction}";
    }
}
