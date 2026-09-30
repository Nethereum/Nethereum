using System;
using System.Security.Cryptography;
using Nethereum.Util;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Digests;

namespace Nethereum.Signer.Crypto
{
    public static class EciesEncryption
    {
        private const int KeyLen = 16;
        private const int PubKeyLenWithPrefix = 65;
        private const int IvLen = 16;
        private const int MacLen = 32;
        public const int Overhead = PubKeyLenWithPrefix + IvLen + MacLen;

        public static byte[] Encrypt(byte[] recipientPubKeyNoPrefix, byte[] plaintext, byte[] authData)
        {
            var ephemeral = EthECKey.GenerateKey();
            var ephPubBytes = ephemeral.GetPubKey();

            var recipient = new EthECKey(recipientPubKeyNoPrefix, false, EthECKey.DEFAULT_PREFIX);
            var sharedSecret = ephemeral.CalculateCommonSecret(recipient);

            var keyMaterial = ConcatKdf(sharedSecret, KeyLen * 2);
            var encKey = keyMaterial.Slice(0, KeyLen);
            var macKeyInput = keyMaterial.Slice(KeyLen, KeyLen * 2);
            var macKey = Sha256(macKeyInput);

            var iv = new byte[IvLen];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(iv);

            var ciphertext = AesCtr(encKey, iv, plaintext);
            var mac = ComputeHmac(macKey, iv, ciphertext, authData);

            return ByteUtil.Merge(ephPubBytes, iv, ciphertext, mac);
        }

        public static byte[] Decrypt(byte[] privateKey, byte[] data, byte[] authData)
        {
            if (data.Length < Overhead)
                throw new CryptographicException("ECIES ciphertext too short");

            var ephPubBytes = data.Slice(0, PubKeyLenWithPrefix);
            var iv = data.Slice(PubKeyLenWithPrefix, PubKeyLenWithPrefix + IvLen);
            var ciphertext = data.Slice(PubKeyLenWithPrefix + IvLen, data.Length - MacLen);
            var receivedMac = data.Slice(data.Length - MacLen, data.Length);

            var myKey = new EthECKey(privateKey, true);
            var ephPubNoPrefix = ephPubBytes.Slice(1, PubKeyLenWithPrefix);
            var ephKey = new EthECKey(ephPubNoPrefix, false, EthECKey.DEFAULT_PREFIX);
            var sharedSecret = myKey.CalculateCommonSecret(ephKey);

            var keyMaterial = ConcatKdf(sharedSecret, KeyLen * 2);
            var encKey = keyMaterial.Slice(0, KeyLen);
            var macKeyInput = keyMaterial.Slice(KeyLen, KeyLen * 2);
            var macKey = Sha256(macKeyInput);

            var computedMac = ComputeHmac(macKey, iv, ciphertext, authData);
            if (!ByteUtil.ConstantTimeEquals(computedMac, receivedMac))
                throw new CryptographicException("ECIES MAC verification failed");

            return AesCtr(encKey, iv, ciphertext);
        }

        internal static byte[] ConcatKdf(byte[] sharedSecret, int outputLen)
        {
            var hash = new Sha256Digest();
            var hashLen = hash.GetDigestSize();
            var reps = (outputLen + hashLen - 1) / hashLen;
            var result = new byte[reps * hashLen];

            for (int counter = 1; counter <= reps; counter++)
            {
                var counterBytes = new byte[4];
                counterBytes[0] = (byte)(counter >> 24);
                counterBytes[1] = (byte)(counter >> 16);
                counterBytes[2] = (byte)(counter >> 8);
                counterBytes[3] = (byte)(counter);

                hash.Reset();
                hash.BlockUpdate(counterBytes, 0, 4);
                if (sharedSecret.Length > 0)
                    hash.BlockUpdate(sharedSecret, 0, sharedSecret.Length);

                hash.DoFinal(result, (counter - 1) * hashLen);
            }

            return result.Slice(0, outputLen);
        }

        internal static byte[] AesCtr(byte[] key, byte[] iv, byte[] data)
        {
            var cipher = new SicBlockCipher(new AesEngine());
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));

            var output = new byte[data.Length];
            var blockSize = cipher.GetBlockSize();
            var offset = 0;

            while (offset + blockSize <= data.Length)
            {
                cipher.ProcessBlock(data, offset, output, offset);
                offset += blockSize;
            }

            if (offset < data.Length)
            {
                var lastBlock = new byte[blockSize];
                var outBlock = new byte[blockSize];
                Buffer.BlockCopy(data, offset, lastBlock, 0, data.Length - offset);
                cipher.ProcessBlock(lastBlock, 0, outBlock, 0);
                Buffer.BlockCopy(outBlock, 0, output, offset, data.Length - offset);
            }

            return output;
        }

        private static byte[] ComputeHmac(byte[] key, byte[] iv, byte[] ciphertext, byte[] authData)
        {
            var hmac = new HMac(new Sha256Digest());
            hmac.Init(new KeyParameter(key));
            hmac.BlockUpdate(iv, 0, iv.Length);
            hmac.BlockUpdate(ciphertext, 0, ciphertext.Length);
            if (authData != null && authData.Length > 0)
                hmac.BlockUpdate(authData, 0, authData.Length);
            var mac = new byte[hmac.GetMacSize()];
            hmac.DoFinal(mac, 0);
            return mac;
        }

        private static byte[] Sha256(byte[] data)
        {
            var digest = new Sha256Digest();
            digest.BlockUpdate(data, 0, data.Length);
            var hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);
            return hash;
        }

    }
}
