namespace Nethereum.EVM.Execution.SelfDestruct
{
    public interface ISelfDestructRule
    {
        void Execute(ref SelfDestructContext context);
    }
}
