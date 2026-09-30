using System.Collections.Generic;

namespace Nethereum.CoreChain.Freezer
{
    public sealed class DecodedCluster
    {
        public BlockBodyCluster Body { get; }
        public IReadOnlyList<DerivedReceipt> Receipts { get; }

        public IReadOnlyList<string> Senders { get; }

        public IReadOnlyList<string> ContractAddresses { get; }

        public DecodedCluster(
            BlockBodyCluster body,
            IReadOnlyList<DerivedReceipt> receipts,
            IReadOnlyList<string> senders,
            IReadOnlyList<string> contractAddresses)
        {
            Body = body;
            Receipts = receipts;
            Senders = senders;
            ContractAddresses = contractAddresses;
        }
    }
}
