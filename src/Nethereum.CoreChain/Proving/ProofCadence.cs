namespace Nethereum.CoreChain.Proving
{
    public enum ProofCadenceMode { Off, OnDemand, Periodic, Continuous }

    public class ProofCadence
    {
        public ProofCadenceMode Mode { get; set; }
        public int PeriodicInterval { get; set; } = 1;

        public static ProofCadence Off => new ProofCadence { Mode = ProofCadenceMode.Off };
        public static ProofCadence Continuous => new ProofCadence { Mode = ProofCadenceMode.Continuous };
        public static ProofCadence OnDemand => new ProofCadence { Mode = ProofCadenceMode.OnDemand };

        public static ProofCadence Periodic(int n) => new ProofCadence
        {
            Mode = ProofCadenceMode.Periodic,
            PeriodicInterval = n > 0 ? n : 1
        };

        public bool ShouldProve(long blockNumber)
        {
            switch (Mode)
            {
                case ProofCadenceMode.Continuous: return true;
                case ProofCadenceMode.Periodic: return blockNumber % PeriodicInterval == 0;
                default: return false;
            }
        }
    }
}
