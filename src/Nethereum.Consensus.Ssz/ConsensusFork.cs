namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// Consensus-layer hard forks affecting LightClient* SSZ container shapes,
    /// merkleization indices, and proof verification depths.
    /// Ordering is chronological — comparisons like <c>fork &gt;= Capella</c> are spec-meaningful.
    /// Activation epochs and fork versions are defined in
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
    /// configs/mainnet.yaml</see>.
    /// </summary>
    public enum ConsensusFork
    {
        /// <summary>
        /// Phase0 (genesis). <c>GENESIS_FORK_VERSION = 0x00000000</c> at
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see> line 30 (epoch 0). Beacon-chain definition:
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/phase0/beacon-chain.md">
        /// specs/phase0/beacon-chain.md</see>.
        /// </summary>
        Phase0 = 0,

        /// <summary>
        /// Altair. <c>ALTAIR_FORK_VERSION = 0x01000000</c>, <c>ALTAIR_FORK_EPOCH = 74240</c> at
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see> lines 41–42. Light client introduced:
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/beacon-chain.md">
        /// specs/altair/beacon-chain.md</see>.
        /// </summary>
        Altair = 1,

        /// <summary>
        /// Bellatrix (The Merge). <c>BELLATRIX_FORK_VERSION = 0x02000000</c>,
        /// <c>BELLATRIX_FORK_EPOCH = 144896</c> at
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see> lines 44–45. Spec:
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/bellatrix/beacon-chain.md">
        /// specs/bellatrix/beacon-chain.md</see>.
        /// </summary>
        Bellatrix = 2,

        /// <summary>
        /// Capella. <c>CAPELLA_FORK_VERSION = 0x03000000</c>, <c>CAPELLA_FORK_EPOCH = 194048</c> at
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see> lines 47–48. Spec:
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/capella/beacon-chain.md">
        /// specs/capella/beacon-chain.md</see>.
        /// </summary>
        Capella = 3,

        /// <summary>
        /// Deneb. <c>DENEB_FORK_VERSION = 0x04000000</c>, <c>DENEB_FORK_EPOCH = 269568</c> at
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see> lines 50–51. Spec:
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/deneb/beacon-chain.md">
        /// specs/deneb/beacon-chain.md</see>.
        /// </summary>
        Deneb = 4,

        /// <summary>
        /// Electra. <c>ELECTRA_FORK_VERSION = 0x05000000</c>, <c>ELECTRA_FORK_EPOCH = 364032</c> at
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see> lines 53–54. Spec:
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/beacon-chain.md">
        /// specs/electra/beacon-chain.md</see>.
        /// </summary>
        Electra = 5,

        /// <summary>
        /// Fulu. <c>FULU_FORK_VERSION = 0x06000000</c>, <c>FULU_FORK_EPOCH = 411392</c> at
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see> lines 56–57. Spec:
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/fulu/beacon-chain.md">
        /// specs/fulu/beacon-chain.md</see>.
        /// </summary>
        Fulu = 6,

        /// <summary>
        /// Gloas. <c>GLOAS_FORK_VERSION = 0x07000000</c>,
        /// <c>GLOAS_FORK_EPOCH = 18446744073709551615</c> (<c>FAR_FUTURE_EPOCH</c>) at
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
        /// configs/mainnet.yaml</see> lines 59–60. Not yet scheduled on mainnet; resolving a
        /// post-Fulu future slot to Gloas via <see cref="ChainSpec"/> throws until the spec
        /// assigns an activation epoch.
        /// </summary>
        Gloas = 7
    }
}
