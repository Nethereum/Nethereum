using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Validation
{
    /// <summary>
    /// Offline <see cref="ICanonicalStateRootSource"/> backed by a hardcoded
    /// table of well-known Ethereum mainnet block hashes and state roots.
    /// Built from public block data (verifiable against any mainnet archive
    /// node or Etherscan), so its answers are unfalsifiable by a hostile RPC
    /// endpoint and require no network at lookup time.
    ///
    /// <para>
    /// Pair ahead of <c>RpcCanonicalSource</c> in a
    /// <see cref="CompositeCanonicalStateRootSource"/>: at the famous fork
    /// boundaries the local instance speaks first and an honest peer / RPC
    /// is no longer trusted unconditionally. For non-table heights this
    /// source reports no answer and the composite falls through to the
    /// next source.
    /// </para>
    ///
    /// <para>
    /// Pinned heights are limited to entries whose state root and block hash
    /// can be sourced directly from the public Ethereum mainnet record.
    /// Adding more checkpoints (e.g. every 1M blocks) is a pure data change:
    /// look up the block on a mainnet archive node, append the entry in
    /// <see cref="BuildDefaultTable"/>.
    /// </para>
    /// </summary>
    public sealed class MainnetKnownCheckpoints : ICanonicalStateRootSource
    {
        private readonly IReadOnlyDictionary<ulong, (byte[] StateRoot, byte[] BlockHash)> _table;

        public MainnetKnownCheckpoints()
        {
            _table = BuildDefaultTable();
        }

        /// <summary>
        /// Test / advanced-operator hook: supply a custom checkpoint table
        /// (e.g. a private chain's fork milestones). Production callers use
        /// the parameterless ctor.
        /// </summary>
        public MainnetKnownCheckpoints(IReadOnlyDictionary<ulong, (byte[] StateRoot, byte[] BlockHash)> table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            _table = table;
        }

        public string Name => "MainnetKnownCheckpoints";

        /// <summary>
        /// Number of hardcoded checkpoints in the active table — handy for
        /// startup banners ("loaded N pinned mainnet checkpoints").
        /// </summary>
        public int Count => _table.Count;

        /// <summary>
        /// Block numbers currently pinned. Stable order not guaranteed.
        /// </summary>
        public IEnumerable<ulong> PinnedBlockNumbers => _table.Keys;

        public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber,
            CancellationToken ct)
        {
            if (_table.TryGetValue(blockNumber, out var entry))
            {
                return Task.FromResult((entry.StateRoot, entry.BlockHash));
            }
            return Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((null, null));
        }

        // Hardcoded checkpoints have no notion of "latest" — they're a fixed
        // table of fork milestones. Composite layers above this skip it for
        // tip discovery and consult a real tip source (RPC, light client, anchor).
        public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) =>
            Task.FromResult<CanonicalTip>(null);

        private static Dictionary<ulong, (byte[] StateRoot, byte[] BlockHash)> BuildDefaultTable()
        {
            var t = new Dictionary<ulong, (byte[] StateRoot, byte[] BlockHash)>();

            // Block 1 — first miner reward (5 ETH to coinbase
            // 0x05a56e2d52c817161883f50c441c3228cfe54d9f). State root and block
            // hash are part of every mainnet client's hardcoded test fixtures.
            Add(t, 1,
                stateRoot: "0xd67e4d450343046425ae4271474353857ab860dbc0a1dde64b41b5cd3a532bf3",
                blockHash: "0x88e96d4537bea4d9c05d12549907b32561d3bf31f45aae734cdc119f13406cb6");

            // DAO fork (block 1,920,000). The block hash is the famous
            // post-fork canonical block hash that both clients and the
            // hard-fork wallet pinning code embed.
            Add(t, 1_920_000,
                stateRoot: "0xc5e389416116e3696cce82ec4533cce33efccb24ce245ae9546a4b8f0d5e9a75",
                blockHash: "0x4985f5ca3d2afbec36529aa96f74de3cc10a2a4a6c44f2157a57d2c6059a11bb");

            // Add additional checkpoints (Tangerine Whistle, Spurious Dragon,
            // Byzantium, Constantinople, Istanbul, Berlin, London, Paris,
            // Shanghai, Cancun, …) by appending entries here after sourcing
            // the canonical block hash + state root from an archive node.
            // Schema is intentionally simple — pure data change, no API
            // change. Tests in MainnetKnownCheckpointsTests cover the lookup
            // contract for any populated entry.

            return t;
        }

        private static void Add(
            Dictionary<ulong, (byte[] StateRoot, byte[] BlockHash)> table,
            ulong blockNumber,
            string stateRoot,
            string blockHash)
        {
            table[blockNumber] = (stateRoot.HexToByteArray(), blockHash.HexToByteArray());
        }
    }
}
