using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Sync;
using Nethereum.DevChain;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Util;

namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class EestBlockchainTestsEngineDriver : IConformanceDriver
    {
        public static readonly EestBlockchainTestsEngineDriver Instance = new();

        public string SuiteId => "eest-blockchain-tests-engine";
        public string SuiteName => "Nethereum EEST blockchain_tests - consume-engine (full node, engine_newPayload)";

        public static readonly HardforkRegistry FixtureRegistry = EestBlockchainTestsRlpDriver.FixtureRegistry;

        public async Task<ConformanceCaseResult> RunAsync(
            BlockchainTestLoader.BlockchainTest test, List<EnginePayloadLoader.EnginePayloadEntry> payloads)
        {
            if (payloads.Count == 0)
                return ConformanceCaseResult.Fail("noEnginePayloads",
                    $"Test '{test.Name}' carries no engineNewPayloads entries.");

            ChainForkSchedule schedule;
            try
            {
                schedule = ForkScheduleResolver.Resolve(test.Network);
            }
            catch (Exception ex)
            {
                return ConformanceCaseResult.Fail("malformedNetwork",
                    $"Test '{test.Name}' has an unrecognised Network value '{test.Network}': {ex.Message}");
            }

            var genesisFork = schedule.ResolveActivations().ResolveAt(0, 0);
            var document = EestGenesisBuilder.BuildGenesisDocument(test);
            var registry = FixtureBlobScheduleOverride.BuildRegistry(FixtureRegistry, test.BlobSchedule);

            await using var harness = await FullNodeHarness.ComposeAsync(document, schedule, registry).ConfigureAwait(false);
            var node = harness.Node;

            var genesisCheck = await VerifyGenesisAsync(node, test, genesisFork).ConfigureAwait(false);
            if (genesisCheck != null) return genesisCheck;

            var engine = harness.BuildEngineApiService();

            byte[] lastValidHash = null;
            for (int i = 0; i < payloads.Count; i++)
            {
                var entry = payloads[i];

                if (entry.IsNegative)
                    return await RunNegativePayloadAsync(engine, entry, i).ConfigureAwait(false);

                PayloadStatusV1 status;
                try
                {
                    status = await DispatchAsync(engine, entry).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return ConformanceCaseResult.Fail("exception",
                        $"Payload {i}: {ex.GetType().Name}: {ex.Message}");
                }

                if (status.Status != EnginePayloadStatus.Valid)
                {
                    return ConformanceCaseResult.Fail("payloadNotValid",
                        $"Payload {i}: expected VALID but engine returned {status.Status} " +
                        $"(validationError: {status.ValidationError ?? "none"})");
                }

                lastValidHash = status.LatestValidHash?.HexToByteArray();
            }

            return await VerifyChainReachedTipAsync(node, test, payloads, lastValidHash).ConfigureAwait(false);
        }

        private async Task<ConformanceCaseResult> RunNegativePayloadAsync(
            EngineApiService engine, EnginePayloadLoader.EnginePayloadEntry entry, int index)
        {
            PayloadStatusV1 status;
            try
            {
                status = await DispatchAsync(engine, entry).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (entry.HasErrorCode)
                {
                    if (ex is RpcException rpcEx && entry.ErrorCode == rpcEx.Code)
                        return ConformanceCaseResult.Ok();

                    return ConformanceCaseResult.Fail("rejectionReasonMismatch",
                        $"Payload {index}: expected errorCode={entry.ErrorCode} but the engine threw {ex.GetType().Name}: {ex.Message}");
                }

                if (string.IsNullOrEmpty(entry.ValidationError))
                    return ConformanceCaseResult.Ok();

                if (ExpectedRejectionMatcher.SatisfiedByDecodeFailure(entry.ValidationError))
                    return ConformanceCaseResult.Ok();

                if (ExpectedRejectionMatcher.Satisfies(entry.ValidationError, ex, out var faultDetail))
                    return ConformanceCaseResult.Ok();

                return ConformanceCaseResult.Fail("rejectionReasonMismatch",
                    $"Payload {index}: expected validationError=\"{entry.ValidationError}\" but the engine threw {ex.GetType().Name}: {ex.Message} - {faultDetail}");
            }

            if (status.Status == EnginePayloadStatus.Valid)
            {
                return ConformanceCaseResult.Fail("rejectionNotEnforced",
                    $"Payload {index}: engine returned VALID for a payload that must be rejected " +
                    $"(expected validationError=\"{entry.ValidationError}\")");
            }

            if (status.Status == EnginePayloadStatus.Syncing)
            {
                return ConformanceCaseResult.Fail("engineSyncing",
                    $"Payload {index}: engine returned SYNCING (parent not found) for a rejection fixture");
            }

            if (string.IsNullOrEmpty(entry.ValidationError))
                return ConformanceCaseResult.Ok();

            var verdict = ExpectedRejectionMatcher.MatchEngineInvalidReason(
                entry.ValidationError, status.ValidationError, out var detail);

            return verdict == ExpectedRejectionMatcher.EngineReasonVerdict.Mismatch
                ? ConformanceCaseResult.Fail("rejectionReasonMismatch", $"Payload {index}: {detail}")
                : ConformanceCaseResult.Ok();
        }

        private static Task<PayloadStatusV1> DispatchAsync(
            EngineApiService engine, EnginePayloadLoader.EnginePayloadEntry entry) => entry.Version switch
        {
            1 or 2 or 3 => engine.NewPayloadAsync(entry.Payload, entry.ParentBeaconBlockRoot),
            4 => engine.NewPayloadV4Async(entry.Payload, entry.ExecutionRequests, entry.ParentBeaconBlockRoot),
            5 => engine.NewPayloadV5Async(entry.PayloadV4, entry.ExecutionRequests, entry.ParentBeaconBlockRoot),
            _ => throw new NotSupportedException($"newPayloadVersion {entry.Version} is not a known engine payload version.")
        };

        private static async Task<ConformanceCaseResult> VerifyGenesisAsync(
            DevChainNode node, BlockchainTestLoader.BlockchainTest test, HardforkName genesisFork)
        {
            if (test.GenesisBlockHeader.StateRoot is { Length: > 0 })
            {
                var genesisBlock = await node.Blocks.GetByNumberAsync(0).ConfigureAwait(false);
                var genesisRoot = genesisBlock?.StateRoot;
                if (genesisRoot == null || !genesisRoot.AreTheSame(test.GenesisBlockHeader.StateRoot))
                {
                    return ConformanceCaseResult.Fail("genesisStateRoot",
                        $"expected=0x{test.GenesisBlockHeader.StateRoot.ToHex()} actual=0x{genesisRoot?.ToHex()}");
                }
            }

            if (test.GenesisBlockHeader.Hash is { Length: 32 })
            {
                var genesisBlock = await node.Blocks.GetByNumberAsync(0).ConfigureAwait(false);
                var genesisHash = genesisBlock != null ? BlockHashCalculator.ForFork(genesisBlock, genesisFork) : null;
                if (genesisHash == null || !genesisHash.AreTheSame(test.GenesisBlockHeader.Hash))
                {
                    return ConformanceCaseResult.Fail("genesisHash",
                        $"expected=0x{test.GenesisBlockHeader.Hash.ToHex()} actual=0x{genesisHash?.ToHex()}");
                }
            }

            return null;
        }

        private static async Task<ConformanceCaseResult> VerifyChainReachedTipAsync(
            DevChainNode node,
            BlockchainTestLoader.BlockchainTest test,
            List<EnginePayloadLoader.EnginePayloadEntry> payloads,
            byte[] lastValidHash)
        {
            var head = await node.Blocks.GetLatestAsync().ConfigureAwait(false);
            if (head == null || (long)head.BlockNumber != payloads.Count)
            {
                return ConformanceCaseResult.Fail("headNotAdvanced",
                    $"expected head at block {payloads.Count} after {payloads.Count} VALID payloads, actual head={(head == null ? "null" : head.BlockNumber.ToString())}");
            }

            if (test.LastBlockHash is { Length: 32 })
            {
                if (lastValidHash == null || !lastValidHash.AreTheSame(test.LastBlockHash))
                {
                    return ConformanceCaseResult.Fail("lastBlockHash",
                        $"expected lastblockhash=0x{test.LastBlockHash.ToHex()} actual latestValidHash=0x{lastValidHash?.ToHex()}");
                }
            }

            var expectedStateRoot = LastPayloadStateRoot(payloads[payloads.Count - 1]);
            if (expectedStateRoot is { Length: > 0 })
            {
                if (head.StateRoot == null || !head.StateRoot.AreTheSame(expectedStateRoot))
                {
                    return ConformanceCaseResult.Fail("headStateRoot",
                        $"head state root 0x{head.StateRoot?.ToHex()} != final payload state root 0x{expectedStateRoot.ToHex()}");
                }
            }

            return ConformanceCaseResult.Ok();
        }

        private static byte[] LastPayloadStateRoot(EnginePayloadLoader.EnginePayloadEntry entry)
        {
            var stateRoot = entry.PayloadV4?.StateRoot ?? entry.Payload?.StateRoot;
            return string.IsNullOrEmpty(stateRoot) ? Array.Empty<byte>() : stateRoot.HexToByteArray();
        }
    }
}
