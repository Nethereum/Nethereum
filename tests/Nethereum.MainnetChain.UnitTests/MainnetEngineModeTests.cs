using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.CoreChain.Engine;
using Nethereum.Documentation;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.CoreChain.Storage;
using Nethereum.EngineApi.Hosting;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetEngineModeTests
    {
        private sealed class EmptyBlockSource : IBlockSource
        {
            public async System.Collections.Generic.IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock,
                [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken ct)
            {
                await System.Threading.Tasks.Task.CompletedTask;
                yield break;
            }

            public System.Threading.Tasks.Task<BlockSourceHealth> GetHealthAsync(System.Threading.CancellationToken ct)
                => System.Threading.Tasks.Task.FromResult(default(BlockSourceHealth));

            public System.Threading.Tasks.Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, System.Threading.CancellationToken ct)
                => System.Threading.Tasks.Task.CompletedTask;

            public DivergenceSignal? LastChainBreak => null;
        }

        private static ServiceProvider Build(MainnetChainServerConfig config)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.UseInMemoryBundleAndSource(new EmptyBlockSource());
            services.AddMainnetChainServer(config);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void Given_engine_mode_When_the_light_client_is_also_configured_Then_startup_fails()
        {
            var config = new MainnetChainServerConfig
            {
                Engine = { Enabled = true },
                LightClient = new LightClientConfigSection { BeaconEndpoint = "http://beacon" }
            };

            var composition = Assert.Throws<InvalidOperationException>(() => Build(config));
            var guard = ConsensusStartupGuard.Evaluate(config);

            Assert.Equal(ConsensusStartupGuard.EngineWithLightClientMessage, composition.Message);
            Assert.Equal(ConsensusStartupGuard.Decision.RefuseEngineWithLightClient, guard);
        }

        [Fact]
        public void Given_engine_mode_without_a_light_client_When_startup_is_evaluated_Then_the_consensus_client_drives_and_no_beacon_is_demanded()
        {
            var engine = new MainnetChainServerConfig { Engine = { Enabled = true } };
            var lightClientOnly = new MainnetChainServerConfig { LightClient = new LightClientConfigSection { BeaconEndpoint = "http://beacon" } };
            var neither = new MainnetChainServerConfig();

            Assert.Equal(ConsensusStartupGuard.Decision.EngineDriven, ConsensusStartupGuard.Evaluate(engine));
            Assert.Equal(ConsensusStartupGuard.Decision.Verified, ConsensusStartupGuard.Evaluate(lightClientOnly));
            Assert.Equal(ConsensusStartupGuard.Decision.RefuseNoBeacon, ConsensusStartupGuard.Evaluate(neither));
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "mainnet-engine", "Compose the follower under the Engine API")]
        public void Given_engine_mode_When_the_services_are_composed_Then_the_engine_canonical_source_the_follower_service_and_the_shared_gate_are_registered()
        {
            var jwtPath = Path.Combine(Path.GetTempPath(), $"mainnet-engine-{Guid.NewGuid():N}.hex");
            try
            {
                using var provider = Build(new MainnetChainServerConfig
                {
                    Engine = { Enabled = true, Port = 8561, JwtSecretPath = jwtPath }
                });

                var canonical = provider.GetRequiredService<ICanonicalStateRootSource>();
                var engine = provider.GetRequiredService<IEngineApiService>();
                var hostConfig = provider.GetRequiredService<EngineApiServerConfig>();
                var host = provider.GetRequiredService<EngineApiHost>();

                Assert.Contains("Engine", canonical.Name);
                Assert.IsType<FollowerEngineApiService>(engine);
                Assert.Same(engine, provider.GetRequiredService<FollowerEngineApiService>());
                Assert.Equal(8561, hostConfig.Port);
                Assert.Equal(jwtPath, hostConfig.JwtSecretPath);
                Assert.True(File.Exists(jwtPath));
                Assert.NotNull(host.Validator);
                Assert.Same(provider.GetRequiredService<AsyncGate>(), provider.GetRequiredService<AsyncGate>());
            }
            finally
            {
                if (File.Exists(jwtPath)) File.Delete(jwtPath);
            }
        }

        [Fact]
        public void Given_no_engine_mode_When_the_services_are_composed_Then_no_engine_service_or_host_exists()
        {
            using var provider = Build(new MainnetChainServerConfig());

            Assert.Null(provider.GetService<IEngineApiService>());
            Assert.Null(provider.GetService<EngineApiServerConfig>());
            Assert.Null(provider.GetService<AsyncGate>());
            Assert.DoesNotContain("Engine", provider.GetRequiredService<ICanonicalStateRootSource>().Name);
        }

        [Fact]
        public void Given_engine_mode_without_a_jwt_path_When_composed_Then_the_secret_lives_in_the_data_dir()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), $"mainnet-engine-{Guid.NewGuid():N}");
            try
            {
                using var provider = Build(new MainnetChainServerConfig { DataDir = dataDir, Engine = { Enabled = true } });

                var hostConfig = provider.GetRequiredService<EngineApiServerConfig>();

                Assert.Equal(Path.Combine(dataDir, "jwt.hex"), hostConfig.JwtSecretPath);
                Assert.Equal(8551, hostConfig.Port);
            }
            finally
            {
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            }
        }

        [Fact]
        public void Given_the_engine_cli_flags_When_applied_Then_the_engine_section_is_set_and_without_them_it_stays_off()
        {
            var flagged = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(flagged, new[] { "--engine", "--engine-port", "8552", "--engine-jwt-secret", "/run/jwt.hex" });
            var plain = new MainnetChainServerConfig();
            MainnetChainCliArgs.Apply(plain, new[] { "--port", "8545" });

            Assert.True(flagged.Engine.Enabled);
            Assert.Equal(8552, flagged.Engine.Port);
            Assert.Equal("/run/jwt.hex", flagged.Engine.JwtSecretPath);
            Assert.False(plain.Engine.Enabled);
            Assert.Equal(8551, plain.Engine.Port);
            Assert.Null(plain.Engine.JwtSecretPath);
        }

        [Fact]
        public void Given_an_appsettings_engine_section_When_bound_Then_enabled_port_and_jwt_path_reach_the_config()
        {
            var source = new ConfigurationBuilder().AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                ["MainnetChain:Engine:Enabled"] = "true",
                ["MainnetChain:Engine:Port"] = "8553",
                ["MainnetChain:Engine:JwtSecretPath"] = "/etc/jwt.hex"
            }).Build();
            var config = new MainnetChainServerConfig();

            source.GetSection("MainnetChain").Bind(config);

            Assert.True(config.Engine.Enabled);
            Assert.Equal(8553, config.Engine.Port);
            Assert.Equal("/etc/jwt.hex", config.Engine.JwtSecretPath);
        }

        [Fact]
        public void Given_a_write_gate_When_the_follower_options_are_built_Then_the_gate_reaches_the_follower_and_without_one_there_is_none()
        {
            var gate = new AsyncGate();
            var bounds = new EffectiveStartBlockResolver.EffectiveBounds(1, null, EffectiveStartBlockResolver.StartBlockReason.FreshStart);

            var gated = EffectiveStartBlockResolver.BuildOptions(bounds, new MainnetChainServerConfig(), gate);
            var ungated = EffectiveStartBlockResolver.BuildOptions(bounds, new MainnetChainServerConfig());
            var kept = gated with { ExternalHeaderFollow = true };

            Assert.Same(gate, gated.ChainWriteGate);
            Assert.Null(ungated.ChainWriteGate);
            Assert.Same(gate, kept.ChainWriteGate);
        }
    }
}
