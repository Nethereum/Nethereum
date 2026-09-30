using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.EVM.ForkId;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth
{
    public class ForkBoundaryConsistencyTests
    {
        private static readonly string[] ExcludedFromForkIdBlockList =
        {
            nameof(MainnetChainActivations.FrontierThawingBlock),
            nameof(MainnetChainActivations.ParisBlock)
        };

        private static IEnumerable<(string Name, long Value)> ActivationBlockConstants()
            => typeof(MainnetChainActivations)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(long))
                .Select(f => (f.Name, (long)f.GetValue(null)));

        private static IEnumerable<(string Name, ulong Value)> ActivationTimestampConstants()
        {
            foreach (var field in typeof(MainnetChainActivations).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(ulong))
                    yield return (field.Name, (ulong)field.GetValue(null));
                else if (field.FieldType == typeof(ulong?) && field.GetValue(null) is ulong value)
                    yield return (field.Name, value);
            }
        }

        [Fact]
        public void Given_TheForkIdBoundaries_When_ComparedToTheExecutionActivations_Then_EveryOneMatchesTheSameFork()
        {
            var activationBlocks = ActivationBlockConstants().ToArray();
            foreach (var blockHeight in MainnetChainSchedule.ForkIdentity.BlockHeights.Distinct())
            {
                Assert.Contains(activationBlocks, c => c.Value == (long)blockHeight);
            }

            var activationTimestamps = ActivationTimestampConstants().ToArray();
            foreach (var timestamp in MainnetChainSchedule.ForkIdentity.Timestamps.Distinct())
            {
                Assert.Contains(activationTimestamps, c => c.Value == timestamp);
            }
        }

        [Fact]
        public void Given_TheExecutionActivations_When_AnEntryIsAbsentFromTheForkIdList_Then_ItIsOnlyTheThawingBlockOrATtdTriggeredFork()
        {
            var forkIdBlocks = new HashSet<long>(MainnetChainSchedule.ForkIdentity.BlockHeights.Select(v => (long)v));

            foreach (var (name, value) in ActivationBlockConstants())
            {
                if (forkIdBlocks.Contains(value)) continue;
                Assert.Contains(name, ExcludedFromForkIdBlockList);
            }

            var forkIdTimestamps = new HashSet<ulong>(MainnetChainSchedule.ForkIdentity.Timestamps);
            foreach (var (name, value) in ActivationTimestampConstants())
            {
                Assert.True(forkIdTimestamps.Contains(value), $"{name} ({value}) is a timestamp-triggered fork absent from the fork-ID list.");
            }
        }

        [Fact]
        public void Given_TwoForksAtTheSameBlock_When_TheForkIdIsComputed_Then_ThatBlockIsChecksummedOnlyOnce()
        {
            Assert.Equal(MainnetChainActivations.ConstantinopleBlock, MainnetChainActivations.PetersburgBlock);

            var genesis = MainnetGenesisConstants.BlockHashHex.HexToByteArray();

            var withBothForksListed = Eip2124ForkIdCalculator.ComputeForkHash(
                genesis,
                new ulong[] { (ulong)MainnetChainActivations.ConstantinopleBlock, (ulong)MainnetChainActivations.PetersburgBlock },
                Array.Empty<ulong>());

            var checksummedOnce = Eip2124ForkIdCalculator.ComputeForkHash(
                genesis,
                new ulong[] { (ulong)MainnetChainActivations.ConstantinopleBlock },
                Array.Empty<ulong>());

            Assert.Equal(checksummedOnce, withBothForksListed);
        }
    }
}
