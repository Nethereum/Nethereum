using Nethereum.Merkle.Patricia.Storage;
namespace Nethereum.Merkle.Patricia.Nodes.Rlp
{
    public class NodeDecoder
    {
        private readonly NodeRlpDecoder _decoder = new NodeRlpDecoder();

        public Node Decode(HashNode reference, ITrieNodeStore store, bool decodeHashNodes)
        {
            return _decoder.Decode(reference, store, decodeHashNodes);
        }

        public Node DecodeFromRlpData(byte[] currentData, byte[] owner, byte[] path, bool decodeHashNodes, ITrieNodeStore store)
        {
            return _decoder.DecodeFromRlpData(currentData, owner, path, decodeHashNodes, store);
        }
    }
}
