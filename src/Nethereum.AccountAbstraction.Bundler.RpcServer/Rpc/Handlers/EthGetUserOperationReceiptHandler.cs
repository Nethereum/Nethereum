using System.Collections.Generic;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class EthGetUserOperationReceiptHandler : RpcHandlerBase
    {
        private readonly IBundlerService _bundler;

        public EthGetUserOperationReceiptHandler(IBundlerService bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "eth_getUserOperationReceipt";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var userOpHash = GetParam<string>(request, 0);

                if (string.IsNullOrEmpty(userOpHash))
                    throw RpcException.InvalidParams("Missing/invalid userOpHash");

                var receipt = await _bundler.GetUserOperationReceiptAsync(userOpHash);

                if (receipt == null)
                    return Success(request.Id, null);

                var result = new Dictionary<string, object>
                {
                    ["userOpHash"] = receipt.UserOpHash,
                    ["entryPoint"] = receipt.EntryPoint,
                    ["sender"] = receipt.Sender,
                    ["nonce"] = receipt.Nonce?.HexValue ?? "0x0",
                    ["paymaster"] = receipt.Paymaster,
                    ["actualGasCost"] = receipt.ActualGasCost?.HexValue ?? "0x0",
                    ["actualGasUsed"] = receipt.ActualGasUsed?.HexValue ?? "0x0",
                    ["success"] = receipt.Success,
                    ["logs"] = receipt.Logs ?? new List<Nethereum.RPC.Eth.DTOs.FilterLog>(),
                    // ERC-4337: receipt.receipt is the FULL transaction receipt for the bundle
                    // (not just this userOp), so it must carry the same fields as eth_getTransactionReceipt.
                    ["receipt"] = receipt.Receipt != null ? new
                    {
                        transactionHash = receipt.Receipt.TransactionHash,
                        transactionIndex = receipt.Receipt.TransactionIndex?.HexValue,
                        blockHash = receipt.Receipt.BlockHash,
                        blockNumber = receipt.Receipt.BlockNumber?.HexValue,
                        from = receipt.Receipt.From,
                        to = receipt.Receipt.To,
                        cumulativeGasUsed = receipt.Receipt.CumulativeGasUsed?.HexValue,
                        gasUsed = receipt.Receipt.GasUsed?.HexValue,
                        contractAddress = receipt.Receipt.ContractAddress,
                        status = receipt.Receipt.Status?.HexValue,
                        logs = receipt.Receipt.Logs ?? Array.Empty<Nethereum.RPC.Eth.DTOs.FilterLog>(),
                        logsBloom = receipt.Receipt.LogsBloom,
                        effectiveGasPrice = receipt.Receipt.EffectiveGasPrice?.HexValue
                    } : null
                };

                if (receipt.Reason != null)
                {
                    result["reason"] = receipt.Reason;
                }

                return Success(request.Id, result);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}");
            }
        }
    }
}

