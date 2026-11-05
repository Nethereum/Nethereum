using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Nethereum.EngineApi.Hosting;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.SecurityAudit71.S5.Round1
{
    public class S5R1EngineJwtAndSecretTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

        private static string B64(string text) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string TempPath() => Path.Combine(Path.GetTempPath(), "s5r1-" + Guid.NewGuid().ToString("N") + ".hex");

        [Theory]
        [InlineData("[]")]
        [InlineData("1")]
        [InlineData("\"HS256\"")]
        [InlineData("{\"alg\":1}")]
        [InlineData("{\"alg\":{}}")]
        public void Given_a_token_whose_header_is_valid_json_of_the_wrong_shape_When_validated_Then_it_is_refused_without_throwing(string headerJson)
        {
            var validator = new EngineJwtValidator(RandomNumberGenerator.GetBytes(32), () => Now);
            var token = B64(headerJson) + "." + B64("{\"iat\":" + Now.ToUnixTimeSeconds() + "}") + "." + B64("sig");

            var accepted = validator.TryValidate("Bearer " + token, out var error);

            Assert.False(accepted);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("\"now\"")]
        [InlineData("{\"iat\":\"1\"}")]
        [InlineData("{\"iat\":[1]}")]
        [InlineData("{\"iat\":{}}")]
        public void Given_a_correctly_signed_token_whose_claims_are_valid_json_of_the_wrong_shape_When_validated_Then_it_is_refused_without_throwing(string claimsJson)
        {
            var secret = RandomNumberGenerator.GetBytes(32);
            var validator = new EngineJwtValidator(secret, () => Now);
            var header = B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
            var claims = B64(claimsJson);
            using var hmac = new HMACSHA256(secret);
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(header + "." + claims))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var accepted = validator.TryValidate("Bearer " + header + "." + claims + "." + signature, out var error);

            Assert.False(accepted);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void Given_an_empty_secret_file_When_the_secret_is_loaded_Then_it_is_refused()
        {
            var path = TempPath();
            File.WriteAllText(path, "");
            try
            {
                Assert.ThrowsAny<Exception>(() => EngineJwtSecret.LoadOrCreate(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Given_an_empty_secret_file_When_a_token_signed_with_the_empty_key_is_presented_Then_it_is_not_accepted()
        {
            var path = TempPath();
            File.WriteAllText(path, "");
            try
            {
                byte[] secret;
                try { secret = EngineJwtSecret.LoadOrCreate(path); } catch (Exception) { return; }

                var validator = new EngineJwtValidator(secret, () => Now);
                var header = B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
                var payload = B64("{\"iat\":" + Now.ToUnixTimeSeconds() + "}");
                using var hmac = new HMACSHA256(new byte[0]);
                var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(header + "." + payload)))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');

                Assert.False(validator.TryValidate("Bearer " + header + "." + payload + "." + sig, out _));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Given_a_sixteen_byte_secret_file_When_the_secret_is_loaded_Then_it_is_refused()
        {
            var path = TempPath();
            File.WriteAllText(path, new string('a', 32));
            try
            {
                Assert.ThrowsAny<Exception>(() => EngineJwtSecret.LoadOrCreate(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Given_a_0x_prefixed_thirty_two_byte_secret_file_When_the_secret_is_loaded_Then_the_consensus_client_format_is_accepted()
        {
            var path = TempPath();
            File.WriteAllText(path, "0x" + new string('a', 64));
            try
            {
                var secret = EngineJwtSecret.LoadOrCreate(path);
                Assert.Equal(32, secret.Length);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Given_a_secret_file_created_by_the_node_When_its_mode_is_read_Then_only_the_owner_can_read_or_write_it()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            var path = TempPath();
            try
            {
                EngineJwtSecret.LoadOrCreate(path);
                var mode = File.GetUnixFileMode(path);
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
