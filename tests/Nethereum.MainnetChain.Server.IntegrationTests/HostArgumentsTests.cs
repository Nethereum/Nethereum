using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Nethereum.CoreChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class HostArgumentsTests
    {
        private static IEnumerable<FileConfigurationSource> FileSources(WebApplicationBuilder builder) =>
            ((IConfigurationBuilder)builder.Configuration).Sources.OfType<FileConfigurationSource>();

        [Fact]
        public void Given_default_host_arguments_When_the_web_host_builder_is_created_Then_its_json_files_watch_for_changes()
        {
            var builder = WebApplication.CreateBuilder(Array.Empty<string>());

            Assert.Contains(FileSources(builder), source => source.ReloadOnChange);
        }

        [Fact]
        public void Given_the_arguments_without_config_reload_When_the_web_host_builder_is_created_Then_no_file_source_watches_for_changes()
        {
            var builder = WebApplication.CreateBuilder(HostArguments.WithoutConfigReload(new[] { "--urls=http://127.0.0.1:0" }));
            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            Assert.NotEmpty(FileSources(builder));
            Assert.DoesNotContain(FileSources(builder), source => source.ReloadOnChange);
        }
    }
}
