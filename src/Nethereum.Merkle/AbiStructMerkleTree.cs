using Nethereum.Merkle.ByteConvertors;
using Nethereum.Merkle.HashProviders;
using Nethereum.Merkle.StrategyOptions.PairingConcat;

namespace Nethereum.Merkle
{
    public class AbiStructMerkleTree<T> : MerkleTree<T>
    {
        public AbiStructMerkleTree() : base(new Sha3KeccackHashProvider(), new AbiStructEncoderPackedByteConvertor<T>(), PairingConcatType.Sorted)
        {
        }
    }

}
