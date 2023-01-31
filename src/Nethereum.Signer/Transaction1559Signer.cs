using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.Signer
{
    public class Transaction1559Signer:TypeTransactionSigner<Transaction1559>
    {

#if !DOTNET35
        public Task SignExternallyAsync(IEthExternalSigner externalSigner, Transaction1559 transaction)
        {
            return externalSigner.SignAsync(transaction);
        }
#endif
    }
}