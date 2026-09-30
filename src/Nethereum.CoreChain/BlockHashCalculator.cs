using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public static class BlockHashCalculator
    {
        private static readonly Sha3Keccack _keccak = new Sha3Keccack();

        public static byte[] ForFork(BlockHeader header, HardforkName fork)
            => _keccak.CalculateHash(BlockHeaderCodecs.ForFork(fork).Encode(header));

        public static byte[] ForFork(BlockHeader header, HardforkName fork, IBlockHashProvider hashProvider)
            => hashProvider.HashesRlpEncodedHeader
                ? ForFork(header, fork)
                : hashProvider.ComputeBlockHash(header);

        public static byte[] ForHeader(BlockHeader header)
            => RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);
    }
}
