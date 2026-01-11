using System.Linq;
using System.Numerics;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class PoseidonHasherTests
    {
        [Fact]
        public void CircomT3VectorsMatchCircomlib()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT3);

            Assert.Equal(
                BigInteger.Parse("5317387130258456662214331362918410991734007599705406860481038345552731150762"),
                hasher.Hash(BigInteger.Zero, BigInteger.Zero, BigInteger.Zero));

            Assert.Equal(
                BigInteger.Parse("16319005924338521988144249782199320915969277491928916027259324394544057385749"),
                hasher.Hash(BigInteger.One, BigInteger.Zero, BigInteger.Zero));

            Assert.Equal(
                BigInteger.Parse("13234400070188801104792523922697988244748411503422448631147834118387475842488"),
                hasher.Hash(new BigInteger(2), BigInteger.Zero, BigInteger.Zero));

            var largeInput = new[]
            {
                BigInteger.Parse("72057594037927936"),
                BigInteger.One,
                BigInteger.Parse("20634138280259599560273310290025659992320584624461316485434108770067472477956")
            };

            Assert.Equal(
                BigInteger.Parse("3135714887432857880402997813814046724922969450336546007917491784497158924950"),
                hasher.Hash(largeInput));
        }

        [Fact]
        public void CircomT6VectorMatchesCircomlib()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT6);
            var inputs = Enumerable.Range(1, 6).Select(i => new BigInteger(i)).ToArray();
            var expected = BigInteger.Parse("20400040500897583745843009878988256314335038853985262692600694741116813247201");

            Assert.Equal(expected, hasher.Hash(inputs));
        }

        [Fact]
        public void CircomT14VectorsMatchCircomlib()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT14);
            var sequential = Enumerable.Range(1, 14).Select(i => new BigInteger(i)).ToArray();
            var tailZero = Enumerable.Range(1, 9).Select(i => new BigInteger(i)).Concat(Enumerable.Repeat(BigInteger.Zero, 5)).ToArray();

            Assert.Equal(
                BigInteger.Parse("8354478399926161176778659061636406690034081872658507739535256090879947077494"),
                hasher.Hash(sequential));

            Assert.Equal(
                BigInteger.Parse("5540388656744764564518487011617040650780060800286365721923524861648744699539"),
                hasher.Hash(tailZero));
        }

        [Fact]
        public void CircomT16VectorsMatchCircomlib()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT16);
            var sequential = Enumerable.Range(1, 16).Select(i => new BigInteger(i)).ToArray();
            var tailZero = Enumerable.Range(1, 9).Select(i => new BigInteger(i)).Concat(Enumerable.Repeat(BigInteger.Zero, 7)).ToArray();

            Assert.Equal(
                BigInteger.Parse("9989051620750914585850546081941653841776809718687451684622678807385399211877"),
                hasher.Hash(sequential));

            Assert.Equal(
                BigInteger.Parse("11882816200654282475720830292386643970958445617880627439994635298904836126497"),
                hasher.Hash(tailZero));
        }

        [Fact]
        public void HashBytesMatchesFieldElementVersion()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT3);
            var inputs = new[] { BigInteger.One, new BigInteger(123456789), new BigInteger(987654321) };
            var expected = hasher.Hash(inputs);
            var byteInputs = inputs.Select(ToBigEndian).ToArray();

            var converted = hasher.HashBytes(byteInputs);
            Assert.Equal(expected, converted);
        }

        [Fact]
        public void HashHexMatchesBytes()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT3);
            var byteInputs = new[]
            {
                new byte[] {0x01, 0x02},
                new byte[] {0x03, 0x04},
                new byte[] {0x05, 0x06}
            };

            var fieldHash = hasher.HashBytes(byteInputs);
            var hexHash = hasher.HashHex("0x0102", "0x0304", "0x0506");
            Assert.Equal(fieldHash, hexHash);
        }

        [Fact]
        public void HashBytesTreatsInputAsBigEndian()
        {
            var hasher = new PoseidonHasher(PoseidonParameterPreset.CircomT3);
            var bigEndian = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var littleEndian = new byte[] { 0x04, 0x03, 0x02, 0x01, 0x00 };
            var value = new BigInteger(littleEndian);

            var asField = hasher.Hash(value, BigInteger.Zero, BigInteger.Zero);
            var asBytes = hasher.HashBytes(bigEndian, new byte[] { 0x00 }, new byte[] { 0x00 });

            Assert.Equal(asField, asBytes);
        }

        private static byte[] ToBigEndian(BigInteger value)
        {
            var bytes = value.ToByteArray();
            var bigEndian = bytes.Reverse().SkipWhile(b => b == 0).ToArray();
            return bigEndian.Length == 0 ? new[] { (byte)0x00 } : bigEndian;
        }
    }
}
