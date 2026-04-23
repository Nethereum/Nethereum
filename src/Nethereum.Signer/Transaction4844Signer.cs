using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.Signer
{
    public class Transaction4844Signer : TypeTransactionSigner<Transaction4844>
    {
#if !DOTNET35
        public Task SignExternallyAsync(IEthExternalSigner externalSigner, Transaction4844 transaction)
        {
            return externalSigner.SignAsync(transaction);
        }
#endif
    }
}
