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
        private Eip712TypedDataSigner _typedDataSigner;

        public EthSignTypedDataV4External(IEthExternalSigner ethExternalSigner)
        {
            _ethExternalSigner = ethExternalSigner;
            _typedDataSigner = new Eip712TypedDataSigner();
        }


        public async Task<string> SendRequestAsync(string jsonMessage, object id = null)
        {
            var encodedData = _typedDataSigner.EncodeTypedData(jsonMessage);
            var signature = await _ethExternalSigner.SignAsync(Sha3Keccack.Current.CalculateHash(encodedData)).ConfigureAwait(false);
            return EthECDSASignature.CreateStringSignature(signature);
        }

        public RpcRequest BuildRequest(string message, object id = null)
        {
            throw new NotImplementedException();
        }
    }
}
