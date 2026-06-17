namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Key-value store for contract bytecode keyed by codeHash = keccak256(code).
    /// Used by the snap/1 GetByteCodes handler and by AppChain follower bootstrap.
    /// </summary>
    public interface IBytecodeStore
    {
        byte[] Get(byte[] codeHash);
    }
}
