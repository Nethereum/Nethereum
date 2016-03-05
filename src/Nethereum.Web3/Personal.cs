using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.RPC.Personal;

namespace Nethereum.Web3
{
    public class Personal: RpcClientWrapper{

        public PersonalListAccounts ListAccounts { get; private set; }
        public PersonalNewAccount NewAccount { get; private set; }
        public PersonalUnlockAccount UnlockAccount { get; private set; }

        public Personal(RpcClient client) : base(client)
        {
            ListAccounts = new PersonalListAccounts(client);
            NewAccount = new PersonalNewAccount(client);
            UnlockAccount = new PersonalUnlockAccount(client);

        }
    }

}
