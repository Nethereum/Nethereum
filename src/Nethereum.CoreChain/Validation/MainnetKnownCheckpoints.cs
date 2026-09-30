using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Validation
{
    public sealed class MainnetKnownCheckpoints : ICanonicalStateRootSource
    {
        private readonly IReadOnlyDictionary<ulong, (byte[] StateRoot, byte[] BlockHash)> _table;

        public MainnetKnownCheckpoints()
        {
            _table = BuildDefaultTable();
        }

        public MainnetKnownCheckpoints(IReadOnlyDictionary<ulong, (byte[] StateRoot, byte[] BlockHash)> table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            _table = table;
        }

        public string Name => "MainnetKnownCheckpoints";

        public int Count => _table.Count;

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

        public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) =>
            Task.FromResult<CanonicalTip>(null);

        private static Dictionary<ulong, (byte[] StateRoot, byte[] BlockHash)> BuildDefaultTable()
        {
            var t = new Dictionary<ulong, (byte[] StateRoot, byte[] BlockHash)>();

            Add(t, 1,
                stateRoot: "0xd67e4d450343046425ae4271474353857ab860dbc0a1dde64b41b5cd3a532bf3",
                blockHash: "0x88e96d4537bea4d9c05d12549907b32561d3bf31f45aae734cdc119f13406cb6");

            Add(t, 1_920_000,
                stateRoot: "0xc5e389416116e3696cce82ec4533cce33efccb24ce245ae9546a4b8f0d5e9a75",
                blockHash: "0x4985f5ca3d2afbec36529aa96f74de3cc10a2a4a6c44f2157a57d2c6059a11bb");


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
