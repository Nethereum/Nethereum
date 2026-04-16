using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.EVM.Core.Tests
{
    public static class TestTransactionHelper
    {
        private static readonly string DEFAULT_KEY = "0x45a915e4d060149eb4365960e6a7a45f334393093061116b197e3240065ff2d8";

        public static BlockWitnessTransaction CreateSignedTransfer(
            string to,
            EvmUInt256 value,
            EvmUInt256 nonce,
            EvmUInt256 gasPrice,
            EvmUInt256 gasLimit,
            string privateKey = null)
        {
            privateKey = privateKey ?? DEFAULT_KEY;
            var signer = new LegacyTransactionSigner();
            var signedHex = signer.SignTransaction(
                privateKey,
                to,
                (System.Numerics.BigInteger)value,
                (System.Numerics.BigInteger)nonce,
                (System.Numerics.BigInteger)gasPrice,
                (System.Numerics.BigInteger)gasLimit);

            var key = new EthECKey(privateKey);
            return new BlockWitnessTransaction
            {
                From = key.GetPublicAddress(),
                RlpEncoded = signedHex.HexToByteArray()
            };
        }

        public static BlockWitnessTransaction CreateSignedContractCall(
            string to,
            byte[] data,
            EvmUInt256 value,
            EvmUInt256 nonce,
            EvmUInt256 gasPrice,
            EvmUInt256 gasLimit,
            string privateKey = null)
        {
            privateKey = privateKey ?? DEFAULT_KEY;
            var signer = new LegacyTransactionSigner();
            var signedHex = signer.SignTransaction(
                privateKey,
                to,
                (System.Numerics.BigInteger)value,
                (System.Numerics.BigInteger)nonce,
                (System.Numerics.BigInteger)gasPrice,
                (System.Numerics.BigInteger)gasLimit,
                data.ToHex(true));

            var key = new EthECKey(privateKey);
            return new BlockWitnessTransaction
            {
                From = key.GetPublicAddress(),
                RlpEncoded = signedHex.HexToByteArray()
            };
        }

        public static string GetDefaultSenderAddress()
        {
            return new EthECKey(DEFAULT_KEY).GetPublicAddress();
        }
    }
}
