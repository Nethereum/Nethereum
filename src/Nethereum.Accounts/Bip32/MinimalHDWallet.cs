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

namespace Nethereum.Accounts.Bip32
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.CoreFoundation, "account-types", "MinimalHDWallet")]
    public class MinimalHDWallet
    {
        public byte[] Seed { get; }
        public const string MasterKeyHmacKey = "Bitcoin seed";
        public const int MaxDepth = 255;

        public MinimalHDWallet(string mnemonic, string password = "") : this(mnemonic, password, true)
        {
        }

        public MinimalHDWallet(string mnemonic, string password, bool validateMnemonic)
        {
            Seed = Bip39.MnemonicToSeed(mnemonic, password, validateMnemonic);
        }

        public MinimalHDWallet(byte[] seed)
        {
            if (seed == null || seed.Length != 64)
                throw new ArgumentException("Seed must be a 64-byte array");
            Seed = seed;
        }

        public EthECKey GetKeyFromPath(string derivationPath)
        {
            if (derivationPath == null) throw new ArgumentNullException(nameof(derivationPath));
            var segments = derivationPath.Split('/');
            if (segments[0] != "m")
                throw new ArgumentException("Derivation path must start with 'm'");
            if (segments.Length - 1 > MaxDepth)
                throw new ArgumentException("Derivation path is deeper than " + MaxDepth);

            byte[] key, chainCode;
            Span<byte> data = stackalloc byte[37];

            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(MasterKeyHmacKey)))
            {
                var i = hmac.ComputeHash(Seed);
                key = i[..32];
                chainCode = i[32..];
            }


            for (int i = 1; i < segments.Length; i++)
            {
                var segment = segments[i];
                var index = ParseSegment(segment, out bool hardened);

                if (hardened)
                    index += 0x80000000;

                var indexBytes = BitConverter.GetBytes(index);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(indexBytes);



                if (hardened)
                {
                    data[0] = 0x00;
                    key.CopyTo(data[1..33]);
                }
                else
                {
                    var pubKey = new EthECKey(key, true).GetPubKey(true);

                    pubKey.CopyTo(data[0..33]);
                }

                indexBytes.CopyTo(data[33..]);

                using var hmac = new HMACSHA512(chainCode);
                var iBytes = hmac.ComputeHash(data.ToArray());

                var IL = iBytes[..32];
                var IR = iBytes[32..];



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

            }

            return new EthECKey(key, true);
        }

        private static uint ParseSegment(string segment, out bool hardened)
        {
            hardened = segment.Length > 0 && segment[segment.Length - 1] == '\'';
            var digits = hardened ? segment.Substring(0, segment.Length - 1) : segment;
            if (digits.Length == 0 || digits.Length > 10 || (digits.Length > 1 && digits[0] == '0'))
                throw new ArgumentException("Invalid path segment: " + segment);
            foreach (var c in digits)
                if (c < '0' || c > '9')
                    throw new ArgumentException("Invalid path segment: " + segment);
            if (!uint.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)
                || index >= 0x80000000)
                throw new ArgumentException("Invalid path segment: " + segment);
            return index;
        }

        public EthECKey GetEthereumKey(int accountIndex) => GetKeyFromPath($"m/44'/60'/0'/0/{accountIndex}");
        public string GetEthereumAddress(int index) => GetEthereumKey(index).GetPublicAddress();
    }
}
