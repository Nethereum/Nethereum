namespace Nethereum.EVM.Execution.Storage
{
    public interface ISstoreRefundRule
    {
        void Apply(Program program, byte[] currentVal, byte[] newVal, byte[] origVal);
    }
}
