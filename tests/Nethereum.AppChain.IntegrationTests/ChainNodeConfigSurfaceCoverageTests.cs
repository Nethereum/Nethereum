using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nethereum.AppChain.Server;
using Nethereum.ChainNode.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;
using Nethereum.DevChain.Configuration;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeConfigSurfaceCoverageTests
    {
        private static readonly Type[] ForceLoadedAssemblies =
        {
            typeof(Nethereum.ChainNode.Hosting.ChainNode),
            typeof(ChainNodeConfigSurface),
            typeof(AppChainCli),
            typeof(DevChainCli),
            typeof(Microsoft.Extensions.Hosting.Extensions),
        };

        private static readonly HashSet<Type> SubConfigTypes = new HashSet<Type>
        {
            typeof(ChainNodeStorageConfig),
            typeof(ChainNodeNetworkConfig),
            typeof(ChainNodeDiscoveryConfig),
            typeof(ChainNodeSyncConfig),
            typeof(ChainNodeSnapConfig),
            typeof(ChainNodeRpcConfig),
            typeof(ChainNodeMaintenanceConfig),
            typeof(ChainNodeMempoolConfig),
        };

        private static IEnumerable<string> WalkLeafPropertyPaths(Type type, string prefix)
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetSetMethod() == null) continue;
                if (typeof(Delegate).IsAssignableFrom(prop.PropertyType)) continue;

                var path = prefix == null ? prop.Name : $"{prefix}.{prop.Name}";

                if (SubConfigTypes.Contains(prop.PropertyType))
                {
                    foreach (var nested in WalkLeafPropertyPaths(prop.PropertyType, path))
                        yield return nested;
                }
                else
                {
                    yield return path;
                }
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var forceLoaded in ForceLoadedAssemblies) _ = forceLoaded;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type found;
                try { found = assembly.GetType(fullName); }
                catch { found = null; }

                if (found != null) return found;
            }

            return null;
        }

        [Fact]
        public void Given_TheSharedChainNodeConfigSchema_When_EveryLeafPropertyIsWalked_Then_EachHasExactlyOneDescriptorEntry()
        {
            var leaves = WalkLeafPropertyPaths(typeof(ChainNodeConfig), null).ToList();
            var descriptorPaths = ChainNodeConfigSurface.All.Select(f => f.Path).ToList();

            var missing = leaves.Except(descriptorPaths).ToList();
            var extra = descriptorPaths.Except(leaves).ToList();
            var duplicates = descriptorPaths.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            Assert.True(missing.Count == 0,
                $"ChainNodeConfig properties with no ChainNodeConfigSurface entry (a knob that could be silently advertised or silently dropped): {string.Join(", ", missing)}");
            Assert.True(extra.Count == 0,
                $"ChainNodeConfigSurface entries with no matching ChainNodeConfig property (a stale/renamed descriptor row): {string.Join(", ", extra)}");
            Assert.True(duplicates.Count == 0,
                $"ChainNodeConfigSurface entries declared more than once: {string.Join(", ", duplicates)}");
        }

        [Fact]
        public void Given_EveryFieldTheDescriptorMarksLive_When_ItsKnownConsumerIsResolved_Then_TheConsumerTypeAndMemberExist()
        {
            var failures = new List<string>();

            foreach (var kind in new[] { ChainNodeKind.AppChain, ChainNodeKind.DevChain })
            {
                foreach (var field in ChainNodeConfigSurface.All)
                {
                    if (field.LivenessOn(kind) is not ChainNodeFieldLiveness.Live live) continue;

                    var consumerType = FindType(live.ConsumerTypeName);
                    if (consumerType == null)
                    {
                        failures.Add($"{field.Path} ({kind}) claims consumer type '{live.ConsumerTypeName}' which cannot be found in any loaded assembly.");
                        continue;
                    }

                    var hasMember = consumerType.GetMember(
                        live.ConsumerMemberName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Length > 0;

                    if (!hasMember)
                        failures.Add($"{field.Path} ({kind}) claims consumer member '{live.ConsumerTypeName}.{live.ConsumerMemberName}' which does not exist.");
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void Given_NoFieldIsMarkedLiveOnEitherNode_When_TheDescriptorIsEmptyOfLiveEntries_Then_TheCoverageGuardIsNotVacuouslyPassing()
        {
            var liveOnAppChain = ChainNodeConfigSurface.All.Count(f => f.LivenessOn(ChainNodeKind.AppChain) is ChainNodeFieldLiveness.Live);
            var liveOnDevChain = ChainNodeConfigSurface.All.Count(f => f.LivenessOn(ChainNodeKind.DevChain) is ChainNodeFieldLiveness.Live);
            var disabled = ChainNodeConfigSurface.All.Count(f =>
                f.LivenessOn(ChainNodeKind.AppChain) is ChainNodeFieldLiveness.Disabled ||
                f.LivenessOn(ChainNodeKind.DevChain) is ChainNodeFieldLiveness.Disabled);
            var notYetWired = ChainNodeConfigSurface.All.Count(f =>
                f.LivenessOn(ChainNodeKind.AppChain) is ChainNodeFieldLiveness.NotYetWired ||
                f.LivenessOn(ChainNodeKind.DevChain) is ChainNodeFieldLiveness.NotYetWired);

            Assert.True(liveOnAppChain > 0);
            Assert.True(liveOnDevChain > 0);
            Assert.True(disabled > 0);
            Assert.True(notYetWired > 0);
        }

        [Fact]
        public void Given_NetworkDiscoveryIsMarkedDisabledOnAppChainAndDevChain_When_TheRealValidatorSeesItRequested_Then_ItThrows()
        {
            foreach (var kind in new[] { ChainNodeKind.AppChain, ChainNodeKind.DevChain })
            {
                var disabledDiscoveryFields = ChainNodeConfigSurface.All
                    .Where(f => f.Path.StartsWith("Network.Discovery.") && f.LivenessOn(kind) is ChainNodeFieldLiveness.Disabled)
                    .ToList();

                Assert.True(disabledDiscoveryFields.Count == 4,
                    $"Expected all 4 Network.Discovery.* fields to be marked Disabled for {kind}, found {disabledDiscoveryFields.Count}.");

                var requested = new ChainNodeNetworkConfig
                {
                    Discovery = new ChainNodeDiscoveryConfig { DisableDiscv4 = false, DisableDiscv5 = true, Discv4Port = 0, Discv5Port = 0 }
                };

                Assert.Throws<InvalidOperationException>(() => ChainNodeDiscoveryValidator.RefuseIfRequested(requested, kind.ToString()));

                var atDefault = new ChainNodeNetworkConfig
                {
                    Discovery = new ChainNodeDiscoveryConfig { DisableDiscv4 = true, DisableDiscv5 = true, Discv4Port = 0, Discv5Port = 0 }
                };

                var exception = Record.Exception(() => ChainNodeDiscoveryValidator.RefuseIfRequested(atDefault, kind.ToString()));
                Assert.Null(exception);
            }
        }

        [Fact]
        public void Given_HelpAdvancedIsRenderedForDevChain_When_AFieldIsMarkedNotYetWired_Then_ItsLineIsTaggedAndNotClaimedAsWorking()
        {
            var writer = new StringWriter();
            ChainNodeConfigSurface.RenderAdvancedHelp(writer, "DevChain", ChainNodeKind.DevChain);
            var lines = writer.ToString().Split('\n');

            var notYetWiredFields = ChainNodeConfigSurface.All
                .Where(f => !f.Hidden && !f.HasFriendlyFlag && f.LivenessOn(ChainNodeKind.DevChain) is ChainNodeFieldLiveness.NotYetWired)
                .ToList();

            Assert.NotEmpty(notYetWiredFields);

            foreach (var field in notYetWiredFields)
            {
                var canonicalKey = $"--DevChain:Node:{field.Path.Replace('.', ':')} ";
                var line = lines.FirstOrDefault(l => l.Contains(canonicalKey));
                Assert.True(line != null, $"No rendered help line found for NotYetWired field {field.Path}");
                Assert.Contains("[NOT YET WIRED]", line);
            }
        }

        [Fact]
        public void Given_HelpAdvancedIsRenderedForBothNodeKinds_When_RpcHostAndPortAreRendered_Then_BothNodeKindsClaimThemAsLive()
        {
            Assert.IsType<ChainNodeFieldLiveness.Live>(
                ChainNodeConfigSurface.All.Single(f => f.Path == "Rpc.Host").LivenessOn(ChainNodeKind.AppChain));
            Assert.IsType<ChainNodeFieldLiveness.Live>(
                ChainNodeConfigSurface.All.Single(f => f.Path == "Rpc.Host").LivenessOn(ChainNodeKind.DevChain));
            Assert.IsType<ChainNodeFieldLiveness.Live>(
                ChainNodeConfigSurface.All.Single(f => f.Path == "Rpc.Port").LivenessOn(ChainNodeKind.AppChain));
            Assert.IsType<ChainNodeFieldLiveness.Live>(
                ChainNodeConfigSurface.All.Single(f => f.Path == "Rpc.Port").LivenessOn(ChainNodeKind.DevChain));
        }

        [Fact]
        public void Given_RpcCapsAreLiveViaApplyTo_When_ApplyToRunsWithDistinctSentinelValues_Then_EveryValueIsCopiedNotDropped()
        {
            var rpc = new ChainNodeRpcConfig { MaxLogBlockRange = 4242, MaxLogResults = 1313, GasCap = 987654321 };
            var target = new ChainConfig();

            rpc.ApplyTo(target);

            Assert.Equal(4242, target.RpcMaxLogBlockRange);
            Assert.Equal(1313, target.RpcMaxLogResults);
            Assert.Equal((System.Numerics.BigInteger)987654321, target.RpcGasCap);
        }

        [Fact]
        public void Given_StorageFlushCadenceBlocksIsMarkedNotYetWired_When_BuildStorageOptionsRuns_Then_ItIsProvablyNotPresentOnTheResultingOptions()
        {
            var field = ChainNodeConfigSurface.All.Single(f => f.Path == "Storage.FlushCadenceBlocks");
            Assert.IsType<ChainNodeFieldLiveness.NotYetWired>(field.LivenessOn(ChainNodeKind.AppChain));
            Assert.IsType<ChainNodeFieldLiveness.NotYetWired>(field.LivenessOn(ChainNodeKind.DevChain));

            var optionsType = typeof(Nethereum.CoreChain.RocksDB.RocksDbStorageOptions);
            Assert.Null(optionsType.GetProperty("FlushCadenceBlocks"));
        }

        [Fact]
        public void Given_StorageFlushCadenceBlocksIsUnwiredOnBothNodeKinds_When_HelpAdvancedIsRendered_Then_ItIsHiddenFromBoth()
        {
            var field = ChainNodeConfigSurface.All.Single(f => f.Path == "Storage.FlushCadenceBlocks");
            Assert.True(field.Hidden);

            var appChainWriter = new StringWriter();
            ChainNodeConfigSurface.RenderAdvancedHelp(appChainWriter, "AppChain", ChainNodeKind.AppChain);
            Assert.DoesNotContain("FlushCadenceBlocks", appChainWriter.ToString());

            var devChainWriter = new StringWriter();
            ChainNodeConfigSurface.RenderAdvancedHelp(devChainWriter, "DevChain", ChainNodeKind.DevChain);
            Assert.DoesNotContain("FlushCadenceBlocks", devChainWriter.ToString());
        }
    }
}
