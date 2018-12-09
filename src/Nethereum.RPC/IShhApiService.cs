using Nethereum.RPC.Shh;

namespace Nethereum.RPC
{
    public interface IShhApiService
    {
        IShhNewIdentity NewIdentity { get; }
        IShhVersion Version { get; }
    }
}