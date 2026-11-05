using Nethereum.Documentation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Nethereum.X402.Facilitator;

namespace Nethereum.X402.Extensions;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "ServiceCollectionExtensionsFacilitator - hosted facilitator registrations")]
public static class ServiceCollectionExtensionsFacilitator
{
    public static IMvcBuilder AddX402FacilitatorControllers(
        this IMvcBuilder builder)
    {
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<MvcOptions>, FacilitatorRoutePrefixSetup>());
        return builder.AddApplicationPart(typeof(Facilitator.FacilitatorController).Assembly);
    }

    public static IServiceCollection AddX402FacilitatorOptions(
        this IServiceCollection services,
        Action<X402FacilitatorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        var options = new X402FacilitatorOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);
        return services;
    }
}
