using Nethereum.Documentation;
using System.Collections.Generic;
using System.Security.Cryptography;
using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    /// <summary>
    /// EIP-7685 §Requests: <i>"A <c>requests</c> object consists of a <c>request_type</c> byte
    /// prepended to an opaque byte array <c>request_data</c>"</i>, <c>requests = request_type ++
    /// request_data</c>.
    ///
    /// <para>EIP-7685 §Block Header: <i>"In order to compute the commitment, an intermediate hash
    /// list is first built by hashing all non-empty requests elements of the block requests
    /// list."</i> and <i>"Items with empty <c>request_data</c> are excluded, i.e. the intermediate
    /// list skips <c>requests</c> items which contain only the <c>request_type</c> (1 byte) and
    /// nothing else."</i></para>
    /// </summary>
    [NethereumDocExample(DocSection.EvmSimulator, "execution-requests", "EIP-7685 request types and the block requests_hash")]
    public static class ExecutionRequests
    {
        public const byte DepositRequestType = 0x00;

        public const byte WithdrawalRequestType = 0x01;

        public const byte ConsolidationRequestType = 0x02;

        public const byte BuilderDepositRequestType = 0x03;

        public const byte BuilderExitRequestType = 0x04;

        public static byte RequestTypeFor(string predeployAddress)
        {
            if (predeployAddress.IsTheSameAddress(SystemCallContracts.WithdrawalRequests))
                return WithdrawalRequestType;
            if (predeployAddress.IsTheSameAddress(SystemCallContracts.ConsolidationRequests))
                return ConsolidationRequestType;
            if (predeployAddress.IsTheSameAddress(SystemCallContracts.BuilderDeposit))
                return BuilderDepositRequestType;
            if (predeployAddress.IsTheSameAddress(SystemCallContracts.BuilderExit))
                return BuilderExitRequestType;

            throw new System.ArgumentOutOfRangeException(
                nameof(predeployAddress), predeployAddress, "not a request predeploy");
        }

        [NethereumDocExample(DocSection.EvmSimulator, "execution-requests", "Compose a request as request_type ++ request_data")]
        public static byte[] Compose(byte requestType, byte[] requestData)
        {
            var length = requestData == null ? 0 : requestData.Length;
            var request = new byte[length + 1];
            request[0] = requestType;
            if (length > 0) System.Array.Copy(requestData, 0, request, 1, length);

            return request;
        }

        public static bool CarriesData(byte[] request) => request != null && request.Length > 1;

        public static bool IsValidEngineRequestsList(IReadOnlyList<byte[]> requests)
        {
            if (requests == null) return false;

            byte? previousType = null;
            foreach (var request in requests)
            {
                if (!CarriesData(request)) return false;

                var requestType = request[0];
                if (previousType.HasValue && requestType <= previousType.Value) return false;

                previousType = requestType;
            }

            return true;
        }

        /// <summary>
        /// EIP-7685 is introduced at Prague, so a header before it carries no <c>requests_hash</c>
        /// at all - which is a different thing from carrying the hash of an empty list.
        /// </summary>
        [NethereumDocExample(DocSection.EvmSimulator, "execution-requests", "EIP-7685 is introduced at Prague, so before it there is no requests_hash at all")]
        public static bool IsActive(HardforkName fork) => fork >= HardforkName.Prague;

        public static byte[] CommitmentFor(HardforkName fork, IEnumerable<byte[]> blockRequests) =>
            IsActive(fork) ? ComputeRequestsHash(blockRequests) : null;

        public static byte[] ComputeRequestsHash(IEnumerable<byte[]> blockRequests)
        {
            using (var sha256 = SHA256.Create())
            {
                var intermediate = new List<byte>();
                if (blockRequests != null)
                {
                    foreach (var request in blockRequests)
                    {
                        if (!CarriesData(request)) continue;
                        intermediate.AddRange(sha256.ComputeHash(request));
                    }
                }

                return sha256.ComputeHash(intermediate.ToArray());
            }
        }
    }
}
