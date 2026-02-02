using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Signer.Bls.Tests
{
    public class NativeBlsTests
    {
        [Fact]
        public void VerifyAggregate_Throws_WhenNotInitialized()
        {
            var native = new NativeBls(new FakeBindings());

            var exception = Assert.Throws<InvalidOperationException>(() =>
                native.VerifyAggregate(Array.Empty<byte>(), Array.Empty<byte[]>(), Array.Empty<byte[]>(), Array.Empty<byte>()));

            Assert.Contains("initialized", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task VerifyAggregate_Forwards_ToBindings()
        {
            var bindings = new FakeBindings { VerificationResult = true };
            var native = new NativeBls(bindings);

            await native.InitializeAsync();
            var result = native.VerifyAggregate(
                new byte[] { 0x01 },
                new[] { new byte[] { 0x02 } },
                new[] { new byte[] { 0x03 } },
                new byte[] { 0x04 });

            Assert.True(result);
            Assert.True(bindings.VerifyCalled);
        }

        [Fact]
        public async Task VerifyAggregate_Allows_FastAggregate_WithSingleMessage()
        {
            var bindings = new FakeBindings { VerificationResult = true };
            var native = new NativeBls(bindings);

            await native.InitializeAsync();
            var result = native.VerifyAggregate(
                new byte[] { 0x01 },
                new[]
                {
                    new byte[] { 0x02 },
                    new byte[] { 0x03 }
                },
                new[] { new byte[] { 0x04 } },
                new byte[] { 0x05 });

            Assert.True(result);
            Assert.True(bindings.VerifyCalled);
        }

        [Fact]
        public async Task InitializeAsync_IsIdempotent()
        {
            var bindings = new FakeBindings();
            var native = new NativeBls(bindings);

            await native.InitializeAsync();
            await native.InitializeAsync();

            Assert.Equal(1, bindings.InitializationCount);
        }

        private sealed class FakeBindings : INativeBlsBindings
        {
            public bool VerifyCalled { get; private set; }
            public int InitializationCount { get; private set; }
            public bool VerificationResult { get; set; }

            public Task EnsureAvailableAsync(CancellationToken cancellationToken)
            {
                InitializationCount++;
                return Task.CompletedTask;
            }

            public bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain)
            {
                VerifyCalled = true;
                return VerificationResult;
            }

            public byte[] AggregateSignatures(byte[][] signatures)
            {
                return signatures.Length > 0 ? signatures[0] : Array.Empty<byte>();
            }

            public bool Verify(byte[] signature, byte[] publicKey, byte[] message)
            {
                VerifyCalled = true;
                return VerificationResult;
            }
        }
    }
}
