using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Proving;

namespace Nethereum.CoreChain.DataAvailability
{
    public class AnchorPublicationConfig
    {
        public int AnchorGranularity { get; set; } = 1;
        public ProofCadenceMode ProofMode { get; set; } = ProofCadenceMode.Off;
        public ProofCarrierMode? ProofCarrier { get; set; }
        public DaMode DaMode { get; set; } = DaMode.None;
        public string DaCarrier { get; set; }
    }

    public static class AnchorConfigValidator
    {
        public static List<string> Validate(AnchorPublicationConfig config)
        {
            var errors = new List<string>();

            if (config.ProofMode == ProofCadenceMode.Off && config.ProofCarrier.HasValue)
                errors.Add("ProofMode is Off but ProofCarrier is set — nothing to carry.");

            if (config.ProofMode != ProofCadenceMode.Off && !config.ProofCarrier.HasValue)
                errors.Add($"ProofMode is {config.ProofMode} but no ProofCarrier configured.");

            if (config.ProofMode == ProofCadenceMode.Continuous && config.DaMode == DaMode.None)
                errors.Add("OnDemand/Continuous proof requires DA (Federated or Public) for cold re-proving.");

            if (config.ProofMode == ProofCadenceMode.Periodic &&
                config.ProofCarrier == ProofCarrierMode.Inline &&
                config.AnchorGranularity > 1)
                errors.Add("Batched proofs with CarrierInline may exceed calldata limits.");

            if (config.AnchorGranularity <= 0 &&
                (config.ProofMode != ProofCadenceMode.Off || config.DaMode != DaMode.None))
                errors.Add("AnchorGranularity must be > 0 when ProofMode or DaMode is active.");

            return errors;
        }

        public static void ValidateOrThrow(AnchorPublicationConfig config)
        {
            var errors = Validate(config);
            if (errors.Count > 0)
                throw new InvalidOperationException(
                    $"Invalid anchor configuration: {string.Join("; ", errors)}");
        }
    }
}
