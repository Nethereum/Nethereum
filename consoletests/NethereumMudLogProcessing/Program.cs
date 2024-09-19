using Microsoft.Extensions.DependencyInjection;
using Nethereum.Mud.Contracts.Store;
using Nethereum.Mud.Contracts.World;
using Nethereum.Web3;
using System.Numerics;

using Nethereum.Mud;
using Nethereum.Mud.Contracts.Core.Tables;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using System.Diagnostics;
using Nethereum.Mud.Repositories.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace NethereumMudLogProcessing
{
    //To: setup the database use dotnet ef migrations add InitialCreate and dotnet ef database update
    //dotnet ef migrations add InitialCreate
    //dotnet ef database update 
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
            ILogger logger = factory.CreateLogger("Program");

            var configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
               .Build();


            var serviceProvider = new ServiceCollection()
                .AddDbContext<MudPostgresStoreRecordsDbContext>(options =>
                    options.UseNpgsql(configuration.GetConnectionString("PostgresConnection")).UseLowerCaseNamingConvention()
                    )
                .AddSingleton<ILogger>(logger)
                .AddTransient<MudPostgresStoreRecordsProcessingService>()
                .BuildServiceProvider();


            //Blockchain processing
            using (var scope = serviceProvider.CreateScope())
            {
                var services = scope.ServiceProvider;
                var myService = services.GetRequiredService<MudPostgresStoreRecordsProcessingService>();
                myService.Address = "0xa3372F8dd68F9d9309bf9Ac95a88A27b3998ed4e";
                myService.RpcUrl = "https://localhost:8045";
                myService.StartAtBlockNumberIfNotProcessed = 1;

                await myService.ExecuteAsync();
            }

        }

    }
}
