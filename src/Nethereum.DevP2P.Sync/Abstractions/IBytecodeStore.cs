namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface IBytecodeStore
    {
        byte[] Get(byte[] codeHash);
    }
}
