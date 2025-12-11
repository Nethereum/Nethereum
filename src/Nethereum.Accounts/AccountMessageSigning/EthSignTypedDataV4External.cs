using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using System;
using System.Threading.Tasks;

namespace Nethereum.Accounts.AccountMessageSigning
{
    public class EthSignTypedDataV4External : IEthSignTypedDataV4
    {
        private readonly IEthExternalSigner _ethExternalSigner;

        public EthSignTypedDataV4External(IEthExternalSigner ethExternalSigner)
        {
            _ethExternalSigner = ethExternalSigner;
        }


        public async Task<string> SendRequestAsync(string jsonMessage, object id = null)
        {
            return await _ethExternalSigner.SignTypedDataJsonAsync(jsonMessage).ConfigureAwait(false);
        }

        public RpcRequest BuildRequest(string message, object id = null)
        {
            throw new NotImplementedException();
        }
    }
}
