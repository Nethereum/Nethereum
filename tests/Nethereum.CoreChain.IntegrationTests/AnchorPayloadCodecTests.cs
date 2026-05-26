using System.Collections.Generic;
using Nethereum.CoreChain.DataAvailability;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class AnchorPayloadCodecTests
    {
        private readonly ITestOutputHelper _output;
        public AnchorPayloadCodecTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void ShouldRoundtripEmptyPayload()
        {
            var payload = AnchorPayloadCodec.Build(
                StateModel.MptKeccak, AnchorKind.Block, new List<AnchorPayloadSection>());

            var encoded = AnchorPayloadCodec.Encode(payload);
            Assert.Equal(4, encoded.Length);

            var decoded = AnchorPayloadCodec.Decode(encoded);
            Assert.Equal(AnchorPayloadCodec.CurrentVersion, decoded.Header.Version);
            Assert.Equal((byte)StateModel.MptKeccak, decoded.Header.StateModel);
            Assert.Equal((byte)AnchorKind.Block, decoded.Header.AnchorKind);
            Assert.Empty(decoded.Sections);

            _output.WriteLine($"Empty payload: {encoded.Length} bytes, version={decoded.Header.Version}");
        }

        [Fact]
        public void ShouldRoundtripTier3Payload()
        {
            var stateRoot = new byte[32];
            stateRoot[0] = 0xAA;
            var proofBytes = new byte[] { 1, 2, 3, 4, 5 };
            var blockData = new byte[] { 10, 20, 30 };

            var sections = new List<AnchorPayloadSection>
            {
                new() { Type = AnchorPayloadSectionType.StateRoot, Bytes = stateRoot },
                new() { Type = AnchorPayloadSectionType.InlineProof, Bytes = proofBytes },
                new() { Type = AnchorPayloadSectionType.InlineDa, Bytes = blockData }
            };

            var payload = AnchorPayloadCodec.Build(
                StateModel.MptKeccak, AnchorKind.Block, sections);

            var encoded = AnchorPayloadCodec.Encode(payload);
            var decoded = AnchorPayloadCodec.Decode(encoded);

            Assert.Equal(3, decoded.Sections.Count);

            var decodedRoot = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.StateRoot);
            Assert.Equal(stateRoot, decodedRoot.Bytes);

            var decodedProof = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof);
            Assert.Equal(proofBytes, decodedProof.Bytes);

            var decodedDa = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineDa);
            Assert.Equal(blockData, decodedDa.Bytes);

            _output.WriteLine($"Tier 3 payload: {encoded.Length} bytes, {decoded.Sections.Count} sections");
        }

        [Fact]
        public void ShouldRoundtripTier4PayloadWithCommitments()
        {
            var stateRoot = new byte[32];
            var proofCommitment = new byte[32];
            proofCommitment[0] = 0xBB;
            var daCommitment = new byte[32];
            daCommitment[0] = 0xCC;
            var validatedPointer = System.BitConverter.GetBytes(42L);

            var sections = new List<AnchorPayloadSection>
            {
                new() { Type = AnchorPayloadSectionType.StateRoot, Bytes = stateRoot },
                new() { Type = AnchorPayloadSectionType.ProofCommitment, Bytes = proofCommitment },
                new() { Type = AnchorPayloadSectionType.DaCommitment, Bytes = daCommitment },
                new() { Type = AnchorPayloadSectionType.PreviousValidatedPointer, Bytes = validatedPointer }
            };

            var payload = AnchorPayloadCodec.Build(
                StateModel.BinaryPoseidon, AnchorKind.Batch, sections);

            var encoded = AnchorPayloadCodec.Encode(payload);
            var decoded = AnchorPayloadCodec.Decode(encoded);

            Assert.Equal((byte)StateModel.BinaryPoseidon, decoded.Header.StateModel);
            Assert.Equal((byte)AnchorKind.Batch, decoded.Header.AnchorKind);
            Assert.Equal(4, decoded.Sections.Count);

            var pointer = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.PreviousValidatedPointer);
            Assert.Equal(42L, System.BitConverter.ToInt64(pointer.Bytes));

            _output.WriteLine($"Tier 4 payload: {encoded.Length} bytes, stateModel=BinaryPoseidon, kind=Batch");
        }

        [Fact]
        public void ShouldPreserveStateModelByte()
        {
            foreach (var model in new[] { StateModel.MptKeccak, StateModel.BinaryPoseidon, StateModel.BinarySha256 })
            {
                var payload = AnchorPayloadCodec.Build(model, AnchorKind.Block, new List<AnchorPayloadSection>());
                var encoded = AnchorPayloadCodec.Encode(payload);
                var decoded = AnchorPayloadCodec.Decode(encoded);
                Assert.Equal((byte)model, decoded.Header.StateModel);
                _output.WriteLine($"StateModel {model} = 0x{(byte)model:X2} roundtripped");
            }
        }

        [Fact]
        public void ShouldRejectUnsupportedVersion()
        {
            var data = new byte[] { 99, 0, 0, 0 };
            var ex = Assert.Throws<System.NotSupportedException>(() => AnchorPayloadCodec.Decode(data));
            Assert.Contains("version", ex.Message.ToLower());
            _output.WriteLine($"Rejected version 99: {ex.Message}");
        }

        [Fact]
        public void ShouldRejectTruncatedData()
        {
            var sections = new List<AnchorPayloadSection>
            {
                new() { Type = AnchorPayloadSectionType.StateRoot, Bytes = new byte[32] }
            };
            var payload = AnchorPayloadCodec.Build(StateModel.MptKeccak, AnchorKind.Block, sections);
            var encoded = AnchorPayloadCodec.Encode(payload);

            var truncated = new byte[encoded.Length - 10];
            System.Array.Copy(encoded, truncated, truncated.Length);

            Assert.Throws<System.ArgumentException>(() => AnchorPayloadCodec.Decode(truncated));
            _output.WriteLine("Truncated data correctly rejected");
        }

        [Fact]
        public void ShouldFindAndFindSections()
        {
            var sections = new List<AnchorPayloadSection>
            {
                new() { Type = AnchorPayloadSectionType.StateRoot, Bytes = new byte[] { 1 } },
                new() { Type = AnchorPayloadSectionType.MessageRoot, Bytes = new byte[] { 2 } },
                new() { Type = AnchorPayloadSectionType.MessageRoot, Bytes = new byte[] { 3 } }
            };
            var payload = AnchorPayloadCodec.Build(StateModel.MptKeccak, AnchorKind.Block, sections);
            var encoded = AnchorPayloadCodec.Encode(payload);
            var decoded = AnchorPayloadCodec.Decode(encoded);

            var single = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.StateRoot);
            Assert.NotNull(single);

            var missing = AnchorPayloadCodec.FindSection(decoded, AnchorPayloadSectionType.InlineProof);
            Assert.Null(missing);

            var multi = AnchorPayloadCodec.FindSections(decoded, AnchorPayloadSectionType.MessageRoot);
            Assert.Equal(2, multi.Count);

            _output.WriteLine("FindSection/FindSections work correctly");
        }
    }
}
