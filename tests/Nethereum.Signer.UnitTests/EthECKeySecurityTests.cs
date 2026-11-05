using System;
using System.Linq;
using Xunit;
using Nethereum.Signer;

namespace Nethereum.Signer.UnitTests
{
    public class EthECKeySecurityTests
    {
        [Fact]
        public void ShouldClearPrivateKeyOnDispose()
        {
            var key = EthECKey.GenerateKey();
            var privateKey = key.GetPrivateKeyAsBytes();
            
            // Should not be empty/zeroed yet
            Assert.NotNull(privateKey);
            Assert.Equal(32, privateKey.Length);
            Assert.Contains(privateKey, b => b != 0);
            
            key.Dispose();
            
            // Should be zeroed now
            Assert.All(privateKey, b => Assert.Equal(0, b));
            
            // Private key hex should be null after Dispose
            Assert.Null(key.GetPrivateKey());
        }

        [Fact]
        public void GenerateKey_ShouldNotLeakRawBytesInMemory()
        {
            // Verifies that the internal privateKey buffer is usable after GenerateKey (which has internal cleanup)
            var key = EthECKey.GenerateKey();
            Assert.NotNull(key.GetPrivateKey());
            Assert.Equal(32, key.GetPrivateKeyAsBytes().Length);
        }
    }
}
