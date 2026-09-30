namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    public enum SelfDestructSweepVerdict
    {
        NoNewAccountIsCreated,
        TheSweepBringsTheBeneficiaryIntoExistence,
        NotUntilTheContractBalanceIsKnown
    }
}
