namespace Nethereum.EVM.Gas
{
    public class InstructionInfo
    {
        public InstructionInfo(string name, int additional, int args, int ret, bool sideEffects, GasPriceTier gasPriceTier)
        {
            Name = name;
            Additional = additional;
            Args = args;
            Ret = ret;
            SideEffects = sideEffects;
            GasPriceTier = gasPriceTier;
        }
        /// <summary>
        /// The name of the instruction.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Additional items required in memory for this instructions (only for PUSH).
        /// </summary>
        public int Additional { get; set; }
        /// <summary>
        /// Number of items required on the stack for this instruction (and, for the purposes of ret, the number taken from the stack).
        /// </summary>
        public int Args { get; set; }

        /// <summary>
        /// Number of items placed (back) on the stack by this instruction, assuming args items were removed.
        /// </summary>
        public int Ret { get; set; }

        /// <summary>
        /// false if the only effect on the execution environment (apart from gas usage) is a change to a topmost segment of the stack
        /// </summary>
        public bool SideEffects { get; set; }

        /// <summary>
        /// Tier for gas pricing.
        /// </summary>
        public GasPriceTier GasPriceTier { get; set; }
    }
}