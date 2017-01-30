using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Web3.Tests
{
    public class PersonalTest
    {
        public async Task<string> Test()
        {

            var web3 = new Web3("http://192.168.2.22:8545");
            var address = "0x63c2ee74201b99de5e76198a7b2e6540bca83347";
            var pass = "password";
            var result = await web3.Personal.UnlockAccount.SendRequestAsync(address, pass, 600);
            var newAccount = await web3.Personal.NewAccount.SendRequestAsync("password");
            var accounts = await web3.Personal.ListAccounts.SendRequestAsync();
            return "New account : " + newAccount + " all accounts: " + string.Join(",", accounts);
        }
    }

    
}