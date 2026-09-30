using System.Collections.Generic;
using System.Linq;
using Nethereum.Model;

namespace Nethereum.EVM.Execution
{
    /// <summary>
    /// EIP-7685 §Block Header: <i>"Within the intermediate list, <c>requests</c> items must be
    /// ordered by <c>request_type</c> ascending."</i>
    ///
    /// <para>EIP-6110 §Block validity: <i>"Beginning with the <c>FORK_BLOCK</c>, each deposit
    /// accumulated in the block MUST appear in the EIP-7685 requests list in the order they appear
    /// in the logs."</i> Deposits are type <c>0x00</c>, so they open the list.</para>
    /// </summary>
    public sealed class BlockExecutionRequests
    {
        private readonly List<byte[]> _requests = new List<byte[]>();
        private readonly HardforkName _fork;

        private BlockExecutionRequests(HardforkName fork)
        {
            _fork = fork;
        }

        public static BlockExecutionRequests OpenedWithDeposits(
            HardforkName fork, IEnumerable<Log> logsInBlockOrder)
        {
            var requests = new BlockExecutionRequests(fork);
            if (!ExecutionRequests.IsActive(fork)) return requests;

            requests._requests.Add(ExecutionRequests.Compose(
                ExecutionRequests.DepositRequestType,
                DepositRequests.CollectRequestData(logsInBlockOrder)));

            return requests;
        }

        public void AddFrom(string predeployAddress, byte[] requestData) =>
            _requests.Add(ExecutionRequests.Compose(
                ExecutionRequests.RequestTypeFor(predeployAddress), requestData));

        public byte[] Commitment() => ExecutionRequests.CommitmentFor(_fork, _requests);

        public IReadOnlyList<byte[]> NonEmptyRequests() =>
            _requests.Where(ExecutionRequests.CarriesData).ToList();
    }
}
