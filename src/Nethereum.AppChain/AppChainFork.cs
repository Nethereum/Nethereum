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

        EthereumBinaryV1,

        /// <summary>
        /// Full EIP-7807 SSZ execution-block stack + binary state trie (EIP-7864) +
        /// Poseidon. Block hash is SHA256 via <c>hash_tree_root</c> per EIP-7807.
        /// Hash: Poseidon only.
        /// </summary>
        RoadmapSszV1
    }

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

    public static class AppChainForkHashResolver
    {
        public static IHashProvider Resolve(AppChainFork fork, BinaryHashMode? binaryHashMode = null)
        {
            switch (fork)
            {
                case AppChainFork.Ethereum:
                    if (binaryHashMode.HasValue)
                        throw new ArgumentException(
                            "AppChainFork.Ethereum does not accept a BinaryHashMode — Keccak is the only allowed hash.",
                            nameof(binaryHashMode));
                    return Sha3KeccackHashProvider.Instance;

                case AppChainFork.EthereumBinaryV1:
                    var mode = binaryHashMode ?? BinaryHashMode.Poseidon;
                    return mode switch
                    {
                        BinaryHashMode.Poseidon => new PoseidonHashProvider(),
                        BinaryHashMode.Keccak => Sha3KeccackHashProvider.Instance,
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

        public static bool UsesBinaryStateTrie(AppChainFork fork)
            => fork == AppChainFork.EthereumBinaryV1 || fork == AppChainFork.RoadmapSszV1;

        public static bool UsesSszBlockEncoding(AppChainFork fork)
            => fork == AppChainFork.RoadmapSszV1;
    }
}
