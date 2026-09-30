using System.Numerics;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction.Signing
{
    public interface IEip7702AuthSigner
    {
        Authorisation SignAuthorisation(BigInteger chainId, string delegateAddress, BigInteger accountNonce);
    }
}
