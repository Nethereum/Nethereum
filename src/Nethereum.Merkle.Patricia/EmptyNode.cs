
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Patricia
{
    public class EmptyNode : Node
    {
        public EmptyNode() : base(new Sha3KeccackHashProvider())
        {

        }
        public EmptyNode(IHashProvider hashProvider) : base(hashProvider)
        {
        }

        public override byte[] GetRLPEncodedData()
        {
            return RLP.RLP.EncodeElement(new byte[0]);
        }
    }
}
