namespace Nethereum.Model.SSZ
{
    /// <summary>
    /// EIP-7807 block hash: <c>hash_tree_root(header)</c>, SHA256-based via
    /// <see cref="SszBlockHeaderEncoder.BlockHash"/>. Used by
    /// <see cref="Nethereum.AppChain.AppChainFork.RoadmapSszV1"/>.
    /// </summary>
    public class SszSha256BlockHashProvider : IBlockHashProvider
    {
        public static SszSha256BlockHashProvider Instance { get; } = new SszSha256BlockHashProvider();

        public byte[] ComputeBlockHash(BlockHeader header)
            => SszBlockHeaderEncoder.Current.BlockHash(header);
    }
}
