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
using Microsoft.EntityFrameworkCore.Design;

namespace MudStoredRecordsRestApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            var configuration = builder.Configuration;
            // Add services to the container.
            builder.Services.AddDbContext<MudPostgresStoreRecordsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PostgresConnection"))
               .UseLowerCaseNamingConvention());


            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
