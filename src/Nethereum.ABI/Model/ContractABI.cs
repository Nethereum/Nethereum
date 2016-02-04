namespace Nethereum.ABI.FunctionEncoding
{
    public class ContractABI
    {
        public FunctionABI[] Functions { get; set; }
        public ConstructorABI Constructor { get; set; }
        public EventABI[] Events { get; set; }
    }
}