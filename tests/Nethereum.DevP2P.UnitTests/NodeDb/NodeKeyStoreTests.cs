using System;
using System.IO;
using Nethereum.DevP2P.NodeDb;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.NodeDb
{
    public class NodeKeyStoreTests : IDisposable
    {
        private readonly string _dir;

        public NodeKeyStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nodekey-tests-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        [Fact]
        public void Given_NoKeyFileExists_When_LoadOrCreate_Then_ItGeneratesAndPersistsAKey()
        {
            var path = Path.Combine(_dir, "nodekey");

            var key = NodeKeyStore.LoadOrCreate(path);

            Assert.True(File.Exists(path));
            Assert.Equal(32, File.ReadAllBytes(path).Length);
            Assert.NotNull(key.GetPrivateKeyAsBytes());
        }

        [Fact]
        public void Given_APersistedKeyFile_When_LoadOrCreateRunsAgain_Then_TheSameIdentityIsReturned()
        {
            var path = Path.Combine(_dir, "nodekey");

            var first = NodeKeyStore.LoadOrCreate(path);
            var second = NodeKeyStore.LoadOrCreate(path);

            Assert.Equal(first.GetPrivateKeyAsBytes(), second.GetPrivateKeyAsBytes());
            Assert.Equal(first.GetPubKeyNoPrefix(), second.GetPubKeyNoPrefix());
        }

        [Fact]
        public void Given_AWrongLengthKeyFile_When_LoadOrCreate_Then_ItThrowsAndDoesNotOverwriteTheFile()
        {
            var path = Path.Combine(_dir, "nodekey");
            Directory.CreateDirectory(_dir);
            var original = new byte[64];
            new Random(42).NextBytes(original);
            File.WriteAllBytes(path, original);

            Assert.Throws<InvalidOperationException>(() => NodeKeyStore.LoadOrCreate(path));

            Assert.Equal(original, File.ReadAllBytes(path));
        }

        [Fact]
        public void Given_A32ByteFileThatIsNotAValidPrivateKey_When_LoadOrCreate_Then_ItThrowsAndDoesNotOverwriteTheFile()
        {
            var path = Path.Combine(_dir, "nodekey");
            Directory.CreateDirectory(_dir);
            var original = new byte[32];
            File.WriteAllBytes(path, original);

            Assert.Throws<InvalidOperationException>(() => NodeKeyStore.LoadOrCreate(path));

            Assert.Equal(original, File.ReadAllBytes(path));
        }

        [Fact]
        public void Given_LoadOrCreate_When_ItRuns_Then_LogCallbackReceivesAMessage()
        {
            var path = Path.Combine(_dir, "nodekey");
            string logged = null;

            NodeKeyStore.LoadOrCreate(path, msg => logged = msg);

            Assert.NotNull(logged);
        }
    }
}
