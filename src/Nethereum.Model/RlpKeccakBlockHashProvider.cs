using Nethereum.Util;

namespace Nethereum.Model
{
    /// <summary>
    /// Mainnet-compatible block hash: <c>keccak256(rlp_encode(header))</c>.
    /// Used by <see cref="Nethereum.AppChain.AppChainFork.Ethereum"/> and
    /// <see cref="Nethereum.AppChain.AppChainFork.EthereumBinaryV1"/>.
    /// </summary>
    public class RlpKeccakBlockHashProvider : IBlockHashProvider
    {
        public static RlpKeccakBlockHashProvider Instance { get; } = new RlpKeccakBlockHashProvider();

        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public byte[] ComputeBlockHash(BlockHeader header)
        {
            var encoded = BlockHeaderEncoder.Current.Encode(header);
            return _keccak.CalculateHash(encoded);
        }
    }
}
