namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal readonly record struct AccountRoots(byte[] OldFlatRoot, byte[] NewTrieRoot);
}
