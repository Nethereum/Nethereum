using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// Production <see cref="IValidationPolicy"/> for mainnet sync. Defaults to
    /// RewindAndRetry on divergence; falls through to Continue when
    /// <c>--continue-on-mismatch</c> is supplied. Periodic anchor checks fire
    /// when <c>anchorEvery &gt; 0</c>: at every committed block that is a multiple
    /// of <c>anchorEvery</c>, the follower asks the canonical source whether
    /// our computed state root matches, even if the peer-delivered header
    /// agreed. Set 0 to disable.
    /// </summary>
    internal sealed class ProductionValidationPolicy : IValidationPolicy
    {
        private readonly bool _continueOnMismatch;
        private readonly ulong _anchorEvery;
        private readonly ILogger<ProductionValidationPolicy> _logger;

        public ProductionValidationPolicy(
            bool continueOnMismatch,
            ulong anchorEvery = 0,
            ILogger<ProductionValidationPolicy> logger = null)
        {
            _continueOnMismatch = continueOnMismatch;
            _anchorEvery = anchorEvery;
            _logger = logger ?? NullLogger<ProductionValidationPolicy>.Instance;
        }

        public bool ShouldAnchorAt(ulong block)
            => _anchorEvery > 0 && block > 0 && block % _anchorEvery == 0;

        public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber)
        {
            if (_continueOnMismatch)
            {
                _logger.LogWarning("divergence: block={Block} detail={Detail} — continuing per --continue-on-mismatch",
                    blockNumber, verdict.Detail);
                return ValidationAction.Continue;
            }

            switch (verdict.Outcome)
            {
                case DivergenceOutcome.EvmBug:
                    _logger.LogCritical("EVM bug: block={Block} detail={Detail} — fatal",
                        blockNumber, verdict.Detail);
                    return ValidationAction.Fatal;

                case DivergenceOutcome.PeerLied:
                case DivergenceOutcome.SourceUnavailable:
                default:
                    _logger.LogWarning("divergence: block={Block} detail={Detail} — rewinding",
                        blockNumber, verdict.Detail);
                    return ValidationAction.RewindAndRetry;
            }
        }
    }
}
