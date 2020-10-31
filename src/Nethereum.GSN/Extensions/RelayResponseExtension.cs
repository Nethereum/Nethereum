using Nethereum.GSN.Models;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;

namespace Nethereum.GSN.Extensions
{
    public static class RelayResponseExtension
    {
        public static Transaction ToTransaction(this RelayResponse response)
        {
            var tx = new Transaction(
               response.To,
               response.Value.Value,
               response.Nonce.Value,
               response.GasPrice.Value,
               response.Gas.Value,
               response.Input);

            tx.SetSignature(new EthECDSASignature(
                new Org.BouncyCastle.Math.BigInteger(response.R.RemoveHexPrefix(), 16),
                new Org.BouncyCastle.Math.BigInteger(response.S.RemoveHexPrefix(), 16),
                response.V.HexToByteArray()));

            return tx;
        }
    }
}
