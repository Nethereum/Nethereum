using Microsoft.Extensions.DependencyInjection.Extensions;
using Nethereum.Reown.AppKit.Blazor;
using Nethereum.UI;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions {
	public static IServiceCollection AddAppKit(this IServiceCollection services, AppKitConfiguration configuration) {
		services.TryAddSingleton(configuration);
		services.TryAddSingleton<AppKitHostProvider>();
		services.TryAddSingleton<IEthereumHostProvider>(services => services.GetRequiredService<AppKitHostProvider>());
		services.TryAddSingleton<IAppKit>(services => services.GetRequiredService<AppKitHostProvider>());

		return services;
	}
}
