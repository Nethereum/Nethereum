using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Mappers
{
    public static class AccountRPCMapper
    {
        public static Account ToAccount(this AccountProof accountProof)
        {
            var account = new Account()
            {
                Balance = accountProof.Balance,
                CodeHash = accountProof.CodeHash.HexToByteArray(),
                Nonce = accountProof.Nonce,
                StateRoot = accountProof.StorageHash.HexToByteArray()
            };

            return account;
        }
    }
}
