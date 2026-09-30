using Nethereum.ABI.ByteArrayConvertors;
using Nethereum.Merkle.StrategyOptions.PairingConcat;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle
{
    public class AbiStructMerkleTree<T> : MerkleTree<T>
    {
        public AbiStructMerkleTree() : base(Sha3KeccackHashProvider.Instance, new AbiStructEncoderPackedByteConvertor<T>(), PairingConcatType.Sorted)
        {
        }
    }

}
