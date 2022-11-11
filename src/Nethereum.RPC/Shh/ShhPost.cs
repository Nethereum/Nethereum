using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Shh
{
    public class ShhPost : RpcRequestResponseHandler<string>, IShhPost
    {
        public ShhPost(IClient client) : base(client, ApiMethods.shh_post.ToString())
        {
        }

        public RpcRequest BuildRequest(MessageInput input, object id = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrEmpty(input.PubKey) && string.IsNullOrEmpty(input.SymKeyID)) throw new ArgumentNullException($"{nameof(input.SymKeyID)} Or {nameof(input.PubKey)}");
            return base.BuildRequest(id, input);
        }

        public Task<string> SendRequestAsync(MessageInput input, object id = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrEmpty(input.PubKey) && string.IsNullOrEmpty(input.SymKeyID)) throw new ArgumentNullException($"{nameof(input.SymKeyID)} Or {nameof(input.PubKey)}");
            return base.SendRequestAsync(id, input);
        }
    }
}
