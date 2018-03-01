namespace Nethereum.Generator.Console.Generators.Services
{
    public class ServiceModel:CommonModel
    {

        public const string DEFAULT_CONTRACTNAME = "Contract";
       
        public string ContractName { get; }
       
        public ServiceModel(string abi, string byteCode, string contractName = DEFAULT_CONTRACTNAME,
            string namespaceName = CommonModel.DEFAULT_NAMESPACE):base(abi, byteCode, namespaceName)
        {
            ContractName = Utils.CapitaliseFirstChar(contractName);
        }
    }
}