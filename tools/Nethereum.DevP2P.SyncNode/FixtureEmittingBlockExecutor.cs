using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.Model;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// IBlockExecutor decorator that emits a regression-cell fixture for
    /// blocks listed in <see cref="_dumpFixtureBlocks"/> AND for any block
    /// whose state root mismatches when <paramref name="_outputDir"/> is set.
    /// Toggles <see cref="FixturePreStateRecorder.IsRecording"/> only around
    /// the target blocks so non-target blocks pay zero capture overhead.
    /// </summary>
    internal sealed class FixtureEmittingBlockExecutor : IBlockExecutor
    {
        private readonly IBlockExecutor _inner;
        private readonly FixturePreStateRecorder _recorder;
        private readonly IStateStore _state;
        private readonly HashSet<ulong> _dumpFixtureBlocks;
        private readonly string _outputDir;
        private readonly Action<string> _log;

        public FixtureEmittingBlockExecutor(
            IBlockExecutor inner,
            FixturePreStateRecorder recorder,
            IStateStore state,
            HashSet<ulong> dumpFixtureBlocks,
            string outputDir,
            Action<string> log)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _dumpFixtureBlocks = dumpFixtureBlocks ?? new HashSet<ulong>();
            _outputDir = outputDir;
            _log = log ?? (_ => { });
        }

        public async Task<BlockImporterResult> ProcessBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<WithdrawalEntry> withdrawals,
            CancellationToken ct)
        {
            ulong blockNumber = (ulong)(long)header.BlockNumber;
            bool isOnDemandTarget = _dumpFixtureBlocks.Contains(blockNumber);
            bool autoEmitOnMismatchEnabled = !string.IsNullOrEmpty(_outputDir);
            bool shouldRecord = isOnDemandTarget || autoEmitOnMismatchEnabled;

            if (shouldRecord) _recorder.BeginRecording();
            try
            {
                var result = await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct)
                    .ConfigureAwait(false);

                bool shouldEmit = isOnDemandTarget
                    || (autoEmitOnMismatchEnabled && !result.RootMatches);

                if (shouldEmit && !string.IsNullOrEmpty(_outputDir))
                {
                    try
                    {
                        string scenario = isOnDemandTarget ? "on-demand" : "auto-on-mismatch";
                        string error = result.RootMatches
                            ? null
                            : $"state-root mismatch: computed=0x{(result.ComputedStateRoot != null ? Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(result.ComputedStateRoot) : "<null>")} expected=0x{(result.ExpectedStateRoot != null ? Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(result.ExpectedStateRoot) : "<null>")}";

                        await MainnetBlockFixtureEmitter.EmitFromRecorderAsync(
                            _outputDir,
                            header,
                            transactions,
                            uncles,
                            _state,
                            _recorder,
                            scenario,
                            error).ConfigureAwait(false);

                        _log($"  fixture emitted to {_outputDir}/block-{blockNumber}.json ({scenario})");
                    }
                    catch (Exception ex)
                    {
                        _log($"  fixture emit failed for block {blockNumber}: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                return result;
            }
            finally
            {
                if (shouldRecord) _recorder.EndRecording();
            }
        }
    }
}
