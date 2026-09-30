using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.WebAuthn;
using Nethereum.WebAuthn.Windows;
using Xunit;

namespace Nethereum.WebAuthn.Windows.UnitTests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public async Task AddNethereumWebAuthnWindows_registers_a_working_authenticator_for_the_current_platform()
        {
            var services = new ServiceCollection();
            services.AddNethereumWebAuthnWindows(requireUserVerificationForAssertion: false);
            using var provider = services.BuildServiceProvider();

            var authenticator = provider.GetRequiredService<IWebAuthnAuthenticator>();
            var factory = provider.GetRequiredService<IWebAuthnCredentialFactory>();

            if (OperatingSystem.IsWindows())
            {
                Assert.IsType<WindowsWebAuthnAuthenticator>(authenticator);
                Assert.IsType<WindowsWebAuthnAuthenticator>(factory);
                return;
            }

            Assert.IsType<SoftwareWebAuthnAuthenticator>(authenticator);
            Assert.IsType<SoftwareWebAuthnAuthenticator>(factory);

            var created = await factory.CreateCredentialAsync(new WebAuthnCredentialCreationOptions { RpId = "nethereum.local" });
            var assertion = await authenticator.GetAssertionAsync(
                SHA256.HashData(Encoding.UTF8.GetBytes("userOpHash")), created.PlatformCredentialId, "nethereum.local");

            Assert.Equal(created.PlatformCredentialId, assertion.CredentialId);
            Assert.Equal(32, assertion.R.Length);
            Assert.Equal(32, assertion.S.Length);
        }

        [Fact]
        public void AddNethereumWebAuthnWindows_throws_on_resolve_off_Windows_when_the_fallback_is_disabled()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            var services = new ServiceCollection();
            services.AddNethereumWebAuthnWindows(fallbackToSoftwareOffWindows: false);
            using var provider = services.BuildServiceProvider();

            Assert.Throws<PlatformNotSupportedException>(() => provider.GetRequiredService<IWebAuthnAuthenticator>());
            Assert.Throws<PlatformNotSupportedException>(() => provider.GetRequiredService<IWebAuthnCredentialFactory>());
        }
    }
}
