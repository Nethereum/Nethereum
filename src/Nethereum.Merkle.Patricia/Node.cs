using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Patricia
{

    public abstract class Node
    {
        protected IHashProvider HashProvider { get; set; }
        public Node(IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
        }

        public abstract byte[] GetRLPEncodedData();
        
        public virtual byte[] GetHash()
        {
            return HashProvider.ComputeHash(GetRLPEncodedData());
        }
       
    }
}
