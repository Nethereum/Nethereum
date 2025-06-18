using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Bip32
{
    /// <summary>
    /// Minimal HD Wallet to provide key derivation from a mnemonic
    /// </summary>
    /// Note: To visualise the process check https://entropy.to/hd-wallet (great documentation and visualisation of the BIP32 derivation process)
    public class MinimalHDWallet
    {
        public byte[] Seed { get; }
        public const string MasterKeyHmacKey = "Bitcoin seed";

        public MinimalHDWallet(string mnemonic, string password = "")
        {
            Seed = Bip39.MnemonicToSeed(mnemonic, password);
        }

        public MinimalHDWallet(byte[] seed)
        {
            if (seed == null || seed.Length != 64)
                throw new ArgumentException("Seed must be a 64-byte array");
            Seed = seed;
        }

        public EthECKey GetKeyFromPath(string derivationPath)
        {
            var segments = derivationPath.Split('/');
            if (segments.Length == 0 || segments[0] != "m")
                throw new ArgumentException("Derivation path must start with 'm'");

            byte[] key, chainCode;

            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(MasterKeyHmacKey)))
            {
                var i = hmac.ComputeHash(Seed);
                key = i[..32];
                chainCode = i[32..];
            }

            Debug.WriteLine("Seed: " + Seed.ToHex());
            Debug.WriteLine("HMAC-SHA512 (m): " + key.Concat(chainCode).ToArray().ToHex());
            Debug.WriteLine("Private Key (m): " + key.ToHex());
            Debug.WriteLine("Chain Code (m): " + chainCode.ToHex());

            for (int i = 1; i < segments.Length; i++)
            {
                var segment = segments[i];
                bool hardened = segment.EndsWith("'");

                if (!uint.TryParse(segment.TrimEnd('\''), out uint index))
                    throw new ArgumentException($"Invalid path segment: {segment}");

                if (hardened)
                    index += 0x80000000;

                var indexBytes = BitConverter.GetBytes(index);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(indexBytes);

                Debug.WriteLine($"\nStep: {segment}");
                Debug.WriteLine($"Index: {indexBytes.ToHex()} (hardened: {hardened})");
                Debug.WriteLine($"Parent Key: {key.ToHex()}");
                Debug.WriteLine($"Parent Chain Code: {chainCode.ToHex()}");

                Span<byte> data = stackalloc byte[37];
                if (hardened)
                {
                    data[0] = 0x00;
                    key.CopyTo(data[1..33]);
                }
                else
                {
                    var pubKey = new EthECKey(key, true).GetPubKey(true);
                    Debug.WriteLine("Public Key: " + pubKey.ToHex());
                    Debug.WriteLine("Public Key Length: " + pubKey.Length);
                    pubKey.CopyTo(data[0..33]);
                }

                indexBytes.CopyTo(data[33..]);

                using var hmac = new HMACSHA512(chainCode);
                var iBytes = hmac.ComputeHash(data.ToArray());

                var IL = iBytes[..32];
                var IR = iBytes[32..];

                Debug.WriteLine("HMAC-SHA512: " + iBytes.ToHex());
                Debug.WriteLine("Child Tweak: " + IL.ToHex());
                Debug.WriteLine("Chain Code: " + IR.ToHex());

                var tweakInt = new BigInteger(IL, isUnsigned: true, isBigEndian: true);
                var parentKeyInt = new BigInteger(key, isUnsigned: true, isBigEndian: true);
                var curveOrder = BigInteger.Parse("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141", System.Globalization.NumberStyles.HexNumber);

                var childKeyInt = (tweakInt + parentKeyInt) % curveOrder;
                key = childKeyInt.ToByteArray(isUnsigned: true, isBigEndian: true);

                if (key.Length < 32)
                {
                    var padded = new byte[32];
                    Buffer.BlockCopy(key, 0, padded, 32 - key.Length, key.Length);
                    key = padded;
                }

                chainCode = IR;
                Debug.WriteLine("Child Private Key: " + key.ToHex());
            }

            return new EthECKey(key, true);
        }

        public EthECKey GetEthereumKey(int accountIndex) => GetKeyFromPath($"m/44'/60'/0'/0/{accountIndex}");
        public string GetEthereumAddress(int index) => GetEthereumKey(index).GetPublicAddress();
    }
}
