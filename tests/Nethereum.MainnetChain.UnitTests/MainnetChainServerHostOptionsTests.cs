using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetChainServerHostOptionsTests
    {
        [Fact]
        public void Given_AddMainnetChainServerOnEitherBuilder_When_HostOptionsAreResolved_Then_TheShutdownTimeoutIsTheFrameworkDefault()
        {
            var config = new MainnetChainServerConfig { DataDir = null };
            var frameworkDefault = new HostOptions().ShutdownTimeout;

            var genericBuilder = Host.CreateApplicationBuilder();
            genericBuilder.Services.AddMainnetChainServer(config);
            using var genericHost = genericBuilder.Build();

            var webBuilder = WebApplication.CreateBuilder();
            webBuilder.Services.AddMainnetChainServer(config);
            using var webHost = webBuilder.Build();

            Assert.Equal(frameworkDefault, genericHost.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HostOptions>>().Value.ShutdownTimeout);
            Assert.Equal(frameworkDefault, webHost.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HostOptions>>().Value.ShutdownTimeout);
        }
    }
}
