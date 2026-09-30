using System;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StateValueEnvelopeTests
    {
        [Fact]
        public void Encode_Then_Decode_Roundtrips_Version_And_Payload()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            var wrapped = StateValueEnvelope.Encode(payload);

            Assert.Equal(StateValueEnvelope.CurrentVersion, wrapped[0]);

            var span = StateValueEnvelope.Decode(wrapped, out var ver);
            Assert.Equal(StateValueEnvelope.CurrentVersion, ver);
            Assert.True(span.SequenceEqual(payload));
        }

        [Fact]
        public void Decode_Unknown_Version_Throws()
        {
            var bad = new byte[] { 2, 9, 9 };
            Assert.Throws<NotSupportedException>(() => { StateValueEnvelope.Decode(bad, out _); });
        }

        [Fact]
        public void Decode_Empty_Throws()
        {
            Assert.Throws<NotSupportedException>(() => { StateValueEnvelope.Decode(Array.Empty<byte>(), out _); });
        }

        [Fact]
        public void Decode_Aliases_Underlying_Array_Zero_Copy()
        {
            var wrapped = StateValueEnvelope.Encode(new byte[] { 7, 8, 9 });
            var span = StateValueEnvelope.Decode(wrapped, out _);

            wrapped[1] = 0x42;
            Assert.Equal(0x42, span[0]);
        }
    }
}
