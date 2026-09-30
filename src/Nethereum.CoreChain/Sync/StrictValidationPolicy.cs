using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Validation;

namespace Nethereum.CoreChain.Sync
{
    public sealed class StrictValidationPolicy : IValidationPolicy
    {
        private readonly bool _continueOnMismatch;
        private readonly ulong _anchorEvery;
        private readonly ILogger<StrictValidationPolicy> _logger;

        public StrictValidationPolicy(
            bool continueOnMismatch,
            ulong anchorEvery = 0,
            ILogger<StrictValidationPolicy> logger = null)
        {
            _continueOnMismatch = continueOnMismatch;
            _anchorEvery = anchorEvery;
            _logger = logger ?? NullLogger<StrictValidationPolicy>.Instance;
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
