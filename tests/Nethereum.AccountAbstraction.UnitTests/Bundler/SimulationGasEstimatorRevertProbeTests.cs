using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPointSimulations;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Bundler
{
    public class SimulationGasEstimatorRevertProbeTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";
        private const string Sender = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task RevertProbe_FailsWithAA23Reverted_IsSkippedAndLogged()
        {
            var client = new SequencedEthCallClient();
            client.EnqueueSuccess(EncodeValidationResult(preOpGas: 100_000));
            client.EnqueueSuccess(EncodeExecutionResult(preOpGas: 50_000, paid: 60_000, targetSuccess: true));
            client.EnqueueRevert(EncodeFailedOpWithRevert(BigInteger.Zero, "AA23 reverted", Array.Empty<byte>()));

            var logger = new CapturingLogger();
            var estimator = new SimulationGasEstimator(new Web3.Web3(client), new BundlerConfig(), logger);

            var estimate = await estimator.EstimateAsync(CreateUserOp(), EntryPoint);

            Assert.NotNull(estimate);
            Assert.True(logger.WarningCount > 0,
                "the empty-callData probe's AA23 revert was swallowed but never logged - the skipped " +
                "execution-revert check is now an invisible coverage gap");
            Assert.Contains(logger.Warnings, w => w.Contains(Sender) && w.Contains("AA23"));
        }

        [Fact]
        public async Task RevertProbe_FailsWithADifferentReason_Propagates()
        {
            var client = new SequencedEthCallClient();
            client.EnqueueSuccess(EncodeValidationResult(preOpGas: 100_000));
            client.EnqueueSuccess(EncodeExecutionResult(preOpGas: 50_000, paid: 60_000, targetSuccess: true));
            client.EnqueueRevert(EncodeFailedOpWithRevert(BigInteger.Zero, "AA25 invalid account nonce", Array.Empty<byte>()));

            var estimator = new SimulationGasEstimator(new Web3.Web3(client), new BundlerConfig());

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(
                () => estimator.EstimateAsync(CreateUserOp(), EntryPoint));

            Assert.Equal(BundlerErrorCodes.SimulateValidation, ex.Code);
            Assert.Contains("AA25", ex.Message);
        }

        [Fact]
        public async Task RevertProbe_FailsWithAPlainFailedOpReason_Propagates()
        {
            var client = new SequencedEthCallClient();
            client.EnqueueSuccess(EncodeValidationResult(preOpGas: 100_000));
            client.EnqueueSuccess(EncodeExecutionResult(preOpGas: 50_000, paid: 60_000, targetSuccess: true));
            client.EnqueueRevert(EncodeFailedOp(BigInteger.Zero, "AA13 initCode failed or OOG"));

            var estimator = new SimulationGasEstimator(new Web3.Web3(client), new BundlerConfig());

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(
                () => estimator.EstimateAsync(CreateUserOp(), EntryPoint));

            Assert.Equal(BundlerErrorCodes.SimulateValidation, ex.Code);
            Assert.Contains("AA13", ex.Message);
        }

        [Fact]
        public async Task RevertProbe_TargetGenuinelyReverts_ThrowsUserOperationReverted()
        {
            var client = new SequencedEthCallClient();
            client.EnqueueSuccess(EncodeValidationResult(preOpGas: 100_000));
            client.EnqueueSuccess(EncodeExecutionResult(preOpGas: 50_000, paid: 60_000, targetSuccess: true));
            client.EnqueueSuccess(EncodeExecutionResult(
                preOpGas: 50_000, paid: 60_000, targetSuccess: false, targetResult: new byte[] { 0xde, 0xad, 0xbe, 0xef }));

            var estimator = new SimulationGasEstimator(new Web3.Web3(client), new BundlerConfig());

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(
                () => estimator.EstimateAsync(CreateUserOp(), EntryPoint));

            Assert.Equal(BundlerErrorCodes.UserOperationReverted, ex.Code);
        }

        private static UserOperation CreateUserOp() => new UserOperation
        {
            Sender = Sender,
            Nonce = 0,
            CallData = new byte[] { 0x01, 0x02, 0x03, 0x04 },
            MaxFeePerGas = 1_000_000_000,
            MaxPriorityFeePerGas = 1_000_000_000,
            Signature = new byte[65]
        };

        private static string EncodeValidationResult(BigInteger preOpGas)
        {
            var dto = new SimulateValidationOutputDTO
            {
                Result = new ValidationResult
                {
                    ReturnInfo = new ReturnInfo
                    {
                        PreOpGas = preOpGas,
                        Prefund = 0,
                        AccountValidationData = 0,
                        PaymasterValidationData = 0,
                        PaymasterContext = Array.Empty<byte>()
                    },
                    SenderInfo = new StakeInfo(),
                    FactoryInfo = new StakeInfo(),
                    PaymasterInfo = new StakeInfo(),
                    AggregatorInfo = new AggregatorStakeInfo
                    {
                        Aggregator = "0x0000000000000000000000000000000000000000",
                        StakeInfo = new StakeInfo()
                    }
                }
            };
            return new ABIEncode().GetABIParamsEncoded(dto).ToHex(true);
        }

        private static string EncodeExecutionResult(
            BigInteger preOpGas, BigInteger paid, bool targetSuccess, byte[] targetResult = null)
        {
            var dto = new SimulateHandleOpOutputDTO
            {
                Result = new ExecutionResult
                {
                    PreOpGas = preOpGas,
                    Paid = paid,
                    AccountValidationData = 0,
                    PaymasterValidationData = 0,
                    TargetSuccess = targetSuccess,
                    TargetResult = targetResult ?? Array.Empty<byte>()
                }
            };
            return new ABIEncode().GetABIParamsEncoded(dto).ToHex(true);
        }

        private static string EncodeFailedOpWithRevert(BigInteger opIndex, string reason, byte[] inner)
        {
            var errorAbi = ABITypedRegistry.GetError<FailedOpWithRevertError>();
            return new FunctionCallEncoder().EncodeRequest(errorAbi.Sha3Signature, errorAbi.InputParameters, opIndex, reason, inner);
        }

        private static string EncodeFailedOp(BigInteger opIndex, string reason)
        {
            var errorAbi = ABITypedRegistry.GetError<FailedOpError>();
            return new FunctionCallEncoder().EncodeRequest(errorAbi.Sha3Signature, errorAbi.InputParameters, opIndex, reason);
        }

        private sealed class SequencedEthCallClient : ClientBase
        {
            private readonly Queue<Func<object, RpcResponseMessage>> _responders = new();

            public void EnqueueSuccess(string rawHexResult) =>
                _responders.Enqueue(id => new RpcResponseMessage(id, rawHexResult));

            public void EnqueueRevert(string encodedErrorData) =>
                _responders.Enqueue(id => new RpcResponseMessage(id, new Nethereum.JsonRpc.Client.RpcMessages.RpcError
                {
                    Code = 3,
                    Message = "execution reverted",
                    Data = encodedErrorData
                }));

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null)
            {
                if (_responders.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"No more fake responses queued for '{rpcRequestMessage.Method}' - the estimator made more RPC calls than this test expected.");
                }

                return Task.FromResult(_responders.Dequeue()(rpcRequestMessage.Id));
            }

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
                throw new NotSupportedException("SimulationGasEstimator does not batch eth_call requests.");
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _warnings = new();

            public IEnumerable<string> Warnings => _warnings;
            public int WarningCount => _warnings.Count;

            IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    _warnings.Enqueue(formatter(state, exception));
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}
