using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util.HashProviders;
using Org.BouncyCastle.Crypto.Digests;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class Sha3KeccackReuseTests
    {
        private const string EmptyInputKnownVector =
            "c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470";

        public static IEnumerable<object[]> Corpus()
        {
            yield return new object[] { "empty", Array.Empty<byte>() };
            yield return new object[] { "1 byte", new byte[] { 0x42 } };
            yield return new object[] { "31 bytes", MakeSequential(31) };
            yield return new object[] { "32 bytes", MakeSequential(32) };
            yield return new object[] { "33 bytes", MakeSequential(33) };
            yield return new object[] { "1 KB", MakeSequential(1024) };
            yield return new object[] { "all-zero (64 bytes)", new byte[64] };
            yield return new object[] { "high-entropy (256 bytes)", MakeHighEntropy(256, seed: 12345) };
        }

        private static byte[] OracleKeccak256(byte[] input)
        {
            var digest = new KeccakDigest(256);
            var output = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(input, 0, input.Length);
            digest.DoFinal(output, 0);
            return output;
        }

        private static byte[] MakeSequential(int length)
        {
            var bytes = new byte[length];
            for (var i = 0; i < length; i++)
            {
                bytes[i] = (byte)(i % 256);
            }
            return bytes;
        }

        private static byte[] MakeHighEntropy(int length, int seed)
        {
            var random = new System.Random(seed);
            var bytes = new byte[length];
            random.NextBytes(bytes);
            return bytes;
        }

        [Theory]
        [MemberData(nameof(Corpus))]
        public void CalculateHash_MatchesIndependentOracle(string label, byte[] input)
        {
            Assert.False(string.IsNullOrEmpty(label));

            var actual = Sha3Keccack.Current.CalculateHash(input);
            var expected = OracleKeccak256(input);

            Assert.Equal(expected.ToHex(), actual.ToHex());
        }

        [Fact]
        public void CalculateHash_EmptyInput_MatchesHardcodedKnownVector()
        {
            var actual = Sha3Keccack.Current.CalculateHash(Array.Empty<byte>());

            Assert.Equal(EmptyInputKnownVector, actual.ToHex());
        }

        [Theory]
        [MemberData(nameof(Corpus))]
        public void ProviderSingleton_MatchesFreshInstance(string label, byte[] input)
        {
            Assert.False(string.IsNullOrEmpty(label));

            var fromSingleton = Sha3KeccackHashProvider.Instance.ComputeHash(input);
            var fromNewInstance = new Sha3KeccackHashProvider().ComputeHash(input);

            Assert.Equal(fromNewInstance.ToHex(), fromSingleton.ToHex());
        }

        [Fact]
        public void ProviderSingleton_IsReusedAcrossCalls()
        {
            Assert.Same(Sha3KeccackHashProvider.Instance, Sha3KeccackHashProvider.Instance);
        }

        [Fact]
        public void CalculateHash_UnderConcurrency_ThreadLocalDigestIsNotSharedOrTorn()
        {
            const int threadCount = 8;
            const int iterationsPerThread = 5000;

            var exceptions = new List<Exception>();
            var exceptionsLock = new object();
            var barrier = new Barrier(threadCount);
            var threads = new Thread[threadCount];

            for (var t = 0; t < threadCount; t++)
            {
                var threadIndex = t;
                threads[t] = new Thread(() =>
                {
                    try
                    {
                        var inputs = new byte[4][];
                        for (var i = 0; i < inputs.Length; i++)
                        {
                            inputs[i] = MakeHighEntropy(64 + i, seed: threadIndex * 1000 + i);
                        }

                        var expected = new byte[inputs.Length][];
                        for (var i = 0; i < inputs.Length; i++)
                        {
                            expected[i] = OracleKeccak256(inputs[i]);
                        }

                        barrier.SignalAndWait();

                        for (var iteration = 0; iteration < iterationsPerThread; iteration++)
                        {
                            for (var i = 0; i < inputs.Length; i++)
                            {
                                var actual = Sha3Keccack.Current.CalculateHash(inputs[i]);
                                if (actual.ToHex() != expected[i].ToHex())
                                {
                                    throw new InvalidOperationException(
                                        $"Thread {threadIndex} iteration {iteration} input {i}: expected {expected[i].ToHex()} but got {actual.ToHex()}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptionsLock)
                        {
                            exceptions.Add(ex);
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

            Assert.Empty(exceptions);
        }
    }
}
