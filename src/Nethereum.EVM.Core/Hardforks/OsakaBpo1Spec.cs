using Nethereum.EVM.Gas;

namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// First Osaka BPO (Blob Parameter Only) fork, per EIP-7892. A BPO
    /// fork changes ONLY blob-schedule parameters and inherits every
    /// other rule slot from its parent fork. Wired here by cloning
    /// <see cref="OsakaSpec.Instance"/> via record <c>with</c> and
    /// overriding the intrinsic gas bundle to swap the blob-gas rule
    /// from <c>Eip7892BlobGasRule</c> (UPDATE_FRAC 8,346,193) to
    /// <c>Eip7892Bpo1BlobGasRule</c> (UPDATE_FRAC 11,684,671).
    ///
    /// <para>
    /// Mainnet activation: block 24,179,383, timestamp 1,767,747,671
    /// (2026-01-07 01:01:11 UTC). Verified by binary-searching the
    /// boundary on the Erigon canonical node — first block where
    /// <c>fake_exp(1, excess_blob_gas, 8_346_193)</c> stopped matching
    /// canonical <c>baseFeePerBlobGas</c>.
    /// </para>
    /// </summary>
    public static class OsakaBpo1Spec
    {
        public static readonly HardforkSpec Instance = OsakaSpec.Instance with
        {
            Name = HardforkName.OsakaBpo1,
            IntrinsicGas = IntrinsicGasRuleSets.OsakaBpo1,
        };
    }
}
