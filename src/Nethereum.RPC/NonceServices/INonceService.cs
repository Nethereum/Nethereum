using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.NonceServices
{
    public interface INonceService
    {
        IClient Client { get; set; }
        Task<HexBigInteger> GetNextNonceAsync();
        Task ResetNonceAsync();
    }
}