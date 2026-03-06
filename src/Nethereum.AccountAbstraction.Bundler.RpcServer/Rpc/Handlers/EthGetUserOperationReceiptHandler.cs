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
                    throw RpcException.InvalidParams("userOpHash is required");

                var receipt = await _bundler.GetUserOperationReceiptAsync(userOpHash);

                if (receipt == null)
                    return Success(request.Id, null);

                return Success(request.Id, new
                {
                    userOpHash = receipt.UserOpHash,
                    entryPoint = receipt.EntryPoint,
                    sender = receipt.Sender,
                    nonce = receipt.Nonce?.HexValue ?? "0x0",
                    paymaster = receipt.Paymaster,
                    actualGasCost = receipt.ActualGasCost?.HexValue ?? "0x0",
                    actualGasUsed = receipt.ActualGasUsed?.HexValue ?? "0x0",
                    success = receipt.Success,
                    reason = receipt.Reason,
                    logs = receipt.Logs,
                    receipt = receipt.Receipt != null ? new
                    {
                        transactionHash = receipt.Receipt.TransactionHash,
                        blockHash = receipt.Receipt.BlockHash,
                        blockNumber = receipt.Receipt.BlockNumber?.HexValue,
                        from = receipt.Receipt.From,
                        to = receipt.Receipt.To,
                        cumulativeGasUsed = receipt.Receipt.CumulativeGasUsed?.HexValue,
                        gasUsed = receipt.Receipt.GasUsed?.HexValue,
                        status = receipt.Receipt.Status?.HexValue,
                        effectiveGasPrice = receipt.Receipt.EffectiveGasPrice?.HexValue
                    } : null
                });
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }
    }
}
