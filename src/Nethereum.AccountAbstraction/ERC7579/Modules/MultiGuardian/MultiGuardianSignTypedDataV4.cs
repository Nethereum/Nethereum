using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountSigning;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian
{
    public sealed class MultiGuardianSignTypedDataV4 : IEthSignTypedDataV4
    {
        private readonly IReadOnlyList<EthECKey> _guardians;
        private readonly int _threshold;
        private readonly Eip712TypedDataSigner _typedDataSigner = new Eip712TypedDataSigner();

        public MultiGuardianSignTypedDataV4(IReadOnlyList<EthECKey> guardians, int threshold)
        {
            MultiGuardianSignatureBlobBuilder.ValidateGuardianSet(guardians, threshold);
            _guardians = guardians;
            _threshold = threshold;
        }

        public Task<string> SendRequestAsync(string jsonMessage, object id = null)
        {
            if (string.IsNullOrEmpty(jsonMessage))
                throw new ArgumentException("jsonMessage must not be null or empty", nameof(jsonMessage));

            var encodedData = _typedDataSigner.EncodeTypedData(jsonMessage);
            var userOpHash = Sha3Keccack.Current.CalculateHash(encodedData);
            var blob = MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(userOpHash, _guardians, _threshold);
            return Task.FromResult(blob.ToHex(true));
        }

        public RpcRequest BuildRequest(string message, object id = null) => throw new NotImplementedException();
    }
}
