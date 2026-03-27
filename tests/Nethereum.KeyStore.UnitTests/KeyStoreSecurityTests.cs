using System;
using System.Linq;
using Xunit;
using Nethereum.KeyStore.Crypto;

namespace Nethereum.KeyStore.UnitTests
{
    public class KeyStoreSecurityTests
    {
        [Fact]
        public void Decrypt_ShouldClearDerivedKeyBuffer()
        {
            var crypto = new KeyStoreCrypto();
            var derivedKey = new byte[32];
            for (int i = 0; i < 32; i++) derivedKey[i] = (byte)(i + 1); // Avoid zeros
            
            var iv = new byte[16];
            var cipherText = new byte[32];
            var mac = crypto.GenerateMac(derivedKey, cipherText);
            
            // Verify it's not zeroed
            Assert.Contains(derivedKey, b => b != 0);
            
            crypto.Decrypt(mac, iv, cipherText, derivedKey);
            
            // Should be zeroed now
            Assert.All(derivedKey, b => Assert.Equal(0, b));
        }
    }
}
