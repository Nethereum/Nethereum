using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public sealed class InMemorySideBlockStore : ISideBlockStore
    {
        private readonly ConcurrentDictionary<string, SideBlock> _blocks = new();

        public void Put(
            byte[] hash,
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            List<AccountChanges>? declaredBlockAccessList)
        {
            _blocks[hash.ToHex()] = new SideBlock(hash, header, transactions, uncles, withdrawals, declaredBlockAccessList);
        }

        public bool TryGet(byte[] hash, out SideBlock block) => _blocks.TryGetValue(hash.ToHex(), out block!);

        public bool Contains(byte[] hash) => _blocks.ContainsKey(hash.ToHex());

        public void Remove(byte[] hash) => _blocks.TryRemove(hash.ToHex(), out _);

        public void PruneBelow(ulong blockNumber)
        {
            foreach (var entry in _blocks.ToArray())
            {
                if ((ulong)entry.Value.Header.BlockNumber < blockNumber)
                    _blocks.TryRemove(entry.Key, out _);
            }
        }
    }
}
