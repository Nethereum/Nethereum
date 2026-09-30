namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    public enum CallNewAccountVerdict
    {
        NoNewAccountIsCreated,
        TheTransferBringsTheTargetIntoExistence,
        NotUntilTheTargetDeadnessIsKnown
    }
}
