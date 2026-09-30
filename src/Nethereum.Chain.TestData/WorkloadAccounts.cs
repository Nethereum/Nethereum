using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;

namespace Nethereum.Chain.TestData
{
    public sealed class WorkloadAccounts
    {
        public RosterAccount Alice => ChainRoster.First;
        public RosterAccount Bob   => ChainRoster.Second;
        public RosterAccount Carol => ChainRoster.Third;
        public RosterAccount Dave  => ChainRoster.Fourth;
        public RosterAccount Erin  => ChainRoster.Fifth;
        public RosterAccount Frank => ChainRoster.Sixth;
        public RosterAccount Grace => ChainRoster.Seventh;
        public RosterAccount Heidi => ChainRoster.Eighth;

        public IReadOnlyList<RosterAccount> All { get; }

        public WorkloadAccounts(int generatedCount = 0)
        {
            All = ChainRoster.Take(generatedCount);
        }

        private static readonly Nethereum.Util.Sha3Keccack Keccak = new Nethereum.Util.Sha3Keccack();

    }
}
