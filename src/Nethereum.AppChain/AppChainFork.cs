using System;
using Nethereum.Util.HashProviders;

namespace Nethereum.AppChain
{
    /// <summary>
    /// Encoding + state-model choice for an AppChain. Each value pins a combination
    /// of block encoding, state trie type, and allowed state-trie hash function(s).
    /// Operators pick one at genesis; upgrades flow through the AppChain hardfork
    /// ladder.
    ///
    /// Design reference: docs/superpowers/specs/2026-04-20-appchain-config-surface-A.md
    /// Plan + decisions: docs/superpowers/plans/2026-04-20-appchain-config-surface-A-plan.md
    /// </summary>
    public enum AppChainFork
    {
        /// <summary>
        /// Bit-for-bit mainnet. Patricia trie + Keccak + RLP everywhere. No
        /// roadmap EIPs in scope. Hash: Keccak only.
        /// </summary>
        Ethereum,

        /// <summary>
        /// Binary state trie (EIP-7864) with mainnet-compatible RLP wire format.
        /// Hash: Poseidon by default; Keccak available as an explicit compatibility
        /// variant via <see cref="BinaryHashMode"/>.
        /// </summary>
        EthereumBinaryV1,

        /// <summary>
        /// Full EIP-7807 SSZ execution-block stack + binary state trie (EIP-7864) +
        /// Poseidon. Block hash is SHA256 via <c>hash_tree_root</c> per EIP-7807.
        /// Hash: Poseidon only.
        /// </summary>
        RoadmapSszV1
    }

    /// <summary>
    /// State-trie hash choice for <see cref="AppChainFork.EthereumBinaryV1"/>.
    /// Other forks fix the hash and do not accept this knob.
    /// </summary>
    public enum BinaryHashMode
    {
        /// <summary>
        /// Poseidon state-trie hash. Proving-optimised (Zisk CSR 0x812 native).
        /// This is the default for <c>EthereumBinaryV1</c>.
        /// </summary>
        Poseidon,

        /// <summary>
        /// Keccak state-trie hash over the binary trie structure. Explicit compatibility
        /// option for operators who want mainnet-shape hashing on a binary-trie chain.
        /// Higher proving cost than Poseidon.
        /// </summary>
        Keccak
    }

    /// <summary>
    /// Resolves the allowed <see cref="IHashProvider"/> for a given
    /// <see cref="AppChainFork"/> + optional <see cref="BinaryHashMode"/>. Rejects
    /// any combination that violates the per-mode matrix recorded in the plan
    /// document's decision D-002 / validator rule 1 (D-010).
    /// </summary>
    public static class AppChainForkHashResolver
    {
        /// <summary>
        /// Returns the <see cref="IHashProvider"/> mandated by <paramref name="fork"/>.
        /// <paramref name="binaryHashMode"/> is only consulted when
        /// <paramref name="fork"/> is <see cref="AppChainFork.EthereumBinaryV1"/>; for
        /// other forks, passing a non-null value throws (the hash is fixed).
        /// </summary>
        public static IHashProvider Resolve(AppChainFork fork, BinaryHashMode? binaryHashMode = null)
        {
            switch (fork)
            {
                case AppChainFork.Ethereum:
                    if (binaryHashMode.HasValue)
                        throw new ArgumentException(
                            "AppChainFork.Ethereum does not accept a BinaryHashMode — Keccak is the only allowed hash.",
                            nameof(binaryHashMode));
                    return new Sha3KeccackHashProvider();

                case AppChainFork.EthereumBinaryV1:
                    var mode = binaryHashMode ?? BinaryHashMode.Poseidon;
                    return mode switch
                    {
                        BinaryHashMode.Poseidon => new PoseidonHashProvider(),
                        BinaryHashMode.Keccak => new Sha3KeccackHashProvider(),
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(binaryHashMode),
                            mode,
                            "Unknown BinaryHashMode.")
                    };

                case AppChainFork.RoadmapSszV1:
                    if (binaryHashMode.HasValue && binaryHashMode.Value != BinaryHashMode.Poseidon)
                        throw new ArgumentException(
                            $"AppChainFork.RoadmapSszV1 requires Poseidon; {binaryHashMode.Value} is not permitted.",
                            nameof(binaryHashMode));
                    return new PoseidonHashProvider();

                default:
                    throw new ArgumentOutOfRangeException(nameof(fork), fork, "Unknown AppChainFork.");
            }
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="fork"/> places account leaves in a
        /// binary trie (EIP-7864). The <see cref="AppChainFork.Ethereum"/> fork uses
        /// a classical Patricia trie and returns <c>false</c>.
        /// </summary>
        public static bool UsesBinaryStateTrie(AppChainFork fork)
            => fork == AppChainFork.EthereumBinaryV1 || fork == AppChainFork.RoadmapSszV1;

        /// <summary>
        /// Returns <c>true</c> if <paramref name="fork"/> uses SSZ block encoding
        /// and SHA256 block hashing per EIP-7807. Only
        /// <see cref="AppChainFork.RoadmapSszV1"/> does today.
        /// </summary>
        public static bool UsesSszBlockEncoding(AppChainFork fork)
            => fork == AppChainFork.RoadmapSszV1;
    }
}
