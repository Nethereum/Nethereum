using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Nethereum.ChainNode.Hosting.Configuration;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeSyncConfigTests
    {
        public static IEnumerable<object[]> NonSnapModes() =>
            Enum.GetValues<SyncMode>().Where(m => m != SyncMode.Snap).Select(m => new object[] { m });

        [Theory]
        [MemberData(nameof(NonSnapModes))]
        public void Given_ANonSnapModeBound_When_SnapBootstrapIsBoundFalseAfterIt_Then_TheModeIsUnchanged(SyncMode mode)
        {
            var sync = new ChainNodeSyncConfig { Mode = mode };

            sync.SnapBootstrap = false;

            Assert.Equal(mode, sync.Mode);
        }

        [Theory]
        [MemberData(nameof(NonSnapModes))]
        public void Given_ASectionWithANonSnapModeAndSnapBootstrapFalse_When_Bound_Then_TheModeIsUnchanged(SyncMode mode)
        {
            var section = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Sync:Mode"] = mode.ToString(),
                    ["Sync:SnapBootstrap"] = "false",
                })
                .Build()
                .GetSection("Sync");

            var sync = new ChainNodeSyncConfig();
            section.Bind(sync);

            Assert.Equal(mode, sync.Mode);
        }

        [Fact]
        public void Given_SnapBootstrapTrue_Then_ModeIsSnap()
        {
            var sync = new ChainNodeSyncConfig { Mode = SyncMode.None };

            sync.SnapBootstrap = true;

            Assert.Equal(SyncMode.Snap, sync.Mode);
        }

        [Fact]
        public void Given_ModeSnap_When_SnapBootstrapFalse_Then_ModeIsForwardExecute()
        {
            var sync = new ChainNodeSyncConfig { Mode = SyncMode.Snap };

            sync.SnapBootstrap = false;

            Assert.Equal(SyncMode.ForwardExecute, sync.Mode);
        }
    }
}
