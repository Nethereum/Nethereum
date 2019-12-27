using Nethereum.RPC.Shh;

namespace Nethereum.RPC
{
    public interface IShhApiService
    {
        IShhKeyPair KeyPair { get; } 
        IShhVersion Version { get; }
    }
}