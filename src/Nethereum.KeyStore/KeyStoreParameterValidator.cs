using System;
using Nethereum.KeyStore.Model;

namespace Nethereum.KeyStore
{
    internal static class KeyStoreParameterValidator
    {
        private const int DerivedKeyLength = 32;
        private const int IvLength = 16;
        private const int MacLength = 32;
        public const string CipherName = "aes-128-ctr";
        public const string Pbkdf2Prf = "hmac-sha256";

        public static void ValidateScrypt(ScryptParams parameters, KeyStoreKdfLimits limits)
        {
            if (parameters == null) throw Invalid("kdfparams are missing");
            ValidateScryptCost(parameters.N, parameters.R, parameters.P, parameters.Dklen, limits);
            RequireHex(parameters.Salt, "salt", 1, int.MaxValue);
        }

        public static void ValidateScryptCost(int nValue, int rValue, int pValue, int dklen, KeyStoreKdfLimits limits)
        {
            if (limits == null) limits = KeyStoreKdfLimits.Default;
            long n = nValue;
            long r = rValue;
            long p = pValue;
            if (n <= 1 || (n & (n - 1)) != 0) throw Invalid("scrypt n must be a power of two greater than 1");
            if (r < 1) throw Invalid("scrypt r must be at least 1");
            if (p < 1) throw Invalid("scrypt p must be at least 1");
            if (r * p >= (1L << 30)) throw Invalid("scrypt r * p must be below 2^30");
            if (p > limits.MaxScryptP) throw Invalid("scrypt p exceeds the configured limit");
            if (dklen != DerivedKeyLength) throw Invalid("dklen must be 32");
            var memory = 128L * r * n;
            if (memory > limits.MaxScryptMemoryBytes) throw Invalid("scrypt memory cost exceeds the configured limit");
            if (128L * r * p > limits.MaxScryptMemoryBytes) throw Invalid("scrypt block cost exceeds the configured limit");
        }

        public static void ValidatePbkdf2(Pbkdf2Params parameters, KeyStoreKdfLimits limits)
        {
            if (parameters == null) throw Invalid("kdfparams are missing");
            ValidatePbkdf2Cost(parameters.Count, parameters.Dklen, limits);
            if (parameters.Prf != Pbkdf2Prf) throw Invalid("pbkdf2 prf must be hmac-sha256");
            RequireHex(parameters.Salt, "salt", 1, int.MaxValue);
        }

        public static void ValidateVersion(int version)
        {
            if (version != 3) throw Invalid("keystore version must be 3");
        }

        public static T Parse<T>(Func<T> parse) where T : class
        {
            T result;
            try
            {
                result = parse();
            }
            catch (Exception)
            {
                throw Invalid("json is not a valid keystore");
            }
            if (result == null) throw Invalid("json is not a valid keystore");
            return result;
        }

        public static void ValidatePbkdf2Cost(int count, int dklen, KeyStoreKdfLimits limits)
        {
            if (limits == null) limits = KeyStoreKdfLimits.Default;
            if (count < 1) throw Invalid("pbkdf2 c must be at least 1");
            if (count > limits.MaxPbkdf2Iterations) throw Invalid("pbkdf2 c exceeds the configured limit");
            if (dklen != DerivedKeyLength) throw Invalid("dklen must be 32");
        }

        public static void ValidateEnvelope<T>(CryptoInfo<T> crypto, string expectedKdf) where T : KdfParams
        {
            if (crypto == null) throw Invalid("crypto is missing");
            if (crypto.Cipher != CipherName) throw Invalid("cipher must be aes-128-ctr");
            if (crypto.Kdf != expectedKdf) throw Invalid("kdf does not match the kdfparams");
            if (crypto.CipherParams == null) throw Invalid("cipherparams are missing");
            RequireHex(crypto.CipherParams.Iv, "iv", IvLength, IvLength);
            RequireHex(crypto.Mac, "mac", 0, int.MaxValue);
            RequireHex(crypto.CipherText, "ciphertext", 0, int.MaxValue);
        }

        public static byte[] RequireHex(string hex, string field, int minBytes, int maxBytes)
        {
            if (hex == null) throw Invalid(field + " is missing");
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
            if (hex.Length % 2 != 0) throw Invalid(field + " is not valid hex");
            var length = hex.Length / 2;
            if (length < minBytes || length > maxBytes) throw Invalid(field + " has an invalid length");
            var bytes = new byte[length];
            for (var i = 0; i < length; i++)
            {
                var high = Nibble(hex[2 * i]);
                var low = Nibble(hex[2 * i + 1]);
                if (high < 0 || low < 0) throw Invalid(field + " is not valid hex");
                bytes[i] = (byte)((high << 4) | low);
            }
            return bytes;
        }

        private static int Nibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        public static ArgumentException Invalid(string message)
        {
            return new ArgumentException("Invalid keystore: " + message);
        }
    }
}
