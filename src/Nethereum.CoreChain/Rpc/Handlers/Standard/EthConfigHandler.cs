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
                Current = EntryFor(config, config.ResolveHardforkAt(headBlock, headTime),
                    ActivationTimeAt(config, headTime, present: true), genesisHash, thresholds, headBlock, headTime),
                Last = LastEntry(config, headTime, genesisHash, thresholds, headBlock),
                Next = NextEntry(config, headTime, genesisHash, thresholds, headBlock)
            };

            return Success(request.Id, response);
        }

        private static ChainConfigurationEntry EntryFor(
            ChainConfig config, HardforkName fork, ulong activationTime,
            byte[] genesisHash, ForkIdentityThresholds thresholds, long atBlock, ulong atTime)
        {
            var hardforkConfig = config.ConfigForFork(fork);
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
                SystemContracts = new Dictionary<string, string>(forkConfig.SystemContracts)
            };
        }

        private static ChainConfigurationEntry NextEntry(
            ChainConfig config, ulong headTime, byte[] genesisHash, ForkIdentityThresholds thresholds, long headBlock)
        {
            var next = TimestampForksAscending(config).FirstOrDefault(f => f.Timestamp > headTime);
            if (next.Fork == HardforkName.Unspecified) return null;

            return EntryFor(config, next.Fork, next.Timestamp, genesisHash, thresholds, headBlock, next.Timestamp);
        }

        private static ChainConfigurationEntry LastEntry(
            ChainConfig config, ulong headTime, byte[] genesisHash, ForkIdentityThresholds thresholds, long headBlock)
        {
            var last = TimestampForksAscending(config).LastOrDefault(f => f.Timestamp > headTime);
            if (last.Fork == HardforkName.Unspecified) return null;

            return EntryFor(config, last.Fork, last.Timestamp, genesisHash, thresholds, headBlock, last.Timestamp);
        }

        private static ulong ActivationTimeAt(ChainConfig config, ulong headTime, bool present)
        {
            var current = TimestampForksAscending(config).Where(f => f.Timestamp <= headTime).ToList();
            return current.Count > 0 ? current[current.Count - 1].Timestamp : 0UL;
        }

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

        private static IEnumerable<(HardforkName Fork, ulong Timestamp)> TimestampForksAscending(ChainConfig config) =>
            config.ForkSchedule.Schedule
                .Where(e => e.Timestamp.HasValue)
                .Select(e => (Fork: HardforkNames.Parse(e.Fork), Timestamp: e.Timestamp.Value))
                .OrderBy(e => e.Timestamp);
    }
}
