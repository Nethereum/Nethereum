using System;

namespace Nethereum.Consensus.Ssz.Tests
{
    public sealed record ConsensusSpecTestCase(string Container, string CaseName, byte[] Serialized, byte[] Root)
    {
        public string DisplayName => $"{Container}/{CaseName}";

        public override string ToString() => DisplayName;

        public ReadOnlySpan<byte> SerializedSpan => Serialized;
    }
}
