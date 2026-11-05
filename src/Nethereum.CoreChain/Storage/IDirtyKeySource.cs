namespace Nethereum.CoreChain.Storage
{
    public interface IDirtyKeySource
    {
        bool TryAttachDirtyKeyObserver(IDirtyKeyObserver observer);

        void DetachDirtyKeyObserver(IDirtyKeyObserver observer);
    }
}
