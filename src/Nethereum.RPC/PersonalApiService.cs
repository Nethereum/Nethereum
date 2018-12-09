using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Personal;

namespace Nethereum.RPC
{
    public class PersonalApiService : RpcClientWrapper, IPersonalApiService
    {
        public PersonalApiService(IClient client) : base(client)
        {
            ListAccounts = new PersonalListAccounts(client);
            NewAccount = new PersonalNewAccount(client);
            UnlockAccount = new PersonalUnlockAccount(client);
            LockAccount = new PersonalLockAccount(client);
            SignAndSendTransaction = new PersonalSignAndSendTransaction(client);
        }

        public IPersonalListAccounts ListAccounts { get; private set; }
        public IPersonalNewAccount NewAccount { get; private set; }
        public IPersonalUnlockAccount UnlockAccount { get; private set; }
        public IPersonalLockAccount LockAccount { get; private set; }
        public IPersonalSignAndSendTransaction SignAndSendTransaction { get; private set; }
    }
}