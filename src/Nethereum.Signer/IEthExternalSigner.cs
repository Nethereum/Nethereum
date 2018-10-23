using System.Threading.Tasks;
using Nethereum.Signer.Crypto;

namespace Nethereum.Signer
{
#if !DOTNET35

    public enum ExternalSignerTransactionFormat
    {
        RLP,
        Hash,
        Transaction
    }
    
#endif
}