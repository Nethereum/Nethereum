using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class ChainNodeSyncOptionsWiringTests
    {
        [Fact]
        public void Given_DistinctSyncConfigValues_When_ResolvedOnAFreshChain_Then_FollowerOptionsCarryStartBlockBlocksCheckpointEveryAndKeepLatestCheckpoints()
        {
            var sync = new ChainNodeSyncConfig
            {
                StartBlock = 777,
                Blocks = 100,
                CheckpointEvery = 42,
                KeepLatestCheckpoints = 3,
            };

            var bounds = ChainNodeFollowerOptionsBuilder.Resolve(sync, snapState: null, lastBlock: 0);
            var options = ChainNodeFollowerOptionsBuilder.BuildFollowerOptions(bounds, sync);

            Assert.Equal(ChainNodeFollowerOptionsBuilder.EffectiveStartBlockReason.FreshStart, bounds.Reason);
            Assert.Equal(777UL, options.StartBlock);
            Assert.Equal(876UL, options.EndBlock);
            Assert.Equal(42UL, options.CheckpointEvery);
            Assert.Equal(3, options.KeepLatestCheckpoints);
        }

        [Fact]
        public void Given_ExistingCommittedState_When_Resolved_Then_ItResumesFromLastBlockPlusOneIgnoringConfiguredStartBlock()
        {
            var sync = new ChainNodeSyncConfig { StartBlock = 777 };

            var bounds = ChainNodeFollowerOptionsBuilder.Resolve(sync, snapState: null, lastBlock: 500);

            Assert.Equal(ChainNodeFollowerOptionsBuilder.EffectiveStartBlockReason.ResumeFromLastBlock, bounds.Reason);
            Assert.Equal(501UL, bounds.StartBlock);
        }

        [Fact]
        public void Given_ACompletedSnapPivotAheadOfLastBlock_When_Resolved_Then_ItFastStartsAfterThePivot()
        {
            var sync = new ChainNodeSyncConfig { StartBlock = 1 };
            var snapState = new SnapSyncState
            {
                SchemaVersion = 1,
                Phase = SnapPhase.Complete,
                PivotBlockNumber = 900,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Tasks = System.Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            };

            var bounds = ChainNodeFollowerOptionsBuilder.Resolve(sync, snapState, lastBlock: 500);

            Assert.Equal(ChainNodeFollowerOptionsBuilder.EffectiveStartBlockReason.PostSnapPivotFastStart, bounds.Reason);
            Assert.Equal(901UL, bounds.StartBlock);
        }

        [Fact]
        public void Given_ContinueOnMismatchTrue_When_BuildStrictValidationPolicyRuns_Then_ItContinuesPastADivergenceInsteadOfHalting()
        {
            var sync = new ChainNodeSyncConfig { ContinueOnMismatch = true };
            var policy = ChainNodeFollowerOptionsBuilder.BuildStrictValidationPolicy(sync);

            var verdict = new DivergenceVerdict(DivergenceOutcome.EvmBug, new byte[32], new byte[32], "peer", "test");
            var action = policy.OnVerdict(verdict, 42);

            Assert.Equal(ValidationAction.Continue, action);
        }

        [Fact]
        public void Given_ContinueOnMismatchFalse_When_BuildStrictValidationPolicyRunsAgainstAnEvmBugVerdict_Then_ItHaltsFatally()
        {
            var sync = new ChainNodeSyncConfig { ContinueOnMismatch = false };
            var policy = ChainNodeFollowerOptionsBuilder.BuildStrictValidationPolicy(sync);

            var verdict = new DivergenceVerdict(DivergenceOutcome.EvmBug, new byte[32], new byte[32], "peer", "test");
            var action = policy.OnVerdict(verdict, 42);

            Assert.Equal(ValidationAction.Fatal, action);
        }

        [Fact]
        public void Given_DistinctSnapConfigValues_When_ChainNodeSnapBootstrapOptionsBuilderBuilds_Then_TheOrchestratorOptionsCarryEveryConfiguredField()
        {
            var sync = new ChainNodeSyncConfig
            {
                HeadersFrom = 123,
                HeadersTo = 456,
                Snap = new ChainNodeSnapConfig
                {
                    BackwardSkeletonPhase1 = false,
                    Phase1Only = true,
                    Phase1First = true,
                },
            };

            var options = ChainNodeSnapBootstrapOptionsBuilder.Build(sync);

            Assert.False(options.UseBackwardSkeleton);
            Assert.True(options.BackfillOnly);
            Assert.True(options.Phase1First);
            Assert.Equal((123UL, 456UL), options.HeaderSweepOverride);
        }

        [Fact]
        public void Given_NoHeadersFromOverrideConfigured_When_Built_Then_HeaderSweepOverrideIsNull()
        {
            var sync = new ChainNodeSyncConfig();

            var options = ChainNodeSnapBootstrapOptionsBuilder.Build(sync);

            Assert.Null(options.HeaderSweepOverride);
        }

        [Fact]
        public void Given_TheAppChainDefaultSyncConfig_When_ResolvedOnAFreshGenesisStart_Then_ExistingAppChainFollowersKeepTheirPreSlice6Behaviour()
        {
            var defaultSync = new AppChainServerConfig().Node.Sync;

            Assert.Equal(0UL, defaultSync.CheckpointEvery);
            Assert.False(defaultSync.ContinueOnMismatch);
            Assert.False(defaultSync.ReceiptBackfill);
            Assert.True(defaultSync.Snap.BackwardSkeletonPhase1);
            Assert.False(defaultSync.Snap.Phase1Only);
            Assert.False(defaultSync.Snap.Phase1First);

            var freshBounds = ChainNodeFollowerOptionsBuilder.Resolve(defaultSync, snapState: null, lastBlock: 0);
            Assert.Equal(1UL, freshBounds.StartBlock);

            var resumedBounds = ChainNodeFollowerOptionsBuilder.Resolve(defaultSync, snapState: null, lastBlock: 250);
            Assert.Equal(251UL, resumedBounds.StartBlock);

            var defaultSnapOptions = ChainNodeSnapBootstrapOptionsBuilder.Build(defaultSync);
            Assert.True(defaultSnapOptions.UseBackwardSkeleton);
            Assert.False(defaultSnapOptions.BackfillOnly);
            Assert.False(defaultSnapOptions.Phase1First);
            Assert.Null(defaultSnapOptions.HeaderSweepOverride);
        }

        private static AppChainDevP2PFollower UninitializedFollowerWithConfig(AppChainServerConfig config)
        {
            var follower = (AppChainDevP2PFollower)RuntimeHelpers.GetUninitializedObject(typeof(AppChainDevP2PFollower));
            var followerType = typeof(AppChainDevP2PFollower);

            followerType.GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(follower, config);
            followerType.GetField("_loggerFactory", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(follower, NullLoggerFactory.Instance);

            return follower;
        }

        private static StrictValidationPolicy InvokeStrictPolicy(AppChainDevP2PFollower follower)
        {
            var method = typeof(AppChainDevP2PFollower).GetMethod("StrictPolicy", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (StrictValidationPolicy)method.Invoke(follower, null)!;
        }

        [Fact]
        public void Given_TheRealAppChainDevP2PFollower_When_ConfigSetsContinueOnMismatchTrue_Then_ItsStrictPolicyContinuesPastADivergence()
        {
            var config = new AppChainServerConfig();
            config.Node.Sync.ContinueOnMismatch = true;
            var follower = UninitializedFollowerWithConfig(config);

            var policy = InvokeStrictPolicy(follower);
            var verdict = new DivergenceVerdict(DivergenceOutcome.EvmBug, new byte[32], new byte[32], "peer", "test");

            Assert.Equal(ValidationAction.Continue, policy.OnVerdict(verdict, 1));
        }

        [Fact]
        public void Given_TheRealAppChainDevP2PFollower_When_ConfigLeavesContinueOnMismatchAtItsDefault_Then_ItsStrictPolicyHaltsFatallyOnAnEvmBugVerdict()
        {
            var config = new AppChainServerConfig();
            var follower = UninitializedFollowerWithConfig(config);

            var policy = InvokeStrictPolicy(follower);
            var verdict = new DivergenceVerdict(DivergenceOutcome.EvmBug, new byte[32], new byte[32], "peer", "test");

            Assert.Equal(ValidationAction.Fatal, policy.OnVerdict(verdict, 1));
        }
    }
}
