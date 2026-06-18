using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.MainnetChain.Server.Gate
{
    /// <summary>
    /// Wraps an inner <see cref="IBlockExecutor"/> with an <see cref="IConsensusBlockGate"/>
    /// pre-check. Computes the header's keccak hash via <see cref="BlockHeaderEncoder.Current"/>
    /// (matches the canonical hash the network advertises), consults the gate, and only forwards
    /// the call to the inner executor when the gate accepts. A rejection short-circuits with a
    /// <see cref="BlockImporterResult"/> carrying <c>ErrorMessage</c>; the follower's policy
    /// then routes it through the divergence path.
    /// </summary>
    public sealed class ConsensusGatedBlockExecutor : IBlockExecutor
    {
        private static readonly Sha3Keccack _keccak = new Sha3Keccack();

        private readonly IBlockExecutor _inner;
        private readonly IConsensusBlockGate _gate;
        private readonly ILogger _logger;

        public ConsensusGatedBlockExecutor(IBlockExecutor inner, IConsensusBlockGate gate, ILogger? logger = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _logger = logger ?? NullLogger.Instance;
        }

        public async Task<BlockImporterResult> ProcessBlockAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader> uncles,
            IList<WithdrawalEntry> withdrawals,
            CancellationToken ct)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            var encoded = BlockHeaderEncoder.Current.Encode(header);
            var blockHash = _keccak.CalculateHash(encoded);

            var verdict = await _gate.IsBlockCanonicalAsync(header, blockHash, ct).ConfigureAwait(false);
            if (!verdict.Accepted)
            {
                _logger.LogWarning(
                    "Consensus gate rejected block {BlockNumber} hash=0x{Hash}: {Reason}",
                    (ulong)(System.Numerics.BigInteger)header.BlockNumber,
                    blockHash.ToHex(),
                    verdict.Reason ?? "unspecified");

                return new BlockImporterResult
                {
                    StateRootMismatch = false,
                    ErrorMessage = verdict.Reason ?? "consensus gate rejected block",
                    ExpectedStateRoot = header.StateRoot,
                };
            }

            return await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct).ConfigureAwait(false);
        }
    }
}
