using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.RPC.Shh.MessageFilter
{
    public interface IShhNewMessageFilter
    {
        RpcRequest BuildRequest(MessageFilterInput input, object id = null);
        Task<string> SendRequestAsync(MessageFilterInput input, object id = null);
    }

    public class ShhNewMessageFilter : RpcRequestResponseHandler<string>, IShhNewMessageFilter
    {
        public ShhNewMessageFilter(IClient client) : base(client, ApiMethods.shh_newMessageFilter.ToString())
        {
        }

        public RpcRequest BuildRequest(MessageFilterInput input, object id = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrEmpty(input.PrivateKeyID) && string.IsNullOrEmpty(input.SymKeyID)) throw new ArgumentNullException($"{nameof(input.SymKeyID)} Or {nameof(input.PrivateKeyID)}");
            return base.BuildRequest(id, input);
        }

        public Task<string> SendRequestAsync(MessageFilterInput input, object id = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrEmpty(input.PrivateKeyID) && string.IsNullOrEmpty(input.SymKeyID)) throw new ArgumentNullException($"{nameof(input.SymKeyID)} Or {nameof(input.PrivateKeyID)}");
            return base.SendRequestAsync(id, input);
        }
    }
}
