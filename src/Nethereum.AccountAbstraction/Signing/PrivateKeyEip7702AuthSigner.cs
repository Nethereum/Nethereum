using System.Numerics;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.Signing
{
    public class PrivateKeyEip7702AuthSigner : IEip7702AuthSigner
    {
        private readonly EthECKey _key;

        public PrivateKeyEip7702AuthSigner(EthECKey key)
        {
            _key = key;
        }

        public Authorisation SignAuthorisation(BigInteger chainId, string delegateAddress, BigInteger accountNonce)
        {
            var authorisation = new Authorisation7702
            {
                ChainId = chainId,
                Address = delegateAddress,
                Nonce = accountNonce
            };
            var signedAuthorisation = new Authorisation7702Signer().SignAuthorisation(_key, authorisation);
            return signedAuthorisation.ToRPCAuthorisation();
        }
    }
}
