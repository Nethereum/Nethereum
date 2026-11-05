using System;
using System.Text;
using System.Threading;
using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Utilities;

namespace Nethereum.KeyStore.Crypto
{
    //https://github.com/ethereum/wiki/wiki/Web3-Secret-Storage-Definition
    public class KeyStoreCrypto
    {
        public byte[] GenerateDerivedScryptKey(byte[] password, byte[] salt, int n, int r, int p, int dkLen, bool checkRandN = false)
        {
            if (checkRandN)
            {
                //The test vectors of Ethereum provides a cost bigger than the Scrypt spec of "N = less than 2 ^ (128 * r / 8)"
                //so we allow to do validation in general for encryption but not for decryption.
                if (r == 1 && n >= 65536)
                {
                    throw new ArgumentException("Cost parameter N must be > 1 and < 65536.");
                }
            }

            //return SCrypt.Generate(password, salt, n, r, p, dkLen);
            //Using Scrypt.Net instead of BC
            return ScryptNet.CryptoScrypt(password, salt, n, r, p, dkLen);
        }

        public byte[] GenerateCipherKey(byte[] derivedKey)
        {
            var cypherKey = new byte[16];
            Array.Copy(derivedKey, cypherKey, 16);
            return cypherKey;
        }

        public byte[] CalculateKeccakHash(byte[] value)
        {
            var digest = new KeccakDigest(256);
            var output = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(value, 0, value.Length);
            digest.DoFinal(output, 0);
            return output;
        }

        public byte[] CalculateSha256Hash(byte[] value)
        {
            var digest = new Sha256Digest();
            var output = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(value, 0, value.Length);
            digest.DoFinal(output, 0);
            return output;
        }

        public byte[] HmacSha256(byte[] data, byte[] key)
        {
            var hmac = new HMac(new Sha256Digest());
            hmac.Init(new KeyParameter(key));
            var result = new byte[hmac.GetMacSize()];
            hmac.BlockUpdate(data, 0, data.Length);
            hmac.DoFinal(result, 0);
            return result;
        }

        public byte[] GenerateMac(byte[] derivedKey, byte[] cipherText)
        {
            var result = new byte[16 + cipherText.Length];
            Array.Copy(derivedKey, 16, result, 0, 16);
            Array.Copy(cipherText, 0, result, 16, cipherText.Length);
            return CalculateKeccakHash(result);
        }

        //http://stackoverflow.com/questions/34950611/how-to-create-a-pbkdf2-sha256-password-hash-in-c-sharp-bouncy-castle//
        public byte[] GeneratePbkdf2Sha256DerivedKey(string password, byte[] salt, int count, int dklen)
        {
            return GeneratePbkdf2Sha256DerivedKey(password, salt, count, dklen, null);
        }

        public byte[] GeneratePbkdf2Sha256DerivedKey(string password, byte[] salt, int count, int dklen, KeyStoreKdfLimits limits)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (salt == null) throw new ArgumentNullException(nameof(salt));
            if (limits == null) limits = KeyStoreKdfLimits.Default;
            if (count < 1 || count > limits.MaxPbkdf2Iterations)
                throw new ArgumentException("pbkdf2 iteration count is outside the allowed range", nameof(count));
            if (dklen < 1) throw new ArgumentException("dklen must be at least 1", nameof(dklen));
#if NET6_0_OR_GREATER
            return System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                PbeParametersGenerator.Pkcs5PasswordToUtf8Bytes(password.ToCharArray()), salt, count,
                System.Security.Cryptography.HashAlgorithmName.SHA256, dklen);
#else
            var pdb = new Pkcs5S2ParametersGenerator(new Sha256Digest());

            //note Pkcs5PasswordToUtf8Bytes is the same as Encoding.UTF8.GetBytes(password)
            //changing it to keep it as bouncy

            pdb.Init(PbeParametersGenerator.Pkcs5PasswordToUtf8Bytes(password.ToCharArray()), salt,
                count);
            //if dklen == 32, then it is 256 (8 * 32)
            var key = (KeyParameter) pdb.GenerateDerivedMacParameters(8 * dklen);
            return key.GetKey();
#endif
        }

        public byte[] GenerateAesCtrCipher(byte[] iv, byte[] encryptKey, byte[] input)
        {
            //ctr https://gist.github.com/hanswolff/8809275 
            var key = ParameterUtilities.CreateKeyParameter("AES", encryptKey);

            var parametersWithIv = new ParametersWithIV(key, iv);

            var cipher = CipherUtilities.GetCipher("AES/CTR/NoPadding");
            cipher.Init(true, parametersWithIv);
            return cipher.DoFinal(input);
        }

        public byte[] GenerateAesCtrDeCipher(byte[] iv, byte[] encryptKey, byte[] input)
        {
            //ctr https://gist.github.com/hanswolff/8809275 
            var key = ParameterUtilities.CreateKeyParameter("AES", encryptKey);
            var parametersWithIv = new ParametersWithIV(key, iv);

            var cipher = CipherUtilities.GetCipher("AES/CTR/NoPadding");

            cipher.Init(false, parametersWithIv);
            return cipher.DoFinal(input);
        }


        public byte[] DecryptScrypt(string password, byte[] mac, byte[] iv, byte[] cipherText, int n, int p, int r,
            byte[] salt, int dklen)
        {
            return DecryptScrypt(password, mac, iv, cipherText, n, p, r, salt, dklen, null);
        }

        public byte[] DecryptScrypt(string password, byte[] mac, byte[] iv, byte[] cipherText, int n, int p, int r,
            byte[] salt, int dklen, KeyStoreKdfLimits limits)
        {
            KeyStoreParameterValidator.ValidateScryptCost(n, r, p, dklen, limits);
            var derivedKey = GenerateDerivedScryptKey(GetPasswordAsBytes(password), salt, n, r, p, dklen, false);
            return Decrypt(mac, iv, cipherText, derivedKey);
        }

        public byte[] DecryptPbkdf2Sha256(string password, byte[] mac, byte[] iv, byte[] cipherText, int c, byte[] salt,
            int dklen)
        {
            return DecryptPbkdf2Sha256(password, mac, iv, cipherText, c, salt, dklen, null);
        }

        public byte[] DecryptPbkdf2Sha256(string password, byte[] mac, byte[] iv, byte[] cipherText, int c, byte[] salt,
            int dklen, KeyStoreKdfLimits limits)
        {
            KeyStoreParameterValidator.ValidatePbkdf2Cost(c, dklen, limits);
            var derivedKey = GeneratePbkdf2Sha256DerivedKey(password, salt, c, dklen, limits);
            return Decrypt(mac, iv, cipherText, derivedKey);
        }

        public byte[] Decrypt(byte[] mac, byte[] iv, byte[] cipherText, byte[] derivedKey)
        {
            ValidateMac(mac, cipherText, derivedKey);
            var encryptKey = new byte[16];
            Array.Copy(derivedKey, encryptKey, 16);
            var privateKey = GenerateAesCtrCipher(iv, encryptKey, cipherText);
            return privateKey;
        }

        private void ValidateMac(byte[] mac, byte[] cipherText, byte[] derivedKey)
        {
            var generatedMac = GenerateMac(derivedKey, cipherText);
            if (mac == null || !FixedTimeEquals(generatedMac, mac))
                throw new DecryptionException(
                    "Cannot derive the same mac as the one provided from the cipher and derived key");
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];
            return difference == 0;
        }

        public byte[] GetPasswordAsBytes(string password)
        {
            return Encoding.UTF8.GetBytes(password);
        }
    }
}