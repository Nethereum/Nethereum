using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.IpcClient;

namespace Nethereum.Web3.Ipc.Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine(Test().Result);
            Console.WriteLine(new EventFilterWith2Topics().Test().Result);

            Console.ReadLine();

        }

        public static async Task<string> Test()
        {
            var client = new IpcClient("./geth.ipc");
            var web3 = new Web3(client);
            var address = "0x12890d2cce102216644c59dae5baed380d84830c";
            var pass = "password";
            var result = await web3.Personal.UnlockAccount.SendRequestAsync(address, pass, new HexBigInteger(600));
            var newAccount = await web3.Personal.NewAccount.SendRequestAsync("password");
            var accounts = await web3.Personal.ListAccounts.SendRequestAsync();
            return "New account : " + newAccount + " all accounts: " + string.Join(",", accounts);
        }
    }
}
