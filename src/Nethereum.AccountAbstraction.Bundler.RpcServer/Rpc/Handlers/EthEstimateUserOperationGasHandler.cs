using System.Text.Json;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using DomainUserOperation = Nethereum.AccountAbstraction.UserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class EthEstimateUserOperationGasHandler : RpcHandlerBase
    {
        private readonly IBundlerService _bundler;

        public EthEstimateUserOperationGasHandler(IBundlerService bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "eth_estimateUserOperationGas";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var userOpJson = GetJsonElement(request, 0);
                var entryPoint = UserOperationRpcParser.ParseEntryPoint(GetJsonElement(request, 1));

                var rpcUserOp = UserOperationRpcParser.ParseUserOperationForEstimate(userOpJson);

                var domainUserOp = rpcUserOp.ToDomainUserOperation();

                var estimate = await _bundler.EstimateUserOperationGasAsync(domainUserOp, entryPoint);

                return Success(request.Id, new
                {
                    callGasLimit = ToHex(estimate.CallGasLimit.Value),
                    verificationGasLimit = ToHex(estimate.VerificationGasLimit.Value),
                    preVerificationGas = ToHex(estimate.PreVerificationGas.Value),
                    maxFeePerGas = ToHex(estimate.MaxFeePerGas?.Value ?? 0),
                    maxPriorityFeePerGas = ToHex(estimate.MaxPriorityFeePerGas?.Value ?? 0),
                    paymasterVerificationGasLimit = estimate.PaymasterVerificationGasLimit != null
                        ? ToHex(estimate.PaymasterVerificationGasLimit.Value) : null,
                    paymasterPostOpGasLimit = estimate.PaymasterPostOpGasLimit != null
                        ? ToHex(estimate.PaymasterPostOpGasLimit.Value) : null
                });
            }
            catch (RpcException)
            {
                throw;
            }
            catch (BundlerRpcException ex)
            {
                return Error(request.Id, ex.Code, ex.Message, ex.ErrorData);
            }
            catch (JsonException ex)
            {
                return Error(request.Id, BundlerErrorCodes.InvalidFields, $"Invalid UserOperation format: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                return Error(request.Id, BundlerErrorCodes.InvalidFields, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Error(request.Id, BundlerErrorCodes.SimulateValidation, ex.Message);
            }
            catch (Exception ex)
            {
                return BundlerRpcResponses.InternalError(request.Id, context, ex);
            }
        }
    }
}
