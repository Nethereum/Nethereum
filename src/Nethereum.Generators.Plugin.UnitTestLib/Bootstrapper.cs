using Microsoft.Extensions.DependencyInjection;
using Nethereum.Generators.Desktop.Core;
using Nethereum.Generators.Plugin.UnitTestLib.Tabs;

namespace Nethereum.Generators.Plugin.UnitTestLib
{
    public static class Bootstrapper
    {
        public static void RegisterServices(IServiceCollection services)
        {
            
            services.AddSingleton<TabPageNethereumUnitTest>();
            services.AddScoped<IMainTab, TabPageMainNethereumUnitTest>();
        }
    }
}
