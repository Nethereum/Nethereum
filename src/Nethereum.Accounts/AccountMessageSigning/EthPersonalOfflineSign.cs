using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using System;
using System.Threading.Tasks;

namespace Nethereum.Accounts.AccountMessageSigning
{
    public class EthPersonalOfflineSign : IEthPersonalSign
    {
        private readonly EthECKey _ethECKey;
        private EthereumMessageSigner _ethereumMessageSigner;

        public EthPersonalOfflineSign(EthECKey ethECKey)
        {
            _ethECKey = ethECKey;
            _ethereumMessageSigner = new EthereumMessageSigner();
        }

        public Task<string> SendRequestAsync(byte[] value, object id = null)
        {
           return Task.FromResult(_ethereumMessageSigner.Sign(value, _ethECKey));
        }

        public Task<string> SendRequestAsync(HexUTF8String utf8Hex, object id = null)
        {
            return Task.FromResult(_ethereumMessageSigner.Sign(utf8Hex.HexValue.HexToByteArray(), _ethECKey));
        }

        public RpcRequest BuildRequest(HexUTF8String utf8Hex, object id = null)
        {
            throw new NotImplementedException();
        }

        public RpcRequest BuildRequest(byte[] value, object id = null)
        {
            throw new NotImplementedException();
        }
    }
}
