using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian
{
    public sealed class MultiGuardianPersonalSign : IEthPersonalSign
    {
        private readonly IReadOnlyList<EthECKey> _guardians;
        private readonly int _threshold;
        private readonly EthereumMessageSigner _ethereumMessageSigner = new EthereumMessageSigner();

        public MultiGuardianPersonalSign(IReadOnlyList<EthECKey> guardians, int threshold)
        {
            MultiGuardianSignatureBlobBuilder.ValidateGuardianSet(guardians, threshold);
            _guardians = guardians;
            _threshold = threshold;
        }

        public Task<string> SendRequestAsync(byte[] value, object id = null)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var ethSignedHash = _ethereumMessageSigner.HashPrefixedMessage(value);
            var blob = MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(ethSignedHash, _guardians, _threshold);
            return Task.FromResult(blob.ToHex(true));
        }

        public Task<string> SendRequestAsync(HexUTF8String utf8Hex, object id = null)
        {
            if (utf8Hex == null)
                throw new ArgumentNullException(nameof(utf8Hex));

            return SendRequestAsync(utf8Hex.HexValue.HexToByteArray(), id);
        }

        public RpcRequest BuildRequest(byte[] value, object id = null) => throw new NotImplementedException();

        public RpcRequest BuildRequest(HexUTF8String utf8Hex, object id = null) => throw new NotImplementedException();
    }
}
