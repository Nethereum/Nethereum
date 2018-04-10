using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Generators.Desktop.Core.Contract;
using Nethereum.Generators.Desktop.Core.ContractLibrary;

namespace Nethereum.Generators.Desktop.Core
{
    public static class Bootstrapper
    {
        public static void RegisterServices(IServiceCollection services)
        {
            
            services.AddSingleton<ContractViewModel>();
            services.AddSingleton<ContractLibraryViewModel>();
            services.AddSingleton<ContractLibraryClassGeneratorCommand>();
            services.AddSingleton<NetstandardLibraryGeneratorCommand>();

            services.AddSingleton<ContractPanel>();
            services.AddSingleton<ContractLibraryPanel>();
            services.AddSingleton<TabPageNethereumLibrary>();
            services.AddScoped<IMainTab,TabPageMainNethereumLibrary>();
        }
    }
}
