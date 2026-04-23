namespace Nethereum.Model
{
    /// <summary>
    /// Strategy for computing a block's hash from its header. Mainnet uses
    /// <c>keccak256(rlp_encode(header))</c>; EIP-7807 SSZ execution blocks use
    /// <c>hash_tree_root(header)</c> (SHA256-based). An AppChain picks one at
    /// genesis via <see cref="Nethereum.AppChain.AppChainFork"/>.
    /// </summary>
    public interface IBlockHashProvider
    {
        byte[] ComputeBlockHash(BlockHeader header);
    }
}
