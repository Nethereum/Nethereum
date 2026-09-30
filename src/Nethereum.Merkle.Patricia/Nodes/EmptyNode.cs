
using Nethereum.Util.HashProviders;

using Nethereum.Merkle.Patricia.Nodes.Rlp;
namespace Nethereum.Merkle.Patricia.Nodes
{
    public class EmptyNode : Node
    {
        public static readonly EmptyNode Instance = new EmptyNode();

        public EmptyNode() : base(Sha3KeccackHashProvider.Instance)
        {

        }
        public EmptyNode(IHashProvider hashProvider) : base(hashProvider)
        {
        }

        protected override byte[] EncodeCore() => EmptyNodeRlpSerializer.Encode(this);
    }
}
