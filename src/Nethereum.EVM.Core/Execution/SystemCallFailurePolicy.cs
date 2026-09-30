using Nethereum.Util;

namespace Nethereum.EVM.Execution
{
    /// <summary>
    /// EIP-7002 §Specification: <i>"If the call to the contract fails or returns an error, the
    /// block MUST be invalidated."</i> and <i>"If there is no code at
    /// <c>WITHDRAWAL_REQUEST_PREDEPLOY_ADDRESS</c>, the corresponding block MUST be marked
    /// invalid."</i> EIP-7251 §Specification states both for
    /// <c>CONSOLIDATION_REQUEST_PREDEPLOY_ADDRESS</c>. EIP-8282 §Deployment: <i>"If there is no
    /// code at either address once the EIP is active, every block from activation onward MUST be
    /// invalid."</i>
    ///
    /// <para>EIP-4788 §Block processing states the opposite for its own predeploy: <i>"if no code
    /// exists at <c>BEACON_ROOTS_ADDRESS</c>, the call must fail silently"</i>. EIP-2935 §Block
    /// processing: <i>"if no code exists at <c>HISTORY_STORAGE_ADDRESS</c>, the call must fail
    /// silently"</i>.</para>
    /// </summary>
    public static class SystemCallFailurePolicy
    {
        public static bool FailureInvalidatesBlock(string contractAddress) =>
            NamesARequestPredeploy(contractAddress);

        public static bool AbsenceInvalidatesBlock(string contractAddress) =>
            NamesARequestPredeploy(contractAddress);

        private static bool NamesARequestPredeploy(string contractAddress)
        {
            var requestPredeploys = SystemCallContracts.AllRequestContracts;
            for (var i = 0; i < requestPredeploys.Count; i++)
                if (requestPredeploys[i].IsTheSameAddress(contractAddress))
                    return true;

            return false;
        }
    }
}
