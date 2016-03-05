using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Nethereum.Hex.HexTypes;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Personal
{

    ///<Summary>
       /// personal_unlockAccount
/// 
/// Unlock an account
/// 
/// Parameters
/// 
/// string, address of the account to unlock
/// 
/// string, passphrase of the account to unlock (optional in console, user will be prompted)
/// 
/// integer, unlock account for duration seconds (optional)
/// 
/// Return
/// 
/// boolean indication if the account was unlocked
/// 
/// Example
/// 
/// personal.unlockAccount(eth.coinbase, "mypasswd", 300)
/// 
///     
    ///</Summary>
    public class PersonalUnlockAccount : RpcRequestResponseHandler<bool>
        {
            public PersonalUnlockAccount(RpcClient client) : base(client,ApiMethods.personal_unlockAccount.ToString()) { }

            public async Task<bool> SendRequestAsync(string address, string passPhrase, HexBigInteger durationInSeconds, object id = null)
            {
                return await base.SendRequestAsync(id, address, passPhrase, durationInSeconds);
            }
            public RpcRequest BuildRequest(string address, string passPhrase, HexBigInteger durationInSeconds, object id = null)
            {
                return base.BuildRequest(id, address, passPhrase, durationInSeconds);
            }
        }

    }

