namespace Nethereum.Contracts.CQS
{
    public class ContractDefinition
    {
        public string ByteCode { get; set; }
        public string Abi { get; set; }
        //We could make this network specific, referencing to libraries, etc
        public string ContractAddress { get; set; }
    }
}
