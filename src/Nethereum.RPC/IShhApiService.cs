using Nethereum.RPC.Shh;

namespace Nethereum.RPC
{
    public interface IShhApiService
    {
        IShhNewKeyPair NewKeyPair { get; }
        //IShhAddPrivateKey AddPrivateKey { get; }
        IShhVersion Version { get; }
    }
}