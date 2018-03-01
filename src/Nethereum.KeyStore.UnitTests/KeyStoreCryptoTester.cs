using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Crypto;
using Xunit;

namespace Nethereum.KeyStore.UnitTests
{
    public class KeyStoreCryptoTester
    {
        private readonly KeyStoreCrypto keyStoreCrypto = new KeyStoreCrypto();

        [Fact]
        public void ShouldDecryptPbkdf2Sha256()
        {
            var iv = "6087dab2f9fdbbfaddc31a909735c1e6".HexToByteArray();
            var cipherText = "5318b4d5bcd28de64ee5559e671353e16f075ecae9f99c7a79a38af5f869aa46".HexToByteArray();
            var dklen = 32;
            var c = 262144;
            var salt = "ae3cd4e7013836a3df6bd7241b12db061dbe2c6785853cce422d148a624ce0bd".HexToByteArray();
            var mac = "517ead924a9d0dc3124507e3393d175ce3ff7c1e96529c6c555ce9e51205e9b2".HexToByteArray();
            var password = "testpassword";

            var result = keyStoreCrypto.DecryptPbkdf2Sha256(password, mac, iv, cipherText, c, salt, dklen);

            Assert.Equal("7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d", result.ToHex());
        }

        [Fact]
        public void ShouldDecryptScryptHmacSha256()
        {
            var iv = "83dbcc02d8ccb40e466191a123791e0e".HexToByteArray();
            var cipherText = "d172bf743a674da9cdad04534d56926ef8358534d458fffccd4e6ad2fbde479c".HexToByteArray();
            var dklen = 32;
            var n = 262144;
            var r = 1;
            var p = 8;
            var salt = "ab0c7876052600dd703518d6fc3fe8984592145b591fc8fb5c6d43190334ba19".HexToByteArray();
            var mac = "2103ac29920d71da29f15d75b4a16dbe95cfd7ff8faea1056c33131d846e3097".HexToByteArray();
            var password = "testpassword";

            var result = keyStoreCrypto.DecryptScrypt(password, mac, iv, cipherText, n, p, r, salt, dklen);

            Assert.Equal("7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d", result.ToHex());
        }

        [Fact]
        public void ShouldGenerateAES128DerivedKey()
        {
            var password = "testpassword";
            var c = 262144;
            var dklen = 32;
            var salt = "ae3cd4e7013836a3df6bd7241b12db061dbe2c6785853cce422d148a624ce0bd";

            var derived = keyStoreCrypto.GeneratePbkdf2Sha256DerivedKey(password, salt.HexToByteArray(), c, dklen);
            Assert.Equal("f06d69cdc7da0faffb1008270bca38f5e31891a3a773950e6d0fea48a7188551", derived.ToHex(false));
        }

        [Fact]
        public void ShouldGenerateCipherText()
        {
            var derivedKey = "f06d69cdc7da0faffb1008270bca38f5e31891a3a773950e6d0fea48a7188551".HexToByteArray();
            var privateKey = "7a28b5ba57c53603b0b07b56bba752f7784bf506fa95edc395f5cf6c7514fe9d".HexToByteArray();

            var cypherKey = keyStoreCrypto.GenerateCipherKey(derivedKey);
            Assert.Equal("f06d69cdc7da0faffb1008270bca38f5", cypherKey.ToHex());


            var iv = "6087dab2f9fdbbfaddc31a909735c1e6".HexToByteArray();
            var result = keyStoreCrypto.GenerateAesCtrCipher(iv, cypherKey, privateKey);

            Assert.Equal("5318b4d5bcd28de64ee5559e671353e16f075ecae9f99c7a79a38af5f869aa46", result.ToHex());
        }

        [Fact]
        public void ShouldGenerateDerivedScryptKey()
        {
            var N = 262144;
            var R = 1;
            var P = 8;
            var DKLEN = 32;
            var salt = "ab0c7876052600dd703518d6fc3fe8984592145b591fc8fb5c6d43190334ba19".HexToByteArray();
            var password = "testpassword";
            var derived =
                keyStoreCrypto.GenerateDerivedScryptKey(Encoding.UTF8.GetBytes(password), salt, N, R, P, DKLEN);
            var result = "fac192ceb5fd772906bea3e118a69e8bbb5cc24229e20d8766fd298291bba6bd";
            Assert.Equal(result, derived.ToHex());
        }

        [Fact]
        public void ShouldGenerateMac()
        {
            var derivedKey = "f06d69cdc7da0faffb1008270bca38f5e31891a3a773950e6d0fea48a7188551".HexToByteArray();
            var cipherText = "5318b4d5bcd28de64ee5559e671353e16f075ecae9f99c7a79a38af5f869aa46".HexToByteArray();

            var result = keyStoreCrypto.GenerateMac(derivedKey, cipherText);

            Assert.Equal("517ead924a9d0dc3124507e3393d175ce3ff7c1e96529c6c555ce9e51205e9b2", result.ToHex());
        }
    }
}