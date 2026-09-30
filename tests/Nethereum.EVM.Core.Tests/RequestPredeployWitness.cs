using System.Collections.Generic;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// EIP-7002 §Specification: <i>"If there is no code at
    /// <c>WITHDRAWAL_REQUEST_PREDEPLOY_ADDRESS</c>, the corresponding block MUST be marked
    /// invalid."</i> EIP-7251 and EIP-8282 state the same for the other three. A witness for a
    /// block at or after Prague is incomplete until it carries them, because the block executes
    /// a system call against each.
    /// </summary>
    internal static class RequestPredeployWitness
    {
        private static readonly byte[] ReturnsNoRequests = "00".HexToByteArray();

        public static BlockWitnessData AddRequestPredeploys(this BlockWitnessData block)
        {
            if (block?.Accounts == null) return block;

            var fork = block.Features?.Fork ?? HardforkName.Unspecified;
            foreach (var address in SystemCallContracts.RequestContractsFor(fork))
            {
                if (IsAlreadyPresent(block.Accounts, address)) continue;

                block.Accounts.Add(new WitnessAccount
                {
                    Address = address,
                    Balance = EvmUInt256.Zero,
                    Nonce = 1,
                    Code = ReturnsNoRequests,
                    Storage = new List<WitnessStorageSlot>()
                });
            }

            return block;
        }

        private static bool IsAlreadyPresent(IList<WitnessAccount> accounts, string address)
        {
            for (var i = 0; i < accounts.Count; i++)
                if (accounts[i].Address.IsTheSameAddress(address))
                    return true;

            return false;
        }
    }
}
