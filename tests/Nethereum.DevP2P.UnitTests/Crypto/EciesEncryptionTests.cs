using System.Security.Cryptography;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Crypto
{
    public class EciesEncryptionTests
    {
        [Fact]
        public void Encrypt_Decrypt_RoundTrip()
        {
            var recipient = EthECKey.GenerateKey();
            var plaintext = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var authData = new byte[] { 0x00, 0x42 };

            var ciphertext = EciesEncryption.Encrypt(
                recipient.GetPubKeyNoPrefix(), plaintext, authData);

            var decrypted = EciesEncryption.Decrypt(
                recipient.GetPrivateKeyAsBytes(), ciphertext, authData);

            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void Encrypt_Overhead_Is113Bytes()
        {
            var recipient = EthECKey.GenerateKey();
            var plaintext = new byte[32];

            var ciphertext = EciesEncryption.Encrypt(
                recipient.GetPubKeyNoPrefix(), plaintext, Array.Empty<byte>());

            Assert.Equal(plaintext.Length + EciesEncryption.Overhead, ciphertext.Length);
        }

        [Fact]
        public void Decrypt_WrongKey_Throws()
        {
            var recipient = EthECKey.GenerateKey();
            var wrongKey = EthECKey.GenerateKey();
            var plaintext = new byte[] { 1, 2, 3 };

            var ciphertext = EciesEncryption.Encrypt(
                recipient.GetPubKeyNoPrefix(), plaintext, Array.Empty<byte>());

            Assert.ThrowsAny<CryptographicException>(() =>
                EciesEncryption.Decrypt(
                    wrongKey.GetPrivateKeyAsBytes(), ciphertext, Array.Empty<byte>()));
        }

        [Fact]
        public void Decrypt_TamperedCiphertext_Throws()
        {
            var recipient = EthECKey.GenerateKey();
            var plaintext = new byte[] { 1, 2, 3, 4, 5 };

            var ciphertext = EciesEncryption.Encrypt(
                recipient.GetPubKeyNoPrefix(), plaintext, Array.Empty<byte>());

            ciphertext[70] ^= 0xFF;

            Assert.ThrowsAny<CryptographicException>(() =>
                EciesEncryption.Decrypt(
                    recipient.GetPrivateKeyAsBytes(), ciphertext, Array.Empty<byte>()));
        }

        [Fact]
        public void Decrypt_WrongAuthData_Throws()
        {
            var recipient = EthECKey.GenerateKey();
            var plaintext = new byte[] { 1, 2, 3, 4, 5 };

            var ciphertext = EciesEncryption.Encrypt(
                recipient.GetPubKeyNoPrefix(), plaintext, new byte[] { 0x01 });

            Assert.ThrowsAny<CryptographicException>(() =>
                EciesEncryption.Decrypt(
                    recipient.GetPrivateKeyAsBytes(), ciphertext, new byte[] { 0x02 }));
        }

        [Fact]
        public void Decrypt_EthereumTestVector()
        {
            var privateKey = "5e173f6ac3c669587538e7727cf19b782a4f2fda07c1eaa662c593e5e85e3051"
                .HexToByteArray();
            var ciphertext = ("049934a7b2d7f9af8fd9db941d9da281ac9381b5740e1f64f7092f3588d4f87f5c"
                + "e55191a6653e5e80c1c5dd538169aa123e70dc6ffc5af1827e546c0e958e42dad"
                + "355bcc1fcb9cdf2cf47ff524d2ad98cbf275e661bf4cf00960e74b5956b7997713"
                + "34f426df007350b46049adb21a6e78ab1408d5e6ccde6fb5e69f0f4c92bb9c725c"
                + "02f99fa72b9cdc8dd53cff089e0e73317f61cc5abf6152513cb7d833f09d285160"
                + "3919bf0fbe44d79a09245c6e8338eb502083dc84b846f2fee1cc310d2cc8b1b933"
                + "4728f97220bb799376233e113").HexToByteArray();
            var expectedPayload = ("802b052f8b066640bba94a4fc39d63815c377fced6fcb84d27f791c992"
                + "1ddf3e9bf0108e298f490812847109cbd778fae393e80323fd643209841a3b7f11"
                + "0397f37ec61d84cea03dcc5e8385db93248584e8af4b4d1c832d8c7453c0089687"
                + "a700").HexToByteArray();

            var decrypted = EciesEncryption.Decrypt(privateKey, ciphertext, Array.Empty<byte>());

            Assert.Equal(expectedPayload, decrypted);
        }


        [Fact]
        public void Ecdh_SharedSecret_KnownVector()
        {
            var key1 = new EthECKey(
                "7ebbc6a8358bc76dd73ebc557056702c8cfc34e5cfcd90eb83af0347575fd2ad".HexToByteArray(),
                true);
            var key2 = new EthECKey(
                "6a3d6396903245bba5837752b9e0348874e72db0c4e11e9c485a81b4ea4353b9".HexToByteArray(),
                true);

            var secret = key1.CalculateCommonSecret(key2);

            Assert.Equal(
                "167ccc13ac5e8a26b131c3446030c60fbfac6aa8e31149d0869f93626a4cdf62",
                secret.ToHex());
        }
    }
}
