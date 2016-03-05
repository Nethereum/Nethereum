using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.Personal
{

    ///<Summary>
       /// 
/// Create a new account
/// 
/// Parameters
/// 
/// string, passphrase to protect the account
/// 
/// Return
/// 
/// string address of the new account
/// 
/// Example
/// 
/// personal.newAccount("mypasswd")    
    ///</Summary>
    public class PersonalNewAccount : RpcRequestResponseHandler<string>
        {
            public PersonalNewAccount(RpcClient client) : base(client,ApiMethods.personal_newAccount.ToString()) { }

            public async Task<string> SendRequestAsync(string passPhrase, object id = null)
            {
                return await base.SendRequestAsync(id, passPhrase);
            }
            public RpcRequest BuildRequest(string passPhrase, object id = null)
            {
                return base.BuildRequest(id, passPhrase);
            }
        }

    }

