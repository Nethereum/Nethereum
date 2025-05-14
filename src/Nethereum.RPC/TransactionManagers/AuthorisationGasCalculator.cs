using Nethereum.RPC.Eth.DTOs;
using Nethereum.Model;
using System.Linq;

namespace Nethereum.RPC.TransactionManagers
{
    public static class AuthorisationGasCalculator
    {
        public static int CalculateGasForAuthorisationDelegation(params Authorisation[] authorisations)
        {
            var numberOfNew = authorisations.Count(x => x.Nonce.Value == 0);
            var numberOfExisting = authorisations.Count(x => x.Nonce.Value > 0);
            return CalculateGasForAuthorisationDelegation(numberOfNew, numberOfExisting);
        }

        public static int CalculateGasForAuthorisationDelegation(int numberOfNew, int numberOfExisting)
        {
            var gas = 0;
            if (numberOfNew > 0)
            {
                gas += (numberOfNew * Gas7702.PER_EMPTY_ACCOUNT_COST);
            }
            if (numberOfExisting > 0)
            {
                //it is the same as empty account.
                gas += (numberOfExisting * Gas7702.PER_EMPTY_ACCOUNT_COST);
            }
            return gas;
        }
    }
}