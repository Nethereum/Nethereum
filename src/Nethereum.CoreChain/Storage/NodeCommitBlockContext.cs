namespace Nethereum.CoreChain.Storage
{
    public interface INodeCommitBlockSource
    {
        ulong? CurrentBlock { get; }
    }

    public sealed class NodeCommitBlockContext : INodeCommitBlockSource
    {
        public ulong? CurrentBlock { get; private set; }

        public void Arm(ulong block) => CurrentBlock = block;

        public void Clear() => CurrentBlock = null;
    }
}
