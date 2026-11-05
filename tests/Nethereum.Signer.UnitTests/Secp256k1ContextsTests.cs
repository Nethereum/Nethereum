using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Signer.UnitTests
{
    [CollectionDefinition("Secp256k1ColdContexts", DisableParallelization = true)]
    public sealed class Secp256k1ColdContextsCollection { }

    [Collection("Secp256k1ColdContexts")]
    public class Secp256k1ContextsTests
    {
        private readonly ITestOutputHelper _output;

        public Secp256k1ContextsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Given_ContextsInitialisedAtStartup_When_TheFirstRecoveryAndSignRun_Then_NeitherPaysTheContextConstruction()
        {
            var warm = ColdProcess.Run(initialiseFirst: true);
            _output.WriteLine($"initialise {warm.InitialiseMs} ms, first recovery {warm.FirstRecoveryMs} ms, first sign {warm.FirstSignMs} ms");

            Assert.True(warm.FirstRecoveryMs < warm.InitialiseMs / 4,
                $"first recovery {warm.FirstRecoveryMs} ms after an initialise of {warm.InitialiseMs} ms");
            Assert.True(warm.FirstSignMs < warm.InitialiseMs / 4,
                $"first sign {warm.FirstSignMs} ms after an initialise of {warm.InitialiseMs} ms");
        }

        [Fact]
        public void Given_ContextsNotInitialised_When_TheFirstRecoveryRuns_Then_ItPaysTheContextConstruction()
        {
            var warm = ColdProcess.Run(initialiseFirst: true);
            _output.WriteLine($"initialise {warm.InitialiseMs} ms, first recovery {warm.FirstRecoveryMs} ms, first sign {warm.FirstSignMs} ms");
            var cold = ColdProcess.Run(initialiseFirst: false);
            _output.WriteLine($"cold first recovery {cold.FirstRecoveryMs} ms, cold first sign {cold.FirstSignMs} ms");

            Assert.True(cold.FirstRecoveryMs > warm.InitialiseMs / 2,
                $"cold first recovery {cold.FirstRecoveryMs} ms against an initialise of {warm.InitialiseMs} ms");
        }

        [Fact]
        public void Given_ContextsAlreadyInitialised_When_InitialisedAgain_Then_ItReturnsImmediately()
        {
            Secp256k1Contexts.EnsureInitialised();
            var watch = Stopwatch.StartNew();
            Secp256k1Contexts.EnsureInitialised();

            Assert.True(Secp256k1Contexts.IsInitialised);
            Assert.True(watch.ElapsedMilliseconds < 5);
        }
    }

    public readonly struct ColdTimings
    {
        public ColdTimings(double initialiseMs, double firstRecoveryMs, double firstSignMs)
        {
            InitialiseMs = initialiseMs;
            FirstRecoveryMs = firstRecoveryMs;
            FirstSignMs = firstSignMs;
        }

        public double InitialiseMs { get; }
        public double FirstRecoveryMs { get; }
        public double FirstSignMs { get; }
    }

    public static class ColdProcess
    {
        public static ColdTimings Run(bool initialiseFirst)
        {
            var directory = Path.GetDirectoryName(typeof(ColdProcess).Assembly.Location);
            var context = new FreshNethereumContext(directory);
            var assembly = context.LoadFromAssemblyPath(typeof(ColdProcess).Assembly.Location);
            var measure = assembly.GetType(typeof(ColdMeasurement).FullName).GetMethod(nameof(ColdMeasurement.Measure));
            var values = (double[])measure.Invoke(null, new object[] { initialiseFirst });
            return new ColdTimings(values[0], values[1], values[2]);
        }

        private sealed class FreshNethereumContext : AssemblyLoadContext
        {
            private readonly string _directory;

            public FreshNethereumContext(string directory)
            {
                _directory = directory;
            }

            protected override Assembly Load(AssemblyName name)
            {
                var fresh = name.Name.StartsWith("Nethereum.") || name.Name.StartsWith("NBitcoin") || name.Name.StartsWith("BouncyCastle");
                var path = Path.Combine(_directory, name.Name + ".dll");
                return fresh && File.Exists(path) ? LoadFromAssemblyPath(path) : null;
            }
        }
    }

    public static class ColdMeasurement
    {
        public static double[] Measure(bool initialiseFirst)
        {
            var key = new EthECKey("0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318");
            var hash = new byte[32];
            hash[31] = 7;
            EthECKey.SignRecoverable = false;
            var signature = key.SignAndCalculateV(hash);
            EthECKey.SignRecoverable = true;

            var initialiseMs = 0d;
            if (initialiseFirst)
            {
                var initialise = Stopwatch.StartNew();
                Secp256k1Contexts.EnsureInitialised();
                initialiseMs = initialise.Elapsed.TotalMilliseconds;
            }

            var recovery = Stopwatch.StartNew();
            var recovered = EthECKey.RecoverFromSignature(signature, hash);
            var recoveryMs = recovery.Elapsed.TotalMilliseconds;
            if (recovered.GetPublicAddress() != key.GetPublicAddress()) throw new InvalidOperationException("recovered a different address");

            var sign = Stopwatch.StartNew();
            key.SignAndCalculateV(hash);
            return new[] { initialiseMs, recoveryMs, sign.Elapsed.TotalMilliseconds };
        }
    }
}
