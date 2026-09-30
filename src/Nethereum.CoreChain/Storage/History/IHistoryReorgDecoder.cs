namespace Nethereum.CoreChain.Storage.History
{
    public interface IHistoryReorgDecoder
    {
        byte[] BlockHash(byte[] blockMetaValue);
        byte[] TxHash(byte[] txBodyValue);
    }
}
