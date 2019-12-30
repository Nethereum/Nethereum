using Nethereum.Signer;
using Nethereum.Web3.Accounts;

namespace Nethereum.Quorum
{
    public class QuorumAccount : Account
    {

        public QuorumAccount(EthECKey key):base(key)
        {
            
        }

        public QuorumAccount(string privateKey):base(privateKey)
        {
       
        }

        public QuorumAccount(byte[] privateKey):base(privateKey)
        {
         
        }

        protected override void InitialiseDefaultTransactionManager()
        {
            TransactionManager = new QuorumTransactionManager(null, null, this);
        }
    }
}
