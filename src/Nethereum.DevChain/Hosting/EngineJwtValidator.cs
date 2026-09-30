using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nethereum.DevChain.Hosting
{
    public sealed class EngineJwtValidator
    {
        private const string BearerPrefix = "Bearer ";
        private const long AllowedClockSkewSeconds = 60;

        private readonly byte[] _secret;
        private readonly Func<DateTimeOffset> _clock;

        public EngineJwtValidator(byte[] secret, Func<DateTimeOffset> clock = null)
        {
            _secret = secret ?? throw new ArgumentNullException(nameof(secret));
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        public bool TryValidate(string authorizationHeaderValue, out string error)
        {
            if (string.IsNullOrWhiteSpace(authorizationHeaderValue))
            {
                error = "Missing Authorization header";
                return false;
            }

            if (!authorizationHeaderValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "Authorization header must use the Bearer scheme";
                return false;
            }

            var token = authorizationHeaderValue.Substring(BearerPrefix.Length).Trim();
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                error = "Malformed JWT";
                return false;
            }

            byte[] headerBytes;
            byte[] payloadBytes;
            byte[] signatureBytes;
            try
            {
                headerBytes = Base64UrlDecode(parts[0]);
                payloadBytes = Base64UrlDecode(parts[1]);
                signatureBytes = Base64UrlDecode(parts[2]);
            }
            catch (FormatException)
            {
                error = "Malformed JWT";
                return false;
            }

            if (!TryReadAlg(headerBytes, out var alg))
            {
                error = "Malformed JWT header";
                return false;
            }

            if (!string.Equals(alg, "HS256", StringComparison.Ordinal))
            {
                error = "Unsupported JWT alg";
                return false;
            }

            var signingInput = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
            using var hmac = new HMACSHA256(_secret);
            var expectedSignature = hmac.ComputeHash(signingInput);

            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, signatureBytes))
            {
                error = "Invalid JWT signature";
                return false;
            }

            if (!TryReadIat(payloadBytes, out var iat))
            {
                error = "Missing iat claim";
                return false;
            }

            var now = _clock().ToUnixTimeSeconds();
            if (Math.Abs(now - iat) > AllowedClockSkewSeconds)
            {
                error = "iat claim outside the +-60s window";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryReadAlg(byte[] headerBytes, out string alg)
        {
            alg = null;
            try
            {
                using var document = JsonDocument.Parse(headerBytes);
                if (!document.RootElement.TryGetProperty("alg", out var algElement))
                    return false;

                alg = algElement.GetString();
                return alg != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryReadIat(byte[] payloadBytes, out long iat)
        {
            iat = 0;
            try
            {
                using var document = JsonDocument.Parse(payloadBytes);
                if (!document.RootElement.TryGetProperty("iat", out var iatElement))
                    return false;

                return iatElement.TryGetInt64(out iat);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static byte[] Base64UrlDecode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            return Convert.FromBase64String(padded);
        }
    }
}
