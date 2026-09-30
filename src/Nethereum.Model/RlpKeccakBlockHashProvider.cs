using Nethereum.Util;

namespace Nethereum.Model
{
    public class RlpKeccakBlockHashProvider : IBlockHashProvider
    {
        public static RlpKeccakBlockHashProvider Instance { get; } = new RlpKeccakBlockHashProvider();

        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public bool HashesRlpEncodedHeader => true;

        public byte[] ComputeBlockHash(BlockHeader header)
        {
            var encoded = BlockHeaderEncoder.Current.Encode(header);
            return _keccak.CalculateHash(encoded);
        }
    }
}
