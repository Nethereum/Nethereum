using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util.HashProviders;
using Xunit;
using BcKeccakDigest = Org.BouncyCastle.Crypto.Digests.KeccakDigest;
using NethereumKeccakDigest = Nethereum.Util.Keccak.KeccakDigest;

namespace Nethereum.Util.UnitTests
{
    public class Keccak256Tests
    {
        public static IEnumerable<object[]> KnownAnswers()
        {
            yield return new object[] { Array.Empty<byte>(), "c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470" };
            yield return new object[] { Encoding.ASCII.GetBytes("abc"), "4e03657aea45a94fc7d47ba826c8d667c0d1e6e33a64a036ec44f58fa12d6c45" };
            yield return new object[] { Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog"), "4d741b6f1eb29cb2a9b9911c82f56fa8d73b04959d3d9d222895df6c0b28aa15" };
            yield return new object[] { new byte[] { 0x80 }, "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421" };
            yield return new object[] { new byte[] { 0xc0 }, "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347" };
            yield return new object[] { Filled(200, 0xa3), "3a57666b048777f2c953dc4456f45a2588e1cb6f2da760122d530ac2ce607d4a" };
            yield return new object[] { Sequential(135), "cbdfd9dee5faad3818d6b06f95a219fd290b0e1706f6a82e5a595b9ce9faca62" };
            yield return new object[] { Sequential(136), "7ce759f1ab7f9ce437719970c26b0a66ff11fe3e38e17df89cf5d29c7d7f807e" };
            yield return new object[] { Sequential(137), "ac73d4fae68b8453f764007c1a20ce95994187861f0c3227a3a8e99a73a3b1db" };
            yield return new object[] { Sequential(272), "fdf2ec49e749960d3c8521a0219af8d03e30e2b3bf19bd16150ee0eaf133d66e" };
            yield return new object[] { Sequential(413), "42c05358750e234b362cda466256976eaca19fa6c03dc6e7a745c05ee834a80b" };
            yield return new object[] { Sequential(532), "6cec40d944dc321c7811a4768339b0e3a6779b92a33b4b2598731fdb45fcd2a7" };
        }

        [Theory]
        [MemberData(nameof(KnownAnswers))]
        public void CalculateHash_KnownAnswer_MatchesPublishedDigest(byte[] input, string expected)
        {
            Assert.Equal(expected, Sha3Keccack.Current.CalculateHash(input).ToHex());
            Assert.Equal(expected, Sha3KeccackHashProvider.Instance.ComputeHash(input).ToHex());
            Assert.Equal(expected, BouncyCastleKeccak(256, input).ToHex());
        }

        [Theory]
        [MemberData(nameof(KnownAnswers))]
        public void KeccakDigest_KnownAnswer_MatchesPublishedDigest(byte[] input, string expected)
        {
            var digest = new NethereumKeccakDigest(256);
            var output = new byte[32];
            digest.BlockUpdate(input, 0, input.Length);
            digest.DoFinal(output, 0);

            Assert.Equal(expected, output.ToHex());
        }

        [Theory]
        [MemberData(nameof(KnownAnswers))]
        public void CalculateHashIntoBuffer_KnownAnswer_MatchesPublishedDigest(byte[] input, string expected)
        {
            var output = new byte[32];
            Sha3Keccack.Current.CalculateHash(input, 0, input.Length, output, 0);

            Assert.Equal(expected, output.ToHex());
        }

        [Fact]
        public void CalculateHash_RandomLengthsUpTo2000_MatchesBouncyCastle()
        {
            var random = new System.Random(20261002);
            for (var i = 0; i < 4000; i++)
            {
                var input = RandomBytes(random, random.Next(0, 2001));

                var expected = BouncyCastleKeccak(256, input).ToHex();

                Assert.Equal(expected, Sha3Keccack.Current.CalculateHash(input).ToHex());
            }
        }

        [Fact]
        public void CalculateHash_EveryLengthAcrossFourBlocks_MatchesBouncyCastle()
        {
            var random = new System.Random(7);
            for (var length = 0; length <= 4 * 136 + 8; length++)
            {
                var input = RandomBytes(random, length);

                Assert.Equal(BouncyCastleKeccak(256, input).ToHex(), Sha3Keccack.Current.CalculateHash(input).ToHex());
            }
        }

        [Fact]
        public void CalculateHashIntoBuffer_HashesOnlyTheSliceAndWritesOnlyTheOutputWindow()
        {
            var random = new System.Random(99);
            for (var i = 0; i < 2000; i++)
            {
                var length = random.Next(0, 1200);
                var offset = random.Next(0, 40);
                var buffer = RandomBytes(random, offset + length + random.Next(0, 40));
                var slice = new byte[length];
                Array.Copy(buffer, offset, slice, 0, length);
                var outputOffset = random.Next(0, 16);
                var output = RandomBytes(random, outputOffset + 32 + random.Next(0, 16));
                var guard = (byte[])output.Clone();

                Sha3Keccack.Current.CalculateHash(buffer, offset, length, output, outputOffset);

                var expected = BouncyCastleKeccak(256, slice);
                for (var b = 0; b < output.Length; b++)
                {
                    var inWindow = b >= outputOffset && b < outputOffset + 32;
                    Assert.Equal(inWindow ? expected[b - outputOffset] : guard[b], output[b]);
                }
            }
        }

        [Theory]
        [InlineData(128)]
        [InlineData(224)]
        [InlineData(256)]
        [InlineData(288)]
        [InlineData(384)]
        [InlineData(512)]
        public void KeccakDigest_RandomChunkedUpdates_MatchesBouncyCastle(int bitLength)
        {
            var random = new System.Random(bitLength);
            var digest = new NethereumKeccakDigest(bitLength);
            for (var i = 0; i < 400; i++)
            {
                var input = RandomBytes(random, random.Next(0, 1500));
                var position = 0;
                while (position < input.Length)
                {
                    if (random.Next(0, 8) == 0)
                    {
                        digest.Update(input[position]);
                        position++;
                        continue;
                    }

                    var chunk = Math.Min(random.Next(1, 300), input.Length - position);
                    digest.BlockUpdate(input, position, chunk);
                    position += chunk;
                }

                var output = new byte[digest.GetDigestSize()];
                digest.DoFinal(output, 0);

                Assert.Equal(BouncyCastleKeccak(bitLength, input).ToHex(), output.ToHex());
            }
        }

        [Fact]
        public void KeccakDigest_CopyConstructor_ForksStateWithoutSharingIt()
        {
            var random = new System.Random(5);
            var prefix = RandomBytes(random, 300);
            var left = RandomBytes(random, 77);
            var right = RandomBytes(random, 211);

            var original = new NethereumKeccakDigest(256);
            original.BlockUpdate(prefix, 0, prefix.Length);
            var fork = new NethereumKeccakDigest(original);
            original.BlockUpdate(left, 0, left.Length);
            fork.BlockUpdate(right, 0, right.Length);

            var originalOutput = new byte[32];
            var forkOutput = new byte[32];
            original.DoFinal(originalOutput, 0);
            fork.DoFinal(forkOutput, 0);

            Assert.Equal(BouncyCastleKeccak(256, Concat(prefix, left)).ToHex(), originalOutput.ToHex());
            Assert.Equal(BouncyCastleKeccak(256, Concat(prefix, right)).ToHex(), forkOutput.ToHex());
        }

        [Fact]
        public void CalculateHashIntoBuffer_NullInput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Sha3Keccack.Current.CalculateHash(null, 0, 0, new byte[32], 0));
        }

