using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Nethereum.WebAuthn.Blazor
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNethereumWebAuthnBlazor(this IServiceCollection services, bool requireUserVerificationForAssertion = true)
        {
            services.AddScoped(sp => new BlazorWebAuthnAuthenticator(sp.GetRequiredService<IJSRuntime>(), requireUserVerificationForAssertion));
            services.AddScoped<IWebAuthnAuthenticator>(sp => sp.GetRequiredService<BlazorWebAuthnAuthenticator>());
            services.AddScoped<IWebAuthnCredentialFactory>(sp => sp.GetRequiredService<BlazorWebAuthnAuthenticator>());
            return services;
        }
    }
}
