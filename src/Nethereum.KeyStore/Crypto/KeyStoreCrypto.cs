using System;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Macs;

namespace Nethereum.KeyStore.Crypto
{
    //https://github.com/ethereum/wiki/wiki/Web3-Secret-Storage-Definition
    public class KeyStoreCrypto
    {
        public byte[] GenerateDerivedScryptKey(byte[] password, byte[] salt, int n, int r, int p, int dkLen)
        {
            return SCrypt.Generate(password, salt, n, r, p, dkLen);
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
            byte[] result = new byte[hmac.GetMacSize()];
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
            var pdb = new Pkcs5S2ParametersGenerator(new Sha256Digest());

            //note Pkcs5PasswordToUtf8Bytes is the same as Encoding.UTF8.GetBytes(password)
            //changing it to keep it as bouncy

            pdb.Init(PbeParametersGenerator.Pkcs5PasswordToUtf8Bytes(password.ToCharArray()), salt,
                     count);
            //if dklen == 32, then it is 256 (8 * 32)
            var key = (KeyParameter)pdb.GenerateDerivedMacParameters(8 * dklen);
            return key.GetKey();
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


        public byte[] DecryptScrypt(string password, byte[] mac, byte[] iv, byte[] cipherText, int n, int p, int r, byte[] salt, int dklen)
        {
            var derivedKey = GenerateDerivedScryptKey(GetPasswordAsBytes(password), salt, n, r, p, dklen);
            return Decrypt(mac, iv, cipherText, derivedKey);
        }

        public byte[] DecryptPbkdf2Sha256(string password, byte[] mac, byte[] iv, byte[] cipherText, int c, byte[] salt, int dklen)
        {
            var derivedKey = GeneratePbkdf2Sha256DerivedKey(password, salt, c, dklen);
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
            if (generatedMac.ToHex() != mac.ToHex())
                throw new DecryptionException("Cannot derive the same mac as the one provided from the cipher and derived key");
        }

        public byte[] GetPasswordAsBytes(string password)
        {
            return Encoding.UTF8.GetBytes(password);
        }
    }
  
}
