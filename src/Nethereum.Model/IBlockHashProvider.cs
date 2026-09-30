namespace Nethereum.Model
{
    public interface IBlockHashProvider
    {
        byte[] ComputeBlockHash(BlockHeader header);

        bool HashesRlpEncodedHeader { get; }
    }
}