        [Fact]
        public void CalculateHashIntoBuffer_NullOutput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Sha3Keccack.Current.CalculateHash(new byte[4], 0, 4, null, 0));
        }

        [Theory]
        [InlineData(-1, 4)]
        [InlineData(0, -1)]
        [InlineData(0, 11)]
        [InlineData(5, 6)]
        [InlineData(11, 0)]
        public void CalculateHashIntoBuffer_SliceOutsideInput_Throws(int offset, int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Sha3Keccack.Current.CalculateHash(new byte[10], offset, length, new byte[32], 0));
        }

        [Theory]
        [InlineData(31, 0)]
        [InlineData(32, 1)]
        [InlineData(40, -1)]
        public void CalculateHashIntoBuffer_OutputWindowTooSmall_Throws(int outputLength, int outputOffset)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Sha3Keccack.Current.CalculateHash(new byte[10], 0, 10, new byte[outputLength], outputOffset));
        }

        [Fact]
        public void CalculateHashIntoBuffer_OffsetAtEndOfInputWithZeroLength_HashesTheEmptyString()
        {
            var input = new byte[] { 1, 2, 3, 4, 5 };
            var output = new byte[32];

            Sha3Keccack.Current.CalculateHash(input, input.Length, 0, output, 0);

            Assert.Equal("c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470", output.ToHex());
        }

        [Fact]
        public void CalculateHashIntoBuffer_OutputOverlapsInputInTheSameBuffer_HashesTheOriginalInput()
        {
            var random = new System.Random(31337);
            for (var i = 0; i < 2000; i++)
            {
                var length = random.Next(0, 700);
                var offset = random.Next(0, 20);
                var buffer = RandomBytes(random, offset + length + 32 + random.Next(0, 20));
                var slice = new byte[length];
                Array.Copy(buffer, offset, slice, 0, length);
                var outputOffset = random.Next(0, buffer.Length - 32 + 1);
                var guard = (byte[])buffer.Clone();

                Sha3Keccack.Current.CalculateHash(buffer, offset, length, buffer, outputOffset);

                var expected = BouncyCastleKeccak(256, slice);
                for (var b = 0; b < buffer.Length; b++)
                {
                    var inWindow = b >= outputOffset && b < outputOffset + 32;
                    Assert.Equal(inWindow ? expected[b - outputOffset] : guard[b], buffer[b]);
                }
            }
        }

        [Fact]
        public void CalculateHash_ManyThreadsHashingMixedSizes_MatchesSingleThreadedResults()
        {
            const int threadCount = 12;
            const int rounds = 300;
            var random = new System.Random(4242);
            var inputs = new byte[64][];
            for (var i = 0; i < inputs.Length; i++)
            {
                inputs[i] = RandomBytes(random, random.Next(0, 1400));
            }

            var expected = new string[inputs.Length];
            for (var i = 0; i < inputs.Length; i++)
            {
                expected[i] = Sha3Keccack.Current.CalculateHash(inputs[i]).ToHex();
                Assert.Equal(BouncyCastleKeccak(256, inputs[i]).ToHex(), expected[i]);
            }

            var mismatches = new int[threadCount];
            var barrier = new System.Threading.Barrier(threadCount);
            var threads = new System.Threading.Thread[threadCount];
            for (var t = 0; t < threadCount; t++)
            {
                var threadIndex = t;
                threads[t] = new System.Threading.Thread(() =>
                {
                    var output = new byte[32];
                    barrier.SignalAndWait();
                    for (var r = 0; r < rounds; r++)
                    {
                        for (var k = 0; k < inputs.Length; k++)
                        {
                            var i = (k + threadIndex * 7 + r) % inputs.Length;
                            if (Sha3Keccack.Current.CalculateHash(inputs[i]).ToHex() != expected[i])
                            {
                                mismatches[threadIndex]++;
                            }

                            Sha3Keccack.Current.CalculateHash(inputs[i], 0, inputs[i].Length, output, 0);
                            if (output.ToHex() != expected[i])
                            {
                                mismatches[threadIndex]++;
                            }
                        }
                    }
                });
            }

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.All(mismatches, count => Assert.Equal(0, count));
        }

        private static byte[] BouncyCastleKeccak(int bitLength, byte[] input)
        {
            var digest = new BcKeccakDigest(bitLength);
            var output = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(input, 0, input.Length);
            digest.DoFinal(output, 0);
            return output;
        }

        private static byte[] RandomBytes(System.Random random, int length)
        {
            var bytes = new byte[length];
            random.NextBytes(bytes);
            return bytes;
        }

        private static byte[] Sequential(int length)
        {
            var bytes = new byte[length];
            for (var i = 0; i < length; i++)
            {
                bytes[i] = (byte)i;
            }
            return bytes;
        }

        private static byte[] Filled(int length, byte value)
        {
            var bytes = new byte[length];
            for (var i = 0; i < length; i++)
            {
                bytes[i] = value;
            }
            return bytes;
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            Array.Copy(a, 0, result, 0, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}
