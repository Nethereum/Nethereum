using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc
{
    public static class RpcTransactionAddress
    {
        public static string Normalize(string address)
            => string.IsNullOrEmpty(address) || address == "0x"
                ? null
                : address.ConvertToValid20ByteAddressLowerCase();

        public static string ResolveReceiver(ISignedTransaction tx) => Normalize(tx?.GetReceiverAddress());

        public static string ResolveSender(ISignedTransaction tx)
        {
            try
            {
                if (tx?.Signature == null) return null;
                var key = EthECKeyBuilderFromSignedTransaction.GetEthECKey(tx);
                return Normalize(key?.GetPublicAddress());
            }
            catch
            {
                return null;
            }
        }
    }
}
