using System;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Nethereum.WebAuthn.Windows
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNethereumWebAuthnWindows(
            this IServiceCollection services,
            bool requireUserVerificationForAssertion = true,
            bool fallbackToSoftwareOffWindows = true)
        {
            if (OperatingSystem.IsWindows())
            {
                AddWindowsAuthenticator(services, requireUserVerificationForAssertion);
            }
            else if (fallbackToSoftwareOffWindows)
            {
                AddSoftwareFallback(services, requireUserVerificationForAssertion);
            }
            else
            {
                services.AddSingleton<IWebAuthnAuthenticator>(_ => throw UnsupportedPlatform());
                services.AddSingleton<IWebAuthnCredentialFactory>(_ => throw UnsupportedPlatform());
            }

            return services;
        }

        [SupportedOSPlatform("windows")]
        private static void AddWindowsAuthenticator(IServiceCollection services, bool requireUserVerificationForAssertion)
        {
            services.AddSingleton(_ => new WindowsWebAuthnAuthenticator(
                new ForegroundWindowHandleProvider(), requireUserVerificationForAssertion));
            services.AddSingleton<IWebAuthnAuthenticator>(sp => sp.GetRequiredService<WindowsWebAuthnAuthenticator>());
            services.AddSingleton<IWebAuthnCredentialFactory>(sp => sp.GetRequiredService<WindowsWebAuthnAuthenticator>());
        }

        private static void AddSoftwareFallback(IServiceCollection services, bool requireUserVerificationForAssertion)
        {
            services.AddSingleton(_ => new SoftwareWebAuthnAuthenticator(requireUV: requireUserVerificationForAssertion));
            services.AddSingleton<IWebAuthnAuthenticator>(sp => sp.GetRequiredService<SoftwareWebAuthnAuthenticator>());
            services.AddSingleton<IWebAuthnCredentialFactory>(sp => sp.GetRequiredService<SoftwareWebAuthnAuthenticator>());
        }

        private static PlatformNotSupportedException UnsupportedPlatform() => new PlatformNotSupportedException(
            "Nethereum.WebAuthn.Windows requires Windows 10 1809+ (webauthn.dll). Pass " +
            "fallbackToSoftwareOffWindows: true to AddNethereumWebAuthnWindows for a non-Windows dev fallback.");
    }
}
