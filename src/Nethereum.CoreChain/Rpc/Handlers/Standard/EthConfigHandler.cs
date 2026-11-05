using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.EVM;
using Nethereum.EVM.ForkId;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthConfigHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_config.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var config = context.Node.Config;
            var head = await context.Node.GetLatestBlockAsync();
            var headBlock = head != null ? (long)head.BlockNumber : 0L;
            var headTime = head != null ? (ulong)head.Timestamp : 0UL;

            var genesisHash = await context.Node.GetBlockHashByNumberAsync(0);
            var thresholds = config.ForkSchedule.ForkThresholds();

            var response = new ChainConfiguration
            {
                Current = EntryFor(config, ActivationTimeAt(config, headTime), genesisHash, thresholds, headBlock, headTime),
                Last = LastEntry(config, headTime, genesisHash, thresholds, headBlock),
                Next = NextEntry(config, headTime, genesisHash, thresholds, headBlock)
            };

            return Success(request.Id, response);
        }

        private static ChainConfigurationEntry EntryFor(
            ChainConfig config, ulong activationTime,
            byte[] genesisHash, ForkIdentityThresholds thresholds, long atBlock, ulong atTime)
        {
            var fork = config.ResolveHardforkAt(atBlock, atTime);
            var hardforkConfig = config.GetHardforkConfigAt(atBlock, atTime);
            var forkConfig = ForkConfiguration.For(fork, hardforkConfig, config.DepositContractAddress);
            var forkId = Eip2124ForkIdCalculator.NewId(
                genesisHash, thresholds.BlockHeights, thresholds.Timestamps, (ulong)atBlock, atTime);

            return new ChainConfigurationEntry
            {
                ChainId = config.ChainId.ToHex(false),
                ActivationTime = activationTime,
                ForkId = "0x" + forkId.Hash.ToString("x8"),
                BlobSchedule = BlobScheduleFor(hardforkConfig),
                Precompiles = new Dictionary<string, string>(forkConfig.Precompiles),
                SystemContracts = forkConfig.SystemContracts.ToDictionary(p => p.Key, p => p.Value.ToLowerInvariant())
            };
        }

        private static ChainConfigurationEntry NextEntry(
            ChainConfig config, ulong headTime, byte[] genesisHash, ForkIdentityThresholds thresholds, long headBlock)
        {
            var later = ActivationTimesAscending(config).Where(t => t > headTime).ToList();
            if (later.Count == 0) return null;

            return EntryFor(config, later[0], genesisHash, thresholds, headBlock, later[0]);
        }

        private static ChainConfigurationEntry LastEntry(
            ChainConfig config, ulong headTime, byte[] genesisHash, ForkIdentityThresholds thresholds, long headBlock)
        {
            var later = ActivationTimesAscending(config).Where(t => t > headTime).ToList();
            if (later.Count == 0) return null;

            var last = later[later.Count - 1];
            return EntryFor(config, last, genesisHash, thresholds, headBlock, last);
        }

        private static ulong ActivationTimeAt(ChainConfig config, ulong headTime) =>
            ActivationTimesAscending(config).Where(t => t <= headTime).DefaultIfEmpty(0UL).Max();

        private static BlobScheduleConfiguration BlobScheduleFor(HardforkConfig hardforkConfig)
        {
            var blob = hardforkConfig.IntrinsicGasRules?.Blob;
            if (blob == null) return null;

            return new BlobScheduleConfiguration
            {
                Target = (ulong)hardforkConfig.TargetBlobsPerBlock,
                Max = (ulong)hardforkConfig.MaxBlobsPerBlock,
                BaseFeeUpdateFraction = (ulong)blob.BaseFeeUpdateFraction
            };
        }

        private static IEnumerable<ulong> ActivationTimesAscending(ChainConfig config) =>
            config.ForkSchedule.Schedule
                .Where(e => e.Timestamp.HasValue)
                .Select(e => e.Timestamp.Value)
                .Concat((config.ForkSchedule.BlobSchedule ?? new List<BlobScheduleActivationEntry>())
                    .Where(e => e.Timestamp.HasValue)
                    .Select(e => e.Timestamp.Value))
                .Distinct()
                .OrderBy(t => t);
    }
}
