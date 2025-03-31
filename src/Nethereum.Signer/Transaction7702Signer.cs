using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.Signer
{
    public class Transaction7702Signer : TypeTransactionSigner<Transaction7702>
    {

#if !DOTNET35
        public Task SignExternallyAsync(IEthExternalSigner externalSigner, Transaction7702 transaction)
        {
            return externalSigner.SignAsync(transaction);
        }
#endif
    }

}