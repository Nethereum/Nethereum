using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using System;
using System.Threading.Tasks;

namespace Nethereum.Accounts.AccountMessageSigning
{
    public class EthSignTypedDataV4Offline : IEthSignTypedDataV4
    {
        private readonly EthECKey _ethECKey;
        private Eip712TypedDataSigner _typedDataSigner;

        public EthSignTypedDataV4Offline(EthECKey ethECKey)
        {
            _ethECKey = ethECKey;
            _typedDataSigner = new Eip712TypedDataSigner();
        }

       

        public Task<string> SendRequestAsync(string jsonMessage, object id = null)
        {
            return Task.FromResult(_typedDataSigner.SignTypedDataV4(jsonMessage, _ethECKey));
        }

        public RpcRequest BuildRequest(string message, object id = null)
        {
            throw new NotImplementedException();
        }
    }
}
