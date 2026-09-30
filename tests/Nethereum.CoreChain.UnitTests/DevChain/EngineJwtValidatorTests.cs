using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.DevChain.Hosting;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class EngineJwtValidatorTests
    {
        private static readonly byte[] Secret = RandomNumberGenerator.GetBytes(32);
        private static readonly DateTimeOffset FixedNow = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string BuildToken(byte[] signingSecret, string alg, long? iat, bool tamperSignature = false)
        {
            var headerJson = JsonSerializer.Serialize(new { alg, typ = "JWT" });
            var payloadObject = iat.HasValue ? (object)new { iat = iat.Value } : new { };
            var payloadJson = JsonSerializer.Serialize(payloadObject);

            var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

            using var hmac = new HMACSHA256(signingSecret);
            var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(headerB64 + "." + payloadB64));

            if (tamperSignature)
            {
                signature[0] ^= 0xFF;
            }

            var signatureB64 = Base64UrlEncode(signature);

            return headerB64 + "." + payloadB64 + "." + signatureB64;
        }

        private static EngineJwtValidator CreateValidator() => new EngineJwtValidator(Secret, () => FixedNow);

        [Fact]
        public void Given_AValidHs256TokenWithAFreshIat_When_Validated_Then_ItIsAccepted()
        {
            var token = BuildToken(Secret, "HS256", FixedNow.ToUnixTimeSeconds());

            var accepted = CreateValidator().TryValidate("Bearer " + token, out var error);

            Assert.True(accepted);
            Assert.Null(error);
        }

        [Fact]
        public void Given_AnIatFiftyNineSecondsOld_When_Validated_Then_ItIsAcceptedAtTheBoundary()
        {
            var token = BuildToken(Secret, "HS256", FixedNow.ToUnixTimeSeconds() - 59);

            var accepted = CreateValidator().TryValidate("Bearer " + token, out var error);

            Assert.True(accepted);
            Assert.Null(error);
        }

        [Fact]
        public void Given_AnIatSixtyOneSecondsOld_When_Validated_Then_ItIsRejected()
        {
            var token = BuildToken(Secret, "HS256", FixedNow.ToUnixTimeSeconds() - 61);

            var accepted = CreateValidator().TryValidate("Bearer " + token, out var error);

            Assert.False(accepted);
            Assert.NotNull(error);
        }

        [Fact]
        public void Given_AnAlgOfNone_When_Validated_Then_ItIsRejected()
        {
            var token = BuildToken(Secret, "none", FixedNow.ToUnixTimeSeconds());

            var accepted = CreateValidator().TryValidate("Bearer " + token, out var error);

            Assert.False(accepted);
            Assert.NotNull(error);
        }

        [Fact]
        public void Given_AnAlgOfHs512_When_Validated_Then_ItIsRejected()
        {
            var token = BuildToken(Secret, "HS512", FixedNow.ToUnixTimeSeconds());

            var accepted = CreateValidator().TryValidate("Bearer " + token, out var error);

            Assert.False(accepted);
            Assert.NotNull(error);
        }

        [Fact]
        public void Given_ATamperedSignature_When_Validated_Then_ItIsRejected()
        {
            var token = BuildToken(Secret, "HS256", FixedNow.ToUnixTimeSeconds(), tamperSignature: true);

            var accepted = CreateValidator().TryValidate("Bearer " + token, out var error);

            Assert.False(accepted);
            Assert.NotNull(error);
        }

        [Fact]
        public void Given_ATokenSignedWithADifferentSecret_When_Validated_Then_ItIsRejected()
        {
            var otherSecret = RandomNumberGenerator.GetBytes(32);
            var token = BuildToken(otherSecret, "HS256", FixedNow.ToUnixTimeSeconds());

            var accepted = CreateValidator().TryValidate("Bearer " + token, out var error);

            Assert.False(accepted);
            Assert.NotNull(error);
        }

        [Fact]
        public void Given_ATokenWithNoIatClaim_When_Validated_Then_ItIsRejected()
        {
            var token = BuildToken(Secret, "HS256", iat: null);

            var accepted = CreateValidator().TryValidate("Bearer " + token, out var error);

            Assert.False(accepted);
            Assert.NotNull(error);
        }

        [Fact]
        public void Given_NoAuthorizationHeader_When_Validated_Then_ItIsRejected()
        {
            var accepted = CreateValidator().TryValidate(null, out var error);

            Assert.False(accepted);
            Assert.NotNull(error);
        }
    }
}
