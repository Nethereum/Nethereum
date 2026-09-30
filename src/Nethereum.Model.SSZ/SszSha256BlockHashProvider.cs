namespace Nethereum.Model.SSZ
{
    public class SszSha256BlockHashProvider : IBlockHashProvider
    {
        public static SszSha256BlockHashProvider Instance { get; } = new SszSha256BlockHashProvider();

        /// <summary>EIP-7807 hashes the SSZ container, not an RLP encoding.</summary>
        public bool HashesRlpEncodedHeader => false;

        public byte[] ComputeBlockHash(BlockHeader header)
            => SszBlockHeaderEncoder.Current.BlockHash(header);
    }
}
