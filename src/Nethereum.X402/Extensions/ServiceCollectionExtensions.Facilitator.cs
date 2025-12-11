using Microsoft.Extensions.DependencyInjection;

namespace Nethereum.X402.Extensions;

public static class ServiceCollectionExtensionsFacilitator
{
    public static IMvcBuilder AddX402FacilitatorControllers(
        this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(typeof(Facilitator.FacilitatorController).Assembly);
    }
}
