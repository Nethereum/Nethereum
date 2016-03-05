using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Web3.Sample
{
    public class PersonalTest
    {
        public async Task<string> Test()
        {

            var web3 = new Web3();
            var address = "0x12890d2cce102216644c59dae5baed380d84830c";
            var pass = "password";
            var result = await web3.Personal.UnlockAccount.SendRequestAsync(address, pass, new HexBigInteger(600));
            var newAccount = await web3.Personal.NewAccount.SendRequestAsync("password");
            var accounts = await web3.Personal.ListAccounts.SendRequestAsync();
            return "New account : " + newAccount + " all accounts: " + string.Join(",", accounts);
        }
    }

    
}