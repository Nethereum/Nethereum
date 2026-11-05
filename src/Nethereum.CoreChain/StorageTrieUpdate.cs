using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.CoreChain
{
    internal sealed class StorageTrieUpdate
    {
        private readonly PatriciaTrie _trie;
        private readonly IReadOnlyList<KeyValuePair<BigInteger, byte[]>> _slots;

        public StorageTrieUpdate(PatriciaTrie trie, IReadOnlyList<KeyValuePair<BigInteger, byte[]>> slots)
        {
            _trie = trie;
            _slots = slots;
        }

        public int SlotsUpdated { get; private set; }

        public int SlotsDeleted { get; private set; }

        public bool Emptied { get; private set; }

        public byte[] Root { get; private set; }

        public void Apply()
        {
            foreach (var slot in _slots)
            {
                var hashedSlot = StateKeys.StorageSlotKey(slot.Key);
                var value = slot.Value;
                if (value == null || value.Length == 0 || value.All(b => b == 0))
                {
                    _trie.Delete(hashedSlot);
                    SlotsDeleted++;
                }
                else
                {
                    _trie.Put(hashedSlot, RLP.RLP.EncodeElement(StateRootAccountRules.TrimLeadingZeros(value)));
                    SlotsUpdated++;
                }
            }

            Emptied = _trie.Root is EmptyNode;
            Root = Emptied ? null : _trie.Root.GetHash();
        }
    }
}
