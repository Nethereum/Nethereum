using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs.Engine;

namespace Nethereum.CoreChain.Engine
{
    public interface IEngineApiService
    {
        Task<PayloadStatusV1> NewPayloadAsync(
            ExecutionPayloadV3 payload,
            string parentBeaconBlockRoot,
            CancellationToken ct = default);

        Task<ForkchoiceUpdatedResponseV1> ForkchoiceUpdatedAsync(
            ForkchoiceStateV1 state,
            PayloadAttributesV3 attributes,
            IReadOnlyList<ISignedTransaction> pendingTransactions = null,
            CancellationToken ct = default);

        Task<ExecutionPayloadV3> GetPayloadAsync(string payloadId, CancellationToken ct = default);

        Task<PayloadStatusV1> NewPayloadV4Async(
            ExecutionPayloadV3 payload,
            string[] executionRequests,
            string parentBeaconBlockRoot,
            CancellationToken ct = default);

        Task<ForkchoiceUpdatedResponseV1> ForkchoiceUpdatedV4Async(
            ForkchoiceStateV1 state,
            PayloadAttributesV4 attributes,
            IReadOnlyList<ISignedTransaction> pendingTransactions = null,
            CancellationToken ct = default);

        Task<GetPayloadV4Response> GetPayloadV4Async(string payloadId, CancellationToken ct = default);

        Task<PayloadStatusV1> NewPayloadV5Async(
            ExecutionPayloadV4 payload,
            string[] executionRequests,
            string parentBeaconBlockRoot,
            CancellationToken ct = default);

        Task<GetPayloadV6Response> GetPayloadV6Async(string payloadId, CancellationToken ct = default);
    }
}
