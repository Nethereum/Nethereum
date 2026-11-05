using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.Documentation;
using Nethereum.EngineApi.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class EngineApiHostingReadmeExampleTests
    {
        public class NoOpEngineService : DispatchProxy
        {
            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
        }

        private static string Token(byte[] secret, long iat)
        {
            static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var header = B64(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            var payload = B64(Encoding.UTF8.GetBytes("{\"iat\":" + iat + "}"));
            using var hmac = new HMACSHA256(secret);
            return header + "." + payload + "." + B64(hmac.ComputeHash(Encoding.ASCII.GetBytes(header + "." + payload)));
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "engine-api-hosting", "Register the JWT-authenticated Engine API server", Order = 1)]
        public void Given_AnEngineServiceAndAJwtPath_When_AddEngineApiServerRuns_Then_TheSecretFileIsCreatedAndTheValidatorAcceptsOnlyFreshTokensSignedWithIt()
        {
            var jwtPath = Path.Combine(Path.GetTempPath(), $"engine-readme-{Guid.NewGuid():N}.hex");
            try
            {
                var engineApiService = DispatchProxy.Create<IEngineApiService, NoOpEngineService>();
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddSingleton(sp => new RpcContext(() => null!, BigInteger.One, sp));

                services.AddEngineApiServer(
                    new EngineApiServerConfig { Port = 8551, JwtSecretPath = jwtPath },
                    provider => engineApiService);

                using var provider = services.BuildServiceProvider();
                var host = provider.GetRequiredService<EngineApiHost>();
                var secretHex = File.ReadAllText(jwtPath).Trim();
                var secret = Convert.FromHexString(secretHex);
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                Assert.Same(engineApiService, provider.GetRequiredService<IEngineApiService>());
                Assert.Equal(EngineJwtSecret.SecretLengthBytes, secret.Length);
                Assert.True(host.Validator.TryValidate("Bearer " + Token(secret, now), out _));
                Assert.False(host.Validator.TryValidate("Bearer " + Token(secret, now - 61), out _));
                Assert.False(host.Validator.TryValidate("Bearer " + Token(new byte[32], now), out _));
            }
            finally
            {
                if (File.Exists(jwtPath)) File.Delete(jwtPath);
            }
        }

        [Fact]
        public void Given_ASecretFileWrittenByAConsensusClient_When_TheSecretIsLoaded_Then_TheZeroXPrefixIsAccepted()
        {
            var jwtPath = Path.Combine(Path.GetTempPath(), $"engine-readme-{Guid.NewGuid():N}.hex");
            try
            {
                var expected = RandomNumberGenerator.GetBytes(32);
                File.WriteAllText(jwtPath, "0x" + Convert.ToHexString(expected).ToLowerInvariant() + "\n");

                Assert.Equal(expected, EngineJwtSecret.LoadOrCreate(jwtPath));
            }
            finally
            {
                if (File.Exists(jwtPath)) File.Delete(jwtPath);
            }
        }
    }
}
