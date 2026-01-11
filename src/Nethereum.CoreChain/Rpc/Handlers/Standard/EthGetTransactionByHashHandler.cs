using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetTransactionByHashHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getTransactionByHash.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var hashHex = GetParam<string>(request, 0);
            var hash = hashHex.HexToByteArray();

            var signedTx = await context.Node.GetTransactionByHashAsync(hash);
            if (signedTx == null)
            {
                return Success(request.Id, null);
            }

            var location = await context.Node.Transactions.GetLocationAsync(hash);
            var transaction = ConvertToTransactionDto(signedTx, location);

            return Success(request.Id, transaction);
        }

        private Transaction ConvertToTransactionDto(ISignedTransaction signedTx, Storage.TransactionLocation location)
        {
            var tx = new Transaction
            {
                TransactionHash = signedTx.Hash?.ToHex(true),
                TransactionIndex = location != null ? new HexBigInteger(location.TransactionIndex) : null,
                BlockHash = location?.BlockHash?.ToHex(true),
                BlockNumber = location != null ? new HexBigInteger(location.BlockNumber) : null,
                From = GetSenderAddress(signedTx),
                R = signedTx.Signature?.R?.ToHex(true),
                S = signedTx.Signature?.S?.ToHex(true),
                V = signedTx.Signature?.V?.ToHex(true)
            };

            switch (signedTx)
            {
                case LegacyTransaction legacy:
                    tx.Type = new HexBigInteger(0);
                    tx.To = legacy.ReceiveAddress?.ToHex(true);
                    tx.Gas = new HexBigInteger(legacy.GasLimit.ToBigIntegerFromRLPDecoded());
                    tx.GasPrice = new HexBigInteger(legacy.GasPrice.ToBigIntegerFromRLPDecoded());
                    tx.Value = new HexBigInteger(legacy.Value.ToBigIntegerFromRLPDecoded());
                    tx.Nonce = new HexBigInteger(legacy.Nonce.ToBigIntegerFromRLPDecoded());
                    tx.Input = legacy.Data?.ToHex(true) ?? "0x";
                    break;

                case LegacyTransactionChainId legacyChainId:
                    tx.Type = new HexBigInteger(0);
                    tx.To = legacyChainId.ReceiveAddress?.ToHex(true);
                    tx.Gas = new HexBigInteger(legacyChainId.GasLimit.ToBigIntegerFromRLPDecoded());
                    tx.GasPrice = new HexBigInteger(legacyChainId.GasPrice.ToBigIntegerFromRLPDecoded());
                    tx.Value = new HexBigInteger(legacyChainId.Value.ToBigIntegerFromRLPDecoded());
                    tx.Nonce = new HexBigInteger(legacyChainId.Nonce.ToBigIntegerFromRLPDecoded());
                    tx.Input = legacyChainId.Data?.ToHex(true) ?? "0x";
                    break;

                case Transaction1559 eip1559:
                    tx.Type = new HexBigInteger(2);
                    tx.To = eip1559.ReceiverAddress;
                    tx.Gas = new HexBigInteger(eip1559.GasLimit ?? 0);
                    tx.MaxFeePerGas = new HexBigInteger(eip1559.MaxFeePerGas ?? 0);
                    tx.MaxPriorityFeePerGas = new HexBigInteger(eip1559.MaxPriorityFeePerGas ?? 0);
                    tx.Value = new HexBigInteger(eip1559.Amount ?? 0);
                    tx.Nonce = new HexBigInteger(eip1559.Nonce ?? 0);
                    tx.Input = eip1559.Data ?? "0x";
                    break;

                case Transaction2930 eip2930:
                    tx.Type = new HexBigInteger(1);
                    tx.To = eip2930.ReceiverAddress;
                    tx.Gas = new HexBigInteger(eip2930.GasLimit ?? 0);
                    tx.GasPrice = new HexBigInteger(eip2930.GasPrice ?? 0);
                    tx.Value = new HexBigInteger(eip2930.Amount ?? 0);
                    tx.Nonce = new HexBigInteger(eip2930.Nonce ?? 0);
                    tx.Input = eip2930.Data ?? "0x";
                    break;
            }

            return tx;
        }

        private static string GetSenderAddress(ISignedTransaction tx)
        {
            try
            {
                var key = EthECKeyBuilderFromSignedTransaction.GetEthECKey(tx);
                return key?.GetPublicAddress();
            }
            catch
            {
                return null;
            }
        }
    }
}
